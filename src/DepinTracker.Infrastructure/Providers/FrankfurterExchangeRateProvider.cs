namespace DepinTracker.Infrastructure.Providers;

using System.Text.Json;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Historical fiat exchange-rate provider backed by the Frankfurter API, which
/// republishes ECB reference rates (keyless). For non-business days the API returns
/// the most recent prior working day's rates. Failures return <c>null</c>.
/// </summary>
public sealed class FrankfurterExchangeRateProvider : IExchangeRateProvider
{
    public const string Key = "frankfurter";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersOptions _options;
    private readonly ILogger<FrankfurterExchangeRateProvider> _logger;

    public FrankfurterExchangeRateProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersOptions> options,
        ILogger<FrankfurterExchangeRateProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderKey => Key;

    public int Priority => 100;

    public async Task<decimal?> GetRateAsync(
        string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(Key);
        var url = $"{_options.FrankfurterBaseUrl}/{date:yyyy-MM-dd}" +
                  $"?from={baseCurrency.ToUpperInvariant()}&to={quoteCurrency.ToUpperInvariant()}";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = await JsonDocument
                .ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty(quoteCurrency.ToUpperInvariant(), out var rateElement) &&
                rateElement.ValueKind == JsonValueKind.Number)
            {
                return rateElement.GetDecimal();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Frankfurter lookup failed for {Base}->{Quote} on {Date}",
                baseCurrency, quoteCurrency, date);
        }

        return null;
    }
}
