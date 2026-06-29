namespace DepinTracker.Plugins.Abstractions.Models;

using DepinTracker.Domain.Enums;

/// <summary>
/// A reward-like transaction parsed by an explorer plugin from raw chain data,
/// before it is persisted as a <c>RewardTransaction</c>. Intentionally a flat,
/// transport-style record so explorers don't depend on persistence concerns.
/// </summary>
public sealed record ExplorerReward(
    string TxHash,
    long? BlockNumber,
    DateTimeOffset TimestampUtc,
    string TokenSymbol,
    string? TokenContract,
    decimal Amount,
    RewardKind Kind,
    string? FromAddress = null);
