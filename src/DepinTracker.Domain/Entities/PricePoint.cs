namespace DepinTracker.Domain.Entities;

/// <summary>
/// A historical unit price for a token on a given date in a given currency.
/// This is generated/cache data: it can always be re-fetched from a price
/// provider, so it lives in cache/generated storage rather than imported.db.
/// </summary>
public sealed class PricePoint
{
    /// <summary>Provider coin identifier or token symbol the price is keyed by.</summary>
    public string TokenId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    /// <summary>Quote currency of <see cref="Price"/>, e.g. <c>"USD"</c>.</summary>
    public string Currency { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string ProviderKey { get; set; } = string.Empty;
    public DateTimeOffset RetrievedUtc { get; set; } = DateTimeOffset.UtcNow;
}
