namespace DepinTracker.Infrastructure.Providers;

using System.Net;
using System.Text.Json;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Historical price provider backed by DeFiLlama's keyless coins API. Resolves
/// tokens by <c>{chain}:{contract}</c>, which is the natural identity for long-tail
/// DePIN tokens that CoinGecko does not (yet) index. Returns <c>null</c> when the
/// reward has no contract, when the requested currency is not USD (DeFiLlama only
/// quotes USD), or when the API misses — letting the price engine fall through to
/// CoinGecko / other providers.
/// </summary>
public sealed class DefiLlamaPriceProvider : IPriceProvider
{
    public const string Key = "defillama";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersOptions _options;
    private readonly ILogger<DefiLlamaPriceProvider> _logger;

    public DefiLlamaPriceProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersOptions> options,
        ILogger<DefiLlamaPriceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderKey => Key;

    /// <summary>Lower than CoinGecko (100) so contract-keyed lookups win when available.</summary>
    public int Priority => 50;

    public bool CanResolve(string tokenId, string? blockchainKey, string? tokenContract) =>
        !string.IsNullOrWhiteSpace(tokenContract) && !string.IsNullOrWhiteSpace(blockchainKey);

    public async Task<decimal?> GetHistoricalPriceAsync(
        string tokenId,
        string? blockchainKey,
        string? tokenContract,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenContract) || string.IsNullOrWhiteSpace(blockchainKey))
        {
            return null;
        }

        // DeFiLlama quotes everything in USD. If the caller wants something else, skip
        // and let a downstream provider handle the conversion (CoinGecko returns native
        // multi-currency).
        if (!string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var chain = _options.ChainKeyToDefiLlamaKey.TryGetValue(blockchainKey, out var mapped)
            ? mapped
            : blockchainKey.ToLowerInvariant();

        // Midnight UTC of the requested day; DeFiLlama's searchWidth window absorbs the offset.
        var unixSeconds = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var coinKey = $"{chain}:{tokenContract}";
        var url = $"{_options.DefiLlamaBaseUrl}/prices/historical/{unixSeconds}/{Uri.EscapeDataString(coinKey)}" +
                  $"?searchWidth={Uri.EscapeDataString(_options.DefiLlamaSearchWidth)}";

        var client = _httpClientFactory.CreateClient(Key);
        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("DeFiLlama rate-limited for {Coin} on {Date}", coinKey, date);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = await JsonDocument
                .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("coins", out var coins) &&
                coins.TryGetProperty(coinKey, out var entry) &&
                entry.TryGetProperty("price", out var priceElement) &&
                priceElement.ValueKind == JsonValueKind.Number)
            {
                return priceElement.GetDecimal();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DeFiLlama lookup failed for {Coin} on {Date}", coinKey, date);
        }

        return null;
    }
}
