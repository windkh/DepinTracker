namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves historical token prices offline-first: the local cache is consulted
/// before any provider, and registered <see cref="IPriceProvider"/>s are tried in
/// priority order on a miss. A successful lookup is written back to the cache so
/// the database can be rebuilt from online sources but normally runs offline.
/// </summary>
public sealed class PriceEngine
{
    private readonly IReadOnlyList<IPriceProvider> _providers;
    private readonly IPriceCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<PriceEngine> _logger;

    public PriceEngine(
        IEnumerable<IPriceProvider> providers,
        IPriceCache cache,
        IClock clock,
        ILogger<PriceEngine> logger)
    {
        _providers = providers.OrderBy(p => p.Priority).ToList();
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<decimal?> GetPriceAsync(
        string tokenId,
        string? blockchainKey,
        string? tokenContract,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(tokenId, date, currency, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached.Price;
        }

        foreach (var provider in _providers)
        {
            if (!provider.CanResolve(tokenId, blockchainKey, tokenContract))
            {
                continue;
            }

            try
            {
                var price = await provider
                    .GetHistoricalPriceAsync(tokenId, blockchainKey, tokenContract, date, currency, cancellationToken)
                    .ConfigureAwait(false);

                if (price is { } value)
                {
                    _logger.LogInformation(
                        "Price · {Token} ({Chain}:{Contract}) on {Date} = {Value} {Currency} via {Provider}",
                        tokenId, blockchainKey ?? "<none>", tokenContract ?? "<none>", date, value, currency, provider.ProviderKey);

                    await _cache.UpsertAsync(
                        new PricePoint
                        {
                            TokenId = tokenId,
                            Date = date,
                            Currency = currency,
                            Price = value,
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
                    "Price provider {Provider} failed for {Token} on {Date}", provider.ProviderKey, tokenId, date);
            }
        }

        _logger.LogInformation("No price found for {Token} on {Date} in {Currency}", tokenId, date, currency);
        return null;
    }
}
