namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IDispositionRepository"/> over the imported store.</summary>
public sealed class DispositionRepository : IDispositionRepository
{
    private const string SelectColumns =
        "Id, WalletId, BlockchainKey, TxHash, TimestampUtc, TokenSymbol, TokenContract, " +
        "Amount, ProceedsPerUnit, ProceedsCurrency, Kind, ProviderKey, ImportSessionId, Notes, CreatedUtc";

    private readonly ISqliteConnectionFactory _factory;

    public DispositionRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<DispositionRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        var rows = await connection.QueryAsync<DispositionRecord>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM dispositions ORDER BY TimestampUtc;",
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> AddIgnoreDuplicatesAsync(DispositionRecord record, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT OR IGNORE INTO dispositions
                (Id, WalletId, BlockchainKey, TxHash, TimestampUtc, TokenSymbol, TokenContract,
                 Amount, ProceedsPerUnit, ProceedsCurrency, Kind, ProviderKey, ImportSessionId,
                 Notes, CreatedUtc, DedupKey)
            VALUES
                (@Id, @WalletId, @BlockchainKey, @TxHash, @TimestampUtc, @TokenSymbol, @TokenContract,
                 @Amount, @ProceedsPerUnit, @ProceedsCurrency, @Kind, @ProviderKey, @ImportSessionId,
                 @Notes, @CreatedUtc, @DedupKey);
            """,
            new
            {
                record.Id,
                record.WalletId,
                record.BlockchainKey,
                record.TxHash,
                record.TimestampUtc,
                record.TokenSymbol,
                record.TokenContract,
                record.Amount,
                record.ProceedsPerUnit,
                record.ProceedsCurrency,
                record.Kind,
                record.ProviderKey,
                record.ImportSessionId,
                record.Notes,
                record.CreatedUtc,
                record.DedupKey,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteForWalletsAsync(IEnumerable<Guid> walletIds, CancellationToken cancellationToken)
    {
        var ids = walletIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dispositions WHERE WalletId IN @ids;",
            new { ids }, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dispositions WHERE Id IN @list;",
            new { list }, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteUnassignedAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dispositions WHERE WalletId IS NULL;",
            cancellationToken: cancellationToken));
    }
}
