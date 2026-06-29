namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>Persistence port for <see cref="Wallet"/> configuration data (config.db).</summary>
public interface IWalletRepository
{
    Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Wallet>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Wallet>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken);
    Task UpdateAsync(Wallet wallet, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
