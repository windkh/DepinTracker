namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>
/// Persistence port for <see cref="DispositionRecord"/>. Lives in imported.db so
/// disposals share the "authoritative imported financial data" backup boundary
/// with reward_transactions.
/// </summary>
public interface IDispositionRepository
{
    Task<IReadOnlyList<DispositionRecord>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Inserts the given disposal, returning 1 on success or 0 if a dedup-key collision was ignored.</summary>
    Task<int> AddIgnoreDuplicatesAsync(DispositionRecord record, CancellationToken cancellationToken);

    /// <summary>Atomic wipe used by the project-level "clear imported data" path.</summary>
    Task<int> DeleteForWalletsAsync(IEnumerable<Guid> walletIds, CancellationToken cancellationToken);
}
