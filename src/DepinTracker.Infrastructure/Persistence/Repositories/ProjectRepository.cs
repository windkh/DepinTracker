namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IProjectRepository"/> over the config store.</summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly TagWriter _tags;

    public ProjectRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
        _tags = new TagWriter(factory);
    }

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var project = await connection.QuerySingleOrDefaultAsync<Project>(
            new CommandDefinition(
                "SELECT * FROM projects WHERE Id = @id;", new { id }, cancellationToken: cancellationToken));
        if (project is not null)
        {
            project.Tags = await _tags.LoadAsync("project_tags", "ProjectId", id, cancellationToken);
            project.RewardSourceAddresses = await LoadSourcesAsync(connection, id, cancellationToken);
        }

        return project;
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var projects = (await connection.QueryAsync<Project>(
            new CommandDefinition("SELECT * FROM projects ORDER BY Name;", cancellationToken: cancellationToken)))
            .ToList();

        foreach (var project in projects)
        {
            project.Tags = await _tags.LoadAsync("project_tags", "ProjectId", project.Id, cancellationToken);
            project.RewardSourceAddresses = await LoadSourcesAsync(connection, project.Id, cancellationToken);
        }

        return projects;
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO projects (Id, Name, Description, Notes, IsActive, CreatedUtc)
            VALUES (@Id, @Name, @Description, @Notes, @IsActive, @CreatedUtc);
            """,
            project, cancellationToken: cancellationToken));
        await _tags.SyncAsync("project_tags", "ProjectId", project.Id, project.Tags, cancellationToken);
        await SyncSourcesAsync(connection, project.Id, project.RewardSourceAddresses, cancellationToken);
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE projects
               SET Name = @Name, Description = @Description, Notes = @Notes, IsActive = @IsActive
             WHERE Id = @Id;
            """,
            project, cancellationToken: cancellationToken));
        await _tags.SyncAsync("project_tags", "ProjectId", project.Id, project.Tags, cancellationToken);
        await SyncSourcesAsync(connection, project.Id, project.RewardSourceAddresses, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM projects WHERE Id = @id;", new { id }, cancellationToken: cancellationToken));
    }

    private static async Task<IList<string>> LoadSourcesAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT Address FROM project_reward_sources WHERE ProjectId = @projectId ORDER BY Address;",
            new { projectId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static async Task SyncSourcesAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection, Guid projectId,
        IEnumerable<string> addresses, CancellationToken cancellationToken)
    {
        // Trim + dedupe; preserve the original case so Solana's case-sensitive base58
        // addresses survive a round-trip. Comparison at filter time is case-insensitive,
        // which still does the right thing for EVM (always returned lowercase by the
        // explorer) and is safe for Solana (collisions are astronomically unlikely).
        var normalized = addresses
            .Select(a => (a ?? string.Empty).Trim())
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM project_reward_sources WHERE ProjectId = @projectId;",
            new { projectId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (var address in normalized)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO project_reward_sources (ProjectId, Address) VALUES (@projectId, @address);",
                new { projectId, address }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
