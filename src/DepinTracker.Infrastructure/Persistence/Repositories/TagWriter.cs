namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Domain.Entities;

/// <summary>
/// Shared helper for the many-to-many tag links used by projects and wallets.
/// Tags are de-duplicated by name in a shared <c>tags</c> table; link rows live in
/// the owner-specific join table (<c>project_tags</c> / <c>wallet_tags</c>).
/// </summary>
internal sealed class TagWriter
{
    private readonly ISqliteConnectionFactory _factory;

    public TagWriter(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<IList<Tag>> LoadAsync(
        string linkTable, string ownerColumn, Guid ownerId, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        var tags = await connection.QueryAsync<Tag>(new CommandDefinition(
            $"""
             SELECT t.Id, t.Name
               FROM tags t
               JOIN {linkTable} l ON l.TagId = t.Id
              WHERE l.{ownerColumn} = @ownerId
              ORDER BY t.Name;
             """,
            new { ownerId }, cancellationToken: cancellationToken));
        return tags.ToList();
    }

    /// <summary>Replaces the owner's tag links with the supplied set, creating tags as needed.</summary>
    public async Task SyncAsync(
        string linkTable, string ownerColumn, Guid ownerId, IEnumerable<Tag> tags, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Config);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {linkTable} WHERE {ownerColumn} = @ownerId;",
            new { ownerId }, transaction, cancellationToken: cancellationToken));

        foreach (var tag in tags)
        {
            var name = tag.Name.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var tagId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT Id FROM tags WHERE Name = @name;", new { name }, transaction, cancellationToken: cancellationToken));

            if (tagId is null)
            {
                tagId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO tags (Id, Name) VALUES (@id, @name);",
                    new { id = tagId, name }, transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                $"INSERT OR IGNORE INTO {linkTable} ({ownerColumn}, TagId) VALUES (@ownerId, @tagId);",
                new { ownerId, tagId }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
