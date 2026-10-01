namespace DepinTracker.Infrastructure.Providers;

using System.Globalization;
using System.Numerics;
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
/// EVM explorer built on the Etherscan v2 unified API
/// (<c>https://api.etherscan.io/v2/api</c>). One API key serves every supported chain
/// via the <c>chainid</c> query parameter, replacing the previous Blockscout-based
/// explorer whose Polygon index had observable gaps. Pagination is offset-based
/// (<c>page</c> × <c>offset</c>) up to the documented 10 000-row ceiling.
/// </summary>
public sealed class EtherscanV2EvmExplorer : IBlockchainExplorer
{
    public const string Key = "etherscan-v2";

    /// <summary>Config key used by <see cref="IUserSettingsStore"/> to persist the API key.</summary>
    public const string ApiKeySettingName = "Etherscan.ApiKey";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProvidersOptions _options;
    private readonly IUserSettingsStore _userSettings;
    private readonly ILogger<EtherscanV2EvmExplorer> _logger;
    private readonly IReadOnlyList<Blockchain> _chains;

    public EtherscanV2EvmExplorer(
        IHttpClientFactory httpClientFactory,
        IOptions<ProvidersOptions> options,
        IUserSettingsStore userSettings,
        ILogger<EtherscanV2EvmExplorer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _userSettings = userSettings;
        _logger = logger;
        _chains = _options.EvmChains
            .Select(kv => new Blockchain
            {
                Key = kv.Key,
                Name = kv.Value.Name,
                ChainType = ChainType.Evm,
                NativeSymbol = kv.Value.NativeSymbol,
                EvmChainId = kv.Value.ChainId,
            })
            .ToList();
    }

    public string ProviderKey => Key;

    public IReadOnlyCollection<Blockchain> SupportedChains => _chains;

    public bool Supports(string blockchainKey) => _options.EvmChains.ContainsKey(blockchainKey);

    public async Task<ExplorerFetchResult> FetchRewardsAsync(
        string blockchainKey, string address, DateRange? range, CancellationToken cancellationToken)
    {
        if (!_options.EvmChains.TryGetValue(blockchainKey, out var chain))
        {
            throw new InvalidOperationException($"Etherscan v2 is not configured for chain '{blockchainKey}'.");
        }

        if (WalletAddressFormat.Validate(ChainType.Evm, address) is { } addressError)
        {
            throw new InvalidOperationException(addressError);
        }

        var apiKey = await _userSettings.GetAsync(ApiKeySettingName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Etherscan API key is not configured. Open Settings, paste a free key from " +
                "https://etherscan.io/apis, then click Save and retry the import.");
        }

        var client = _httpClientFactory.CreateClient(Key);
        var rewards = new List<ExplorerReward>();
        var rawPages = new StringBuilder("[");
        var basePath =
            $"{_options.EtherscanV2BaseUrl}" +
            $"?chainid={chain.ChainId}" +
            $"&module=account&action=tokentx" +
            $"&address={Uri.EscapeDataString(address)}" +
            $"&sort=desc" +
            $"&offset={_options.EtherscanV2PageSize}" +
            $"&apikey={Uri.EscapeDataString(apiKey)}";

        var firstRequest = $"{_options.EtherscanV2BaseUrl}?chainid={chain.ChainId}&module=account&action=tokentx&address={address}&sort=desc&offset={_options.EtherscanV2PageSize}";
        var maxPages = _options.EtherscanV2MaxPages;
        var truncated = false;
        var page = 1;
        var first = true;

