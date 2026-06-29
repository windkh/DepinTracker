namespace DepinTracker.Plugins.Abstractions;

using DepinTracker.Domain.Enums;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Assigns a <see cref="RewardKind"/> to a parsed transaction. Lets projects plug
/// in their own heuristics (contract addresses, method signatures, amounts)
/// without baking project-specific rules into the core.
/// </summary>
public interface IRewardClassifier
{
    string ProviderKey { get; }

    /// <summary>
    /// Classify a reward. Implementations should return <see cref="RewardKind.Unknown"/>
    /// when they have no opinion, allowing the host to fall through to a default.
    /// </summary>
    Task<RewardKind> ClassifyAsync(
        string blockchainKey,
        ExplorerReward reward,
        CancellationToken cancellationToken);
}
