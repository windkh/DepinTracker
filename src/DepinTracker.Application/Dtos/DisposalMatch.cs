namespace DepinTracker.Application.Dtos;

using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// A single match between a (partial) reward lot and a (partial) disposal,
/// computed by the FIFO matcher. One disposal can produce multiple matches if
/// it consumes more than one lot; conversely one lot can be split across
/// multiple disposals.
/// </summary>
public sealed record DisposalMatch(
    DispositionRecord Disposal,
    RewardTransaction Lot,
    decimal Quantity,
    /// <summary>Per-unit fiat value at the time the lot was acquired (reward valuation).</summary>
    decimal? CostBasisPerUnit,
    /// <summary>Per-unit fiat proceeds at disposal time (null when no proceeds recorded).</summary>
    decimal? ProceedsPerUnit,
    string ReportingCurrency,
    /// <summary>true ⇔ disposal occurred ≥ 1 year after the lot was acquired (DE §23 EStG).</summary>
    bool LongTerm,
    /// <summary>Convenience: <c>Quantity × (Proceeds − CostBasis)</c> in the reporting currency, or null if either side is unknown.</summary>
    decimal? RealisedGain)
{
    public DispositionKind Kind => Disposal.Kind;
    public string TokenSymbol => Lot.TokenSymbol;
    public DateTimeOffset AcquiredUtc => Lot.TimestampUtc;
    public DateTimeOffset DisposedUtc => Disposal.TimestampUtc;
}