        while (maxPages == 0 || page <= maxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"{basePath}&page={page}";

            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Etherscan returned {Status} for chain {Chain} page {Page}",
                    response.StatusCode, blockchainKey, page);
                throw new InvalidOperationException(
                    $"Etherscan returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) for {blockchainKey} page {page}.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!first)
            {
                rawPages.Append(',');
            }

            rawPages.Append(body);
            first = false;

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() : null;
            if (status == "0")
            {
                // "No transactions found" is the normal end-of-data signal. Anything else
                // ("NOTOK", rate limit, bad key, …) is an API error whose real reason is the
                // string in "result" — fail the import with it rather than report 0 rows.
                var msg = doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() : null;
                if (string.Equals(msg, "No transactions found", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var detail = doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString() : null;
                _logger.LogWarning(
                    "Etherscan reported '{Message}' ({Detail}) for chain {Chain} page {Page}",
                    msg, detail, blockchainKey, page);
                throw new InvalidOperationException(
                    $"Etherscan rejected the request for {blockchainKey}: {detail ?? msg ?? "unknown error"}");
            }

            if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var pageCount = 0;
            foreach (var item in result.EnumerateArray())
            {
                pageCount++;
                if (!TryParseTransfer(item, address, range, out var reward))
                {
                    continue;
                }

                rewards.Add(reward);
                _logger.LogInformation(
                    "Etherscan row · {Chain} · {Time:o} · {Token} {Amount} · from={From} · contract={Contract} · tx={TxHash} · block={Block}",
                    blockchainKey, reward.TimestampUtc, reward.TokenSymbol, reward.Amount,
                    reward.FromAddress ?? "<none>", reward.TokenContract ?? "<none>", reward.TxHash, reward.BlockNumber);
            }

            // Etherscan returns up to <offset> rows per page. A short page means we're done.
            if (pageCount < _options.EtherscanV2PageSize)
            {
                break;
            }

            page++;
            if (maxPages != 0 && page > maxPages)
            {
                truncated = true;
                _logger.LogWarning(
                    "Etherscan import for {Address} on {Chain} stopped at the {Max}-page cap; more data exists. " +
                    "Raise Providers.EtherscanV2MaxPages in appsettings.json (0 = unlimited) and re-import.",
                    address, blockchainKey, maxPages);
                break;
            }
        }

        rawPages.Append(']');
        return new ExplorerFetchResult(firstRequest, rawPages.ToString(), rewards, truncated);
    }

    private static bool TryParseTransfer(JsonElement item, string walletAddress, DateRange? range, out ExplorerReward reward)
    {
        reward = null!;

        // We requested all token transfers for the address (incoming + outgoing). Keep
        // only incoming ones — outgoing transfers are spends, not rewards.
        var to = item.TryGetProperty("to", out var toEl) && toEl.ValueKind == JsonValueKind.String
            ? toEl.GetString() : null;
        if (string.IsNullOrEmpty(to) || !string.Equals(to, walletAddress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!item.TryGetProperty("timeStamp", out var tsEl) || tsEl.ValueKind != JsonValueKind.String ||
            !long.TryParse(tsEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return false;
        }
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        if (range is { } r && !r.Contains(DateOnly.FromDateTime(timestamp.UtcDateTime)))
        {
            return false;
        }

        var txHash = item.TryGetProperty("hash", out var th) && th.ValueKind == JsonValueKind.String
            ? th.GetString() ?? string.Empty : string.Empty;
        var symbol = item.TryGetProperty("tokenSymbol", out var sym) && sym.ValueKind == JsonValueKind.String
            ? sym.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrEmpty(symbol))
        {
            return false;
        }

        var decimals = item.TryGetProperty("tokenDecimal", out var dec) && dec.ValueKind == JsonValueKind.String &&
            int.TryParse(dec.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 18;
        var contract = item.TryGetProperty("contractAddress", out var ca) && ca.ValueKind == JsonValueKind.String
            ? ca.GetString() : null;
        var from = item.TryGetProperty("from", out var fromEl) && fromEl.ValueKind == JsonValueKind.String
            ? fromEl.GetString()?.ToLowerInvariant() : null;
        long? blockNumber = item.TryGetProperty("blockNumber", out var bn) && bn.ValueKind == JsonValueKind.String &&
            long.TryParse(bn.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : null;

        if (!item.TryGetProperty("value", out var val) || val.ValueKind != JsonValueKind.String ||
            !TryScale(val.GetString()!, decimals, out var amount))
        {
            return false;
        }

        reward = new ExplorerReward(txHash, blockNumber, timestamp, symbol, contract, amount, RewardKind.Unknown, from);
        return true;
    }

    private static bool TryScale(string rawValue, int decimals, out decimal amount)
    {
        amount = 0m;
        if (!BigInteger.TryParse(rawValue, out var big))
        {
            return false;
        }

        try
        {
            var divisor = BigInteger.Pow(10, Math.Max(0, decimals));
            var whole = (decimal)(big / divisor);
            var remainder = (decimal)(big % divisor) / (decimal)divisor;
            amount = whole + remainder;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
