namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IImportSessionRepository"/> over the imported store.</summary>
public sealed class ImportSessionRepository : IImportSessionRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public ImportSessionRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task AddAsync(ImportSession session, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO import_sessions
                (Id, ProviderKey, Source, Status, WalletId, StartedUtc, CompletedUtc, ItemsImported, ItemsSkipped, Message)
            VALUES
                (@Id, @ProviderKey, @Source, @Status, @WalletId, @StartedUtc, @CompletedUtc, @ItemsImported, @ItemsSkipped, @Message);
            """,
            session, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(ImportSession session, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE import_sessions
               SET Status = @Status, CompletedUtc = @CompletedUtc, ItemsImported = @ItemsImported,
                   ItemsSkipped = @ItemsSkipped, Message = @Message
             WHERE Id = @Id;
            """,
            session, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ImportSession>> GetRecentAsync(int count, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        var rows = await connection.QueryAsync<ImportSession>(new CommandDefinition(
            "SELECT * FROM import_sessions ORDER BY StartedUtc DESC LIMIT @count;",
            new { count }, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
