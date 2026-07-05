namespace DepinTracker.Application.Services;

using DepinTracker.Application.Dtos;
using DepinTracker.Domain.Entities;

/// <summary>
/// Computes portfolio analytics from imported rewards and their valuations:
/// holdings per token and a monthly rewards-value series for charting. Pure
/// aggregation over data supplied by callers, keeping it deterministic.
/// </summary>
public sealed class AnalyticsService
{
    /// <summary>Aggregates reward quantities into per-token holdings.</summary>
    public IReadOnlyList<Holding> ComputeHoldings(IEnumerable<RewardTransaction> rewards)
    {
        return rewards
            .GroupBy(r => r.TokenSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Holding
            {
                TokenSymbol = g.Key,
                Quantity = g.Sum(r => r.Amount),
            })
            .OrderByDescending(h => h.Quantity)
            .ToList();
    }

    /// <summary>
    /// Buckets valued rewards by calendar month, summing the fiat value. Rewards
    /// with no resolvable price are excluded from the value total (and counted as
    /// missing elsewhere) rather than treated as zero.
    /// </summary>
    public IReadOnlyList<RewardsOverTimePoint> ComputeRewardsByMonth(IEnumerable<RewardValuation> valuations)
    {
        return valuations
            .Where(v => v.HasPrice)
            .GroupBy(v =>
            {
                var d = v.Reward.TimestampUtc.UtcDateTime;
                return new DateOnly(d.Year, d.Month, 1);
            })
            .Select(g => new RewardsOverTimePoint(g.Key, g.Sum(v => v.Value!.Value.Amount)))
            .OrderBy(p => p.Month)
            .ToList();
    }

    /// <summary>
    /// Buckets valued rewards by calendar year. Includes a count alongside the fiat
    /// total so the user can see how many rewards make up each year's income figure
    /// — useful when a year looks anomalously low and you want to know if it's
    /// "few rewards" or "many cheap rewards".
    /// </summary>
    public IReadOnlyList<YearlyRewardsPoint> ComputeRewardsByYear(IEnumerable<RewardValuation> valuations)
    {
        return valuations
            .Where(v => v.HasPrice)
            .GroupBy(v => v.Reward.TimestampUtc.UtcDateTime.Year)
            .Select(g => new YearlyRewardsPoint(g.Key, g.Sum(v => v.Value!.Value.Amount), g.Count()))
            .OrderBy(p => p.Year)
            .ToList();
    }

    /// <summary>
    /// Summarises holdings per token symbol: total quantity, transaction count, total
    /// priced fiat value (null if none priced) and how many transactions are unpriced.
    /// Ordered by fiat value so the tokens that actually move the portfolio come first;
    /// worthless spam-airdrop tokens (no price) sink to the bottom.
    /// </summary>
    public IReadOnlyList<TokenBreakdownRow> ComputeTokenBreakdown(
        IEnumerable<RewardValuation> valuations, string currency)
    {
        return valuations
            .GroupBy(v => v.Reward.TokenSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var priced = g.Where(v => v.HasPrice).ToList();
                decimal? fiat = priced.Count > 0 ? priced.Sum(v => v.Value!.Value.Amount) : null;
                return new TokenBreakdownRow(
                    TokenSymbol: g.Key,
                    Quantity: g.Sum(v => v.Reward.Amount),
                    Count: g.Count(),
                    FiatValue: fiat,
                    MissingPriceCount: g.Count(v => !v.HasPrice),
                    Currency: currency);
            })
            .OrderByDescending(r => r.FiatValue ?? -1m)
            .ThenByDescending(r => r.Quantity)
            .ToList();
    }

    /// <summary>
    /// Buckets rewards by (month, token symbol), summing the native token quantity.
    /// No price/FX involved — this is "how many tokens did I receive in this month?",
    /// kept separate per symbol because mixing units (GEOD + ETH) makes no sense.
    /// </summary>
    public IReadOnlyList<TokensPerMonthPoint> ComputeTokensByMonth(IEnumerable<RewardTransaction> rewards)
    {
        return rewards
            .GroupBy(r =>
            {
                var d = r.TimestampUtc.UtcDateTime;
                return (Month: new DateOnly(d.Year, d.Month, 1), Token: r.TokenSymbol);
            })
            .Select(g => new TokensPerMonthPoint(g.Key.Month, g.Key.Token, g.Sum(r => r.Amount)))
            .OrderBy(p => p.Month).ThenBy(p => p.TokenSymbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
