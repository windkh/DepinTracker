namespace DepinTracker.Domain.Enums;

/// <summary>
/// Classification of a reward transaction. Determined either at import time or
/// later by an <c>IRewardClassifier</c> plugin. Kept deliberately generic so it
/// applies to any DePIN project rather than encoding project-specific rules.
/// </summary>
public enum RewardKind
{
    Unknown = 0,
    Reward = 1,
    Staking = 2,
    Airdrop = 3,
    Fee = 4,
    Transfer = 5,

    /// <summary>
    /// Unsolicited scam / phishing airdrop (worthless token whose "symbol" encodes a
    /// URL or marketing lure). Flagged so it can be visually separated and excluded.
    /// </summary>
    Spam = 6,
}
