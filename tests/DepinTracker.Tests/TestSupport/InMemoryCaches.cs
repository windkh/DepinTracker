namespace DepinTracker.Tests.TestSupport;

using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>In-memory <see cref="IPriceCache"/> for unit tests (no database needed).</summary>
public sealed class InMemoryPriceCache : IPriceCache
{
    private readonly Dictionary<string, PricePoint> _store = new();

    public Task<PricePoint?> GetAsync(string tokenId, DateOnly date, string currency, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault($"{tokenId}|{date:yyyy-MM-dd}|{currency}"));

    public Task UpsertAsync(PricePoint price, CancellationToken cancellationToken)
    {
        _store[$"{price.TokenId}|{price.Date:yyyy-MM-dd}|{price.Currency}"] = price;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory <see cref="IExchangeRateCache"/> for unit tests.</summary>
public sealed class InMemoryExchangeRateCache : IExchangeRateCache
{
    private readonly Dictionary<string, ExchangeRate> _store = new();

    public Task<ExchangeRate?> GetAsync(string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault($"{baseCurrency}|{quoteCurrency}|{date:yyyy-MM-dd}"));

    public Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken)
    {
        _store[$"{rate.BaseCurrency}|{rate.QuoteCurrency}|{rate.Date:yyyy-MM-dd}"] = rate;
        return Task.CompletedTask;
    }
}
