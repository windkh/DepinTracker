namespace DepinTracker.Application.Dtos;

using DepinTracker.Domain.Entities;
using DepinTracker.Domain.ValueObjects;

/// <summary>
/// A reward paired with its computed fiat value. <see cref="Value"/> is null when
/// no price could be resolved — surfaced as a "missing price" rather than zero so
/// the gap is visible and auditable instead of silently distorting totals.
/// </summary>
public sealed record RewardValuation(
    RewardTransaction Reward,
    Money? Value,
    decimal? UnitPrice,
    string? PriceCurrency,
    decimal? ExchangeRate)
{
    public bool HasPrice => Value is not null;
}
