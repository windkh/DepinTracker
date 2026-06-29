namespace DepinTracker.Domain.Entities;

/// <summary>
/// A historical fiat exchange rate for a date: 1 unit of <see cref="BaseCurrency"/>
/// equals <see cref="Rate"/> units of <see cref="QuoteCurrency"/>. Generated/cache
/// data, re-fetchable from an exchange-rate provider (e.g. ECB/Frankfurter).
/// </summary>
public sealed class ExchangeRate
{
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Rate { get; set; }

    public string ProviderKey { get; set; } = string.Empty;
    public DateTimeOffset RetrievedUtc { get; set; } = DateTimeOffset.UtcNow;
}
