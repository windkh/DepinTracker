namespace DepinTracker.Plugins.Abstractions.Models;

using DepinTracker.Domain.Entities;
using DepinTracker.Domain.ValueObjects;

/// <summary>
/// Input for an analytics computation. Carries the already-loaded reward set and
/// the period of interest so providers stay pure (no data access of their own).
/// </summary>
public sealed record AnalyticsRequest(
    IReadOnlyList<RewardTransaction> Rewards,
    DateRange? Period,
    string ReportingCurrency);
