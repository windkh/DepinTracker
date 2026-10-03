namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>
/// Persistence port for imported <see cref="RewardTransaction"/> data (imported.db).
/// Writes are insert-or-ignore by dedup key so imported financial data is never
/// silently overwritten or double-counted.
/// </summary>
public interface IRewardRepository
{
    Task<IReadOnlyList<RewardTransaction>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RewardTransaction>> GetByWalletAsync(Guid walletId, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>Returns the dedup keys already present, for the given candidate keys.</summary>
    Task<IReadOnlySet<string>> GetExistingDedupKeysAsync(
        IEnumerable<string> candidateKeys, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the given rewards, ignoring any whose dedup key already exists.
    /// Returns the number of rows actually inserted.
    /// </summary>
    Task<int> AddManyIgnoreDuplicatesAsync(
        IEnumerable<RewardTransaction> rewards, CancellationToken cancellationToken);

    /// <summary>Deletes all rewards for a wallet (used by the rebuild engine).</summary>
    Task<int> DeleteByWalletAsync(Guid walletId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically wipes every reward, raw response and import session belonging to
    /// the given wallets — the "clear imported data" operation. Returns the number
    /// of reward rows deleted.
    /// </summary>
    Task<int> DeleteAllForWalletsAsync(IEnumerable<Guid> walletIds, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes individual rewards by id (the user's explicit "remove" action). Import
    /// sessions and raw responses are kept for provenance. Returns the rows deleted.
    /// </summary>
    Task<int> DeleteByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}
