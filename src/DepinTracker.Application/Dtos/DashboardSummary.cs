namespace DepinTracker.Application.Dtos;

using DepinTracker.Domain.ValueObjects;

/// <summary>
/// The headline figures shown on the dashboard. Computed by <c>DashboardService</c>
/// from configuration + imported data + valuations.
/// </summary>
public sealed record DashboardSummary(
    Money PortfolioValue,
    int RewardCount,
    int WalletCount,
    int ProjectCount,
    int MissingPriceCount,
    string SyncState,
    string DatabaseHealth,
    IReadOnlyList<RewardsOverTimePoint> RewardsOverTime,
    IReadOnlyList<YearlyRewardsPoint> RewardsByYear,
    IReadOnlyList<TokensPerMonthPoint> TokensByMonth,
    IReadOnlyList<TokenBreakdownRow> TokenBreakdown);

/// <summary>
/// Per-token holdings summary for the dashboard: how much of each token was received,
/// across how many transactions, and its total priced fiat value. <see cref="FiatValue"/>
/// is null when no transaction of that token could be priced, and
/// <see cref="MissingPriceCount"/> exposes how much of the token is unpriced so a
/// partially-priced token isn't mistaken for a complete valuation.
/// </summary>
public sealed record TokenBreakdownRow(
    string TokenSymbol,
    decimal Quantity,
    int Count,
    decimal? FiatValue,
    int MissingPriceCount,
    string Currency);

/// <summary>A point in the rewards-over-time series (monthly buckets) for the chart.</summary>
public sealed record RewardsOverTimePoint(DateOnly Month, decimal FiatValue);

/// <summary>Total priced reward value for a calendar year (reporting currency).</summary>
public sealed record YearlyRewardsPoint(int Year, decimal FiatValue, int RewardCount);

/// <summary>
/// Total token quantity received per (month, symbol). Native quantities are kept
/// separate per symbol — adding GEOD + ETH would be meaningless.
/// </summary>
public sealed record TokensPerMonthPoint(DateOnly Month, string TokenSymbol, decimal Amount);
