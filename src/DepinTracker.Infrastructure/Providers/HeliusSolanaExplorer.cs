namespace DepinTracker.Infrastructure.Providers;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;
using DepinTracker.Domain.ValueObjects;
using DepinTracker.Plugins.Abstractions;
using DepinTracker.Plugins.Abstractions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Solana explorer built on Helius's enhanced-transactions API
/// (<c>api.helius.xyz/v0/addresses/{addr}/transactions</c>). Helius's free tier
/// covers personal-scale tax tracking (~100k credits/month), the response carries
/// parsed SPL transfers per transaction (no need to walk per-instruction), and
/// pagination is a simple <c>before={signature}</c> cursor. Symbols are resolved
/// in a single batch POST to <c>/v0/token-metadata</c> at the end so the UI shows
/// human-readable names instead of mint hashes.
/// </summary>
public sealed class HeliusSolanaExplorer : IBlockchainExplorer
{
    public const string Key = "helius";

    /// <summary>Config key used by <see cref="IUserSettingsStore"/> for the API key.</summary>
    public const string ApiKeySettingName = "Helius.ApiKey";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersOptions _options;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILogger<HeliusSolanaExplorer> _logger;
    private readonly IReadOnlyList<Blockchain> _chains;

    public HeliusSolanaExplorer(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersOptions> options,
        IUserSettingsStore userSettings,
        ILogger<HeliusSolanaExplorer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _userSettings = userSettings;
        _logger = logger;
        _chains = _options.SolanaChains
            .Select(kv => new Blockchain
            {
                Key = kv.Key,
                Name = kv.Value.Name,
                ChainType = ChainType.Solana,
                NativeSymbol = kv.Value.NativeSymbol,
            })
            .ToList();
    }

    public string ProviderKey => Key;

    public IReadOnlyCollection<Blockchain> SupportedChains => _chains;

    public bool Supports(string blockchainKey) => _options.SolanaChains.ContainsKey(blockchainKey);

    public async Task<ExplorerFetchResult> FetchRewardsAsync(
        string blockchainKey, string address, DateRange? range, CancellationToken cancellationToken)
    {
        if (!_options.SolanaChains.ContainsKey(blockchainKey))
        {
            throw new InvalidOperationException($"Helius is not configured for chain '{blockchainKey}'.");
        }

        var apiKey = await _userSettings.GetAsync(ApiKeySettingName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Helius API key is not configured. Open Settings, paste a free key from " +
                "https://www.helius.dev (dashboard → API keys), then click Save and retry the import.");
        }

        var client = _httpClientFactory.CreateClient(Key);
        var rewards = new List<ExplorerReward>();
        var rawPages = new StringBuilder("[");
        var firstRequest = $"{_options.HeliusBaseUrl}/v0/addresses/{address}/transactions?limit={_options.HeliusPageSize}";
        var pageSize = _options.HeliusPageSize;
        var maxPages = _options.HeliusMaxPages;
        var truncated = false;
        string? beforeSig = null;
        var mints = new HashSet<string>(StringComparer.Ordinal);
        var first = true;

