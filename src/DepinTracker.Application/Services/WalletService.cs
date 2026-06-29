namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>
/// Use-case operations for wallets, including activation/deactivation. Validates
/// that the chosen blockchain exists in the runtime registry so wallets can only
/// reference chains a loaded plugin actually supports.
/// </summary>
public sealed class WalletService
{
    private readonly IWalletRepository _wallets;
    private readonly IBlockchainRegistry _chains;
    private readonly IClock _clock;

    public WalletService(IWalletRepository wallets, IBlockchainRegistry chains, IClock clock)
    {
        _wallets = wallets;
        _chains = chains;
        _clock = clock;
    }

    public Task<IReadOnlyList<Wallet>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        _wallets.GetByProjectAsync(projectId, cancellationToken);

    public Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _wallets.GetAsync(id, cancellationToken);

    public async Task<Wallet> CreateAsync(
        Guid projectId, string blockchainKey, string address, string? label, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Wallet address is required.", nameof(address));
        }

        if (!_chains.TryGet(blockchainKey, out _))
        {
            throw new ArgumentException(
                $"Unknown blockchain '{blockchainKey}'. No loaded plugin supports it.", nameof(blockchainKey));
        }

        var wallet = new Wallet
        {
            ProjectId = projectId,
            BlockchainKey = blockchainKey,
            Address = address.Trim(),
            Label = label,
            IsActive = true,
            CreatedUtc = _clock.UtcNow,
        };

        await _wallets.AddAsync(wallet, cancellationToken).ConfigureAwait(false);
        return wallet;
    }

    public async Task SetActiveAsync(Guid walletId, bool isActive, CancellationToken cancellationToken)
    {
        var wallet = await _wallets.GetAsync(walletId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Wallet {walletId} not found.");
        wallet.IsActive = isActive;
        await _wallets.UpdateAsync(wallet, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Wallet wallet, CancellationToken cancellationToken) =>
        _wallets.UpdateAsync(wallet, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _wallets.DeleteAsync(id, cancellationToken);
}
