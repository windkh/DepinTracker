namespace DepinTracker.Domain.Entities;

using DepinTracker.Domain.Enums;

/// <summary>
/// A disposal event — a sale, swap, transfer-out, spend or loss that reduces
/// the holding of a token. Tracked separately from <see cref="RewardTransaction"/>
/// because rewards are income (taxed on receipt) while disposals create the
/// matching capital-gains event the FIFO matcher pairs against the reward lots.
///
/// Amounts and proceeds are <see cref="decimal"/> for exact accounting; the
/// <see cref="DedupKey"/> protects against double-import.
/// </summary>
public sealed class DispositionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? WalletId { get; set; }
    public string? BlockchainKey { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }

    public string TokenSymbol { get; set; } = string.Empty;
    public string? TokenContract { get; set; }

    /// <summary>Quantity disposed (always positive — the "disposal" direction is implicit).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Per-unit proceeds in <see cref="ProceedsCurrency"/> at disposal time. Null for
    /// disposals without a sale price (e.g. transfer to a self-custody wallet); FIFO
    /// treats those as zero-proceeds, so the realised gain equals minus the cost basis.
    /// </summary>
    public decimal? ProceedsPerUnit { get; set; }
    public string? ProceedsCurrency { get; set; }

    public DispositionKind Kind { get; set; } = DispositionKind.Unknown;

    public string ProviderKey { get; set; } = "manual";
    public Guid ImportSessionId { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Natural identity for insert-or-ignore dedupe. Mirrors the reward dedup key.
    /// </summary>
    public string DedupKey =>
        $"{(WalletId?.ToString("N") ?? "manual")}|{BlockchainKey ?? string.Empty}|{TxHash}|{TokenSymbol}|{Amount}|{TimestampUtc.UtcDateTime:O}";
}
