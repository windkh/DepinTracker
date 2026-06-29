namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>
/// Dapper-backed <see cref="IRewardRepository"/> over the imported store. Inserts
/// use <c>INSERT OR IGNORE</c> against the unique <c>DedupKey</c> so re-running an
/// import never overwrites or double-counts authoritative financial data.
/// </summary>
public sealed class RewardRepository : IRewardRepository
{
    private const string SelectColumns =
        "Id, WalletId, BlockchainKey, FromAddress, TxHash, BlockNumber, TimestampUtc, TokenSymbol, " +
        "TokenContract, Amount, Kind, ProviderKey, ImportSessionId, RawResponseId, CreatedUtc";

    private readonly ISqliteConnectionFactory _factory;

    public RewardRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<RewardTransaction>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        var rows = await connection.QueryAsync<RewardTransaction>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM reward_transactions ORDER BY TimestampUtc;",
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<RewardTransaction>> GetByWalletAsync(
        Guid walletId, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        var rows = await connection.QueryAsync<RewardTransaction>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM reward_transactions WHERE WalletId = @walletId ORDER BY TimestampUtc;",
            new { walletId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM reward_transactions;", cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlySet<string>> GetExistingDedupKeysAsync(
        IEnumerable<string> candidateKeys, CancellationToken cancellationToken)
    {
        var keys = candidateKeys.Distinct().ToList();
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (keys.Count == 0)
        {
            return found;
        }

        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        foreach (var chunk in keys.Chunk(500))
        {
            var present = await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT DedupKey FROM reward_transactions WHERE DedupKey IN @chunk;",
                new { chunk }, cancellationToken: cancellationToken));
            foreach (var key in present)
            {
                found.Add(key);
            }
        }

        return found;
    }

    public async Task<int> AddManyIgnoreDuplicatesAsync(
        IEnumerable<RewardTransaction> rewards, CancellationToken cancellationToken)
    {
        var list = rewards.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var inserted = 0;
        foreach (var reward in list)
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT OR IGNORE INTO reward_transactions
                    (Id, WalletId, BlockchainKey, FromAddress, TxHash, BlockNumber, TimestampUtc, TokenSymbol,
                     TokenContract, Amount, Kind, ProviderKey, ImportSessionId, RawResponseId, CreatedUtc, DedupKey)
                VALUES
                    (@Id, @WalletId, @BlockchainKey, @FromAddress, @TxHash, @BlockNumber, @TimestampUtc, @TokenSymbol,
                     @TokenContract, @Amount, @Kind, @ProviderKey, @ImportSessionId, @RawResponseId, @CreatedUtc, @DedupKey);
                """,
                new
                {
                    reward.Id,
                    reward.WalletId,
                    reward.BlockchainKey,
                    reward.FromAddress,
                    reward.TxHash,
                    reward.BlockNumber,
                    reward.TimestampUtc,
                    reward.TokenSymbol,
                    reward.TokenContract,
                    reward.Amount,
                    reward.Kind,
                    reward.ProviderKey,
                    reward.ImportSessionId,
                    reward.RawResponseId,
                    reward.CreatedUtc,
                    reward.DedupKey,
                },
                transaction, cancellationToken: cancellationToken));
            inserted += rows;
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    public async Task<int> DeleteByWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM reward_transactions WHERE WalletId = @walletId;",
            new { walletId }, cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteAllForWalletsAsync(IEnumerable<Guid> walletIds, CancellationToken cancellationToken)
    {
        var ids = walletIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // raw_provider_responses + import_sessions reference each wallet via ImportSessionId/WalletId.
        // Delete child rows first so we don't dangle, then the rewards, then the sessions themselves.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM raw_provider_responses
            WHERE ImportSessionId IN (SELECT Id FROM import_sessions WHERE WalletId IN @ids);
            """,
            new { ids }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var rewards = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM reward_transactions WHERE WalletId IN @ids;",
            new { ids }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM import_sessions WHERE WalletId IN @ids;",
            new { ids }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rewards;
    }
}
