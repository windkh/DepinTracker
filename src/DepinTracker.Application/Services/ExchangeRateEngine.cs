namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves historical fiat exchange rates offline-first, mirroring
/// <see cref="PriceEngine"/>: identity rate for equal currencies, then cache, then
/// providers in priority order, writing successful lookups back to the cache.
/// </summary>
public sealed class ExchangeRateEngine
{
    private readonly IReadOnlyList<IExchangeRateProvider> _providers;
    private readonly IExchangeRateCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<ExchangeRateEngine> _logger;

    public ExchangeRateEngine(
        IEnumerable<IExchangeRateProvider> providers,
        IExchangeRateCache cache,
        IClock clock,
        ILogger<ExchangeRateEngine> logger)
    {
        _providers = providers.OrderBy(p => p.Priority).ToList();
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<decimal?> GetRateAsync(
        string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        if (string.Equals(baseCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var cached = await _cache.GetAsync(baseCurrency, quoteCurrency, date, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached.Rate;
        }

        foreach (var provider in _providers)
        {
            try
            {
                var rate = await provider
                    .GetRateAsync(baseCurrency, quoteCurrency, date, cancellationToken)
                    .ConfigureAwait(false);

                if (rate is { } value)
                {
                    _logger.LogInformation(
                        "FX · {Base}->{Quote} on {Date} = {Rate} via {Provider}",
                        baseCurrency, quoteCurrency, date, value, provider.ProviderKey);

                    await _cache.UpsertAsync(
                        new ExchangeRate
                        {
                            BaseCurrency = baseCurrency,
                            QuoteCurrency = quoteCurrency,
                            Date = date,
                            Rate = value,
                            ProviderKey = provider.ProviderKey,
                            RetrievedUtc = _clock.UtcNow,
                        },
                        cancellationToken).ConfigureAwait(false);

                    return value;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "FX provider {Provider} failed for {Base}->{Quote} on {Date}",
                    provider.ProviderKey, baseCurrency, quoteCurrency, date);
            }
        }

        _logger.LogInformation("No FX rate for {Base}->{Quote} on {Date}", baseCurrency, quoteCurrency, date);
        return null;
    }
}
