namespace DepinTracker.Domain.Entities;

using DepinTracker.Domain.Enums;

/// <summary>
/// A single imported reward transaction — the core financial record of the
/// application. These live in imported.db and are authoritative: they carry the
/// transaction hash, block, provider and import session for full provenance and
/// are never silently overwritten once imported.
/// </summary>
public sealed class RewardTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletId { get; set; }
    public string BlockchainKey { get; set; } = string.Empty;

    /// <summary>
    /// On-chain <c>from</c> address (token transfer sender). Stored lower-cased so it
    /// compares cleanly against project-level allow-lists. Null for manual entries.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>On-chain transaction hash. Empty for purely manual entries that have none.</summary>
    public string TxHash { get; set; } = string.Empty;

    public long? BlockNumber { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }

    public string TokenSymbol { get; set; } = string.Empty;

    /// <summary>Token contract address (null for native-token rewards).</summary>
    public string? TokenContract { get; set; }

    /// <summary>Reward quantity. Decimal for exact accounting.</summary>
    public decimal Amount { get; set; }

    public RewardKind Kind { get; set; } = RewardKind.Reward;

    // Provenance.
    public string ProviderKey { get; set; } = string.Empty;
    public Guid ImportSessionId { get; set; }
    public Guid? RawResponseId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Natural identity used to detect duplicates so imported data is never
    /// overwritten or double-counted across import sessions.
    /// </summary>
    public string DedupKey =>
        $"{WalletId:N}|{BlockchainKey}|{TxHash}|{TokenSymbol}|{Amount}";
}