        for (var page = 1; maxPages == 0 || page <= maxPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"{_options.HeliusBaseUrl}/v0/addresses/{Uri.EscapeDataString(address)}/transactions" +
                      $"?api-key={Uri.EscapeDataString(apiKey)}" +
                      $"&limit={pageSize}" +
                      (beforeSig is null ? string.Empty : $"&before={Uri.EscapeDataString(beforeSig)}");

            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                var body401 = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Helius rejected API key ({Status}): {Body}", response.StatusCode, body401);
                throw new InvalidOperationException(
                    "Helius rejected the API key (HTTP " + (int)response.StatusCode + "). " +
                    "Open Settings, paste a current key from helius.dev and click Save.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Helius rate-limited on chain {Chain} page {Page}", blockchainKey, page);
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Helius returned {Status} on chain {Chain} page {Page}: {Body}",
                    response.StatusCode, blockchainKey, page, errBody);
                break;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!first)
            {
                rawPages.Append(',');
            }

            rawPages.Append(body);
            first = false;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var pageCount = 0;
            string? lastSignature = null;
            foreach (var tx in doc.RootElement.EnumerateArray())
            {
                pageCount++;
                lastSignature = TryGetString(tx, "signature");

                // Each Helius tx may carry multiple SPL transfers (e.g. a batch payout).
                // Only the ones whose recipient is the queried wallet are real incoming
                // rewards; ignore outgoing transfers in the same transaction.
                if (!tx.TryGetProperty("tokenTransfers", out var transfers) || transfers.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var timestamp = TryGetUnixSeconds(tx, "timestamp");
                if (timestamp is null)
                {
                    continue;
                }

                if (range is { } r && !r.Contains(DateOnly.FromDateTime(timestamp.Value.UtcDateTime)))
                {
                    continue;
                }

                long? slot = TryGetInt64(tx, "slot");

                foreach (var transfer in transfers.EnumerateArray())
                {
                    var to = TryGetString(transfer, "toUserAccount");
                    if (string.IsNullOrEmpty(to) || !string.Equals(to, address, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var mint = TryGetString(transfer, "mint");
                    var from = TryGetString(transfer, "fromUserAccount");
                    var amount = TryGetDecimal(transfer, "tokenAmount");
                    if (amount is null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(mint))
                    {
                        mints.Add(mint);
                    }

                    // Temporary symbol placeholder — replaced after the metadata batch lookup.
                    var symbol = mint is null
                        ? "?"
                        : mint[..Math.Min(6, mint.Length)];

                    rewards.Add(new ExplorerReward(
                        lastSignature ?? string.Empty, slot, timestamp.Value, symbol, mint, amount.Value, RewardKind.Unknown, from));
                }
            }

            if (pageCount < pageSize)
            {
                break;
            }

            if (lastSignature is null)
            {
                break; // Defensive: can't paginate without a cursor.
            }

            beforeSig = lastSignature;

            if (maxPages != 0 && page >= maxPages)
            {
                truncated = true;
                _logger.LogWarning(
                    "Helius import for {Address} on {Chain} stopped at the {Max}-page cap; more data exists. " +
                    "Raise Providers.HeliusMaxPages in appsettings.json (0 = unlimited) and re-import.",
                    address, blockchainKey, maxPages);
                break;
            }
        }

        rawPages.Append(']');

        // Enrich symbols in one batch — Helius's enhanced-transactions response does
        // not include token symbols, only mints. Failures here are non-fatal: the
        // mint-prefix placeholder is good enough for dedupe and DeFiLlama pricing.
        if (mints.Count > 0)
        {
            var symbolByMint = await ResolveSymbolsAsync(client, apiKey, mints, cancellationToken).ConfigureAwait(false);
            if (symbolByMint.Count > 0)
            {
                for (var i = 0; i < rewards.Count; i++)
                {
                    var r = rewards[i];
                    if (r.TokenContract is { } m && symbolByMint.TryGetValue(m, out var sym) && !string.IsNullOrWhiteSpace(sym))
                    {
                        rewards[i] = r with { TokenSymbol = sym };
                    }
                }
            }
        }

        // Per-row log AFTER symbol resolution so the displayed symbol matches what's persisted.
        foreach (var r in rewards)
        {
            _logger.LogInformation(
                "Helius row · {Chain} · {Time:o} · {Token} {Amount} · from={From} · mint={Mint} · sig={Sig}",
                blockchainKey, r.TimestampUtc, r.TokenSymbol, r.Amount,
                r.FromAddress ?? "<none>", r.TokenContract ?? "<none>", r.TxHash);
        }

        return new ExplorerFetchResult(firstRequest, rawPages.ToString(), rewards, truncated);
    }

    private async Task<Dictionary<string, string>> ResolveSymbolsAsync(
        HttpClient client, string apiKey, IReadOnlyCollection<string> mints, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var url = $"{_options.HeliusBaseUrl}/v0/token-metadata?api-key={Uri.EscapeDataString(apiKey)}";
            using var response = await client.PostAsJsonAsync(
                url, new { mintAccounts = mints.ToArray() }, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Helius token-metadata lookup returned {Status}; rows will use mint-prefix symbols.",
                    response.StatusCode);
                return result;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var mint = TryGetString(entry, "account");
                if (string.IsNullOrEmpty(mint))
                {
                    continue;
                }

                // Symbols can live in legacyMetadata.symbol, onChainMetadata.metadata.data.symbol,
                // or offChainMetadata.metadata.symbol depending on the token's metadata standard.
                var symbol =
                    TryGetString(entry.TryGet("legacyMetadata"), "symbol")
                    ?? TryGetString(entry.TryGet("onChainMetadata").TryGet("metadata").TryGet("data"), "symbol")
                    ?? TryGetString(entry.TryGet("offChainMetadata").TryGet("metadata"), "symbol");

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    result[mint] = symbol.Trim();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Helius token-metadata lookup failed; rows will use mint-prefix symbols.");
        }

        return result;
    }

    private static string? TryGetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() : null;

    private static long? TryGetInt64(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) ? n : null;
    }

    private static DateTimeOffset? TryGetUnixSeconds(JsonElement obj, string name)
    {
        var s = TryGetInt64(obj, name);
        return s is null ? null : DateTimeOffset.FromUnixTimeSeconds(s.Value);
    }

    private static decimal? TryGetDecimal(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d) ? d : null;
    }
}

internal static class JsonElementExtensions
{
    /// <summary>Safe property accessor that returns an undefined element instead of throwing.</summary>
    public static JsonElement TryGet(this JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) ? el : default;
}
