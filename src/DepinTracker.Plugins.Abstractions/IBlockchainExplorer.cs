namespace DepinTracker.Plugins.Abstractions;

using DepinTracker.Domain.Entities;
using DepinTracker.Domain.ValueObjects;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Reads on-chain data for one or more blockchains. Explorer plugins both declare
/// the chains they support (registering them with the host) and fetch reward
/// transactions for a wallet address. Implementations must be async and honour
/// the supplied <see cref="CancellationToken"/>.
/// </summary>
public interface IBlockchainExplorer
{
    /// <summary>Stable provider key, recorded on every imported row for provenance.</summary>
    string ProviderKey { get; }

    /// <summary>Blockchains this explorer can serve.</summary>
    IReadOnlyCollection<Blockchain> SupportedChains { get; }

    /// <summary>Whether this explorer handles the given blockchain key.</summary>
    bool Supports(string blockchainKey);

    /// <summary>
    /// Fetch reward transactions for <paramref name="address"/> on the given chain,
    /// optionally bounded by <paramref name="range"/>. Returns the parsed rewards
    /// together with the raw payload for auditing.
    /// </summary>
    Task<ExplorerFetchResult> FetchRewardsAsync(
        string blockchainKey,
        string address,
        DateRange? range,
        CancellationToken cancellationToken);
}
