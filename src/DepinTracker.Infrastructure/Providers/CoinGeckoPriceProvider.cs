namespace DepinTracker.Infrastructure.Providers;

using System.Net;
using System.Text.Json;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Historical price provider backed by CoinGecko's public (keyless) API. Symbols are
/// mapped to CoinGecko coin ids via <see cref="ProvidersOptions.SymbolToCoinGeckoId"/>.
/// Network/parse failures return <c>null</c> (a miss) rather than throwing, so the
/// price engine can degrade gracefully and stay offline-tolerant.
/// </summary>
public sealed class CoinGeckoPriceProvider : IPriceProvider
{
    public const string Key = "coingecko";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersOptions _options;
    private readonly ILogger<CoinGeckoPriceProvider> _logger;

    public CoinGeckoPriceProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersOptions> options,
        ILogger<CoinGeckoPriceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderKey => Key;

    public int Priority => 100;

    public bool CanResolve(string tokenId, string? blockchainKey, string? tokenContract) =>
        !string.IsNullOrWhiteSpace(tokenId);

    public async Task<decimal?> GetHistoricalPriceAsync(
        string tokenId,
        string? blockchainKey,
        string? tokenContract,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken)
    {
        var coinId = ResolveCoinId(tokenId);
        var client = _httpClientFactory.CreateClient(Key);
        var url = $"{_options.CoinGeckoBaseUrl}/coins/{Uri.EscapeDataString(coinId)}/history" +
                  $"?date={date:dd-MM-yyyy}&localization=false";

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("CoinGecko rate-limited for {Coin} on {Date}", coinId, date);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = await JsonDocument
            .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (doc.RootElement.TryGetProperty("market_data", out var marketData) &&
            marketData.TryGetProperty("current_price", out var prices) &&
            prices.TryGetProperty(currency.ToLowerInvariant(), out var priceElement) &&
            priceElement.ValueKind == JsonValueKind.Number)
        {
            return priceElement.GetDecimal();
        }

        return null;
    }

    private string ResolveCoinId(string tokenId) =>
        _options.SymbolToCoinGeckoId.TryGetValue(tokenId, out var id) ? id : tokenId.ToLowerInvariant();
}
