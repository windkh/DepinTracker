namespace DepinTracker.Infrastructure.Persistence.Maintenance;

using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;

/// <summary>
/// One-shot data fix for rewards imported by older builds that lacked fields we now
/// rely on: <c>TokenContract</c> (Blockscout v2 emits <c>address_hash</c>, not
/// <c>address</c>) and <c>FromAddress</c> (added so per-project source-address
/// allow-lists can be applied retroactively). Walks each affected row, parses the
/// saved <c>raw_provider_responses</c> payload, and patches in the missing values
/// by matching <c>transaction_hash</c>. Idempotent: re-running is a no-op once every
/// reward is fully populated or its raw response yields no candidates.
/// </summary>
public sealed class TokenContractBackfiller
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly ILogger<TokenContractBackfiller> _logger;

    public TokenContractBackfiller(ISqliteConnectionFactory factory, ILogger<TokenContractBackfiller> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);

        var candidates = (await connection.QueryAsync<(Guid Id, Guid? RawResponseId, string TxHash, string? TokenContract, string? FromAddress)>(new CommandDefinition(
            """
            SELECT Id, RawResponseId, TxHash, TokenContract, FromAddress
            FROM reward_transactions
            WHERE (TokenContract IS NULL OR FromAddress IS NULL)
              AND RawResponseId IS NOT NULL
              AND TxHash <> '';
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (candidates.Count == 0)
        {
            return 0;
        }

        var byRaw = candidates.GroupBy(c => c.RawResponseId!.Value).ToList();
        var totalUpdated = 0;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var group in byRaw)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var body = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT ResponseBody FROM raw_provider_responses WHERE Id = @id;",
                new { id = group.Key }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (string.IsNullOrEmpty(body))
            {
                continue;
            }

            var byTxHash = ExtractMap(body);
            if (byTxHash.Count == 0)
            {
                continue;
            }

            foreach (var row in group)
            {
                if (!byTxHash.TryGetValue(row.TxHash, out var parsed))
                {
                    continue;
                }

                // Only fill columns that are still NULL — never overwrite already-populated values.
                var contract = row.TokenContract ?? parsed.Contract;
                var from = row.FromAddress ?? parsed.From;
                if (contract == row.TokenContract && from == row.FromAddress)
                {
                    continue;
                }

                var rows = await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE reward_transactions
                       SET TokenContract = @contract,
                           FromAddress   = @from
                     WHERE Id = @id;
                    """,
                    new { contract, from, id = row.Id }, transaction, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                totalUpdated += rows;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (totalUpdated > 0)
        {
            _logger.LogInformation("Backfilled contract/from on {Count} previously-imported reward(s)", totalUpdated);
        }

        return totalUpdated;
    }

    private sealed record Parsed(string? Contract, string? From);

    /// <summary>
    /// Parses a stored Blockscout response (an array of page JSON objects produced by
    /// <c>BlockscoutEvmExplorer.FetchRewardsAsync</c>) into a tx-hash → (contract, from) map.
    /// </summary>
    private static Dictionary<string, Parsed> ExtractMap(string body)
    {
        var map = new Dictionary<string, Parsed>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // The persisted body is "[page1, page2, ...]"; older callers might have stored a single object.
            var pages = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : SingleElement(root);

            foreach (var page in pages)
            {
                if (!page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var tx = item.TryGetProperty("transaction_hash", out var th) && th.ValueKind == JsonValueKind.String
                        ? th.GetString()
                        : item.TryGetProperty("tx_hash", out var th2) && th2.ValueKind == JsonValueKind.String
                            ? th2.GetString()
                            : null;
                    if (string.IsNullOrEmpty(tx))
                    {
                        continue;
                    }

                    string? contract = null;
                    if (item.TryGetProperty("token", out var token))
                    {
                        contract = token.TryGetProperty("address_hash", out var ah) && ah.ValueKind == JsonValueKind.String
                            ? ah.GetString()
                            : token.TryGetProperty("address", out var ad) && ad.ValueKind == JsonValueKind.String
                                ? ad.GetString()
                                : null;
                    }

                    string? from = null;
                    if (item.TryGetProperty("from", out var fromEl) && fromEl.ValueKind == JsonValueKind.Object &&
                        fromEl.TryGetProperty("hash", out var fromHash) && fromHash.ValueKind == JsonValueKind.String)
                    {
                        from = fromHash.GetString()?.ToLowerInvariant();
                    }

                    if (contract is null && from is null)
                    {
                        continue;
                    }

                    if (!map.ContainsKey(tx))
                    {
                        map[tx] = new Parsed(contract, from);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Older or partial payloads — skip; backfill is best-effort.
        }

        return map;
    }

    private static IEnumerable<JsonElement> SingleElement(JsonElement element)
    {
        yield return element;
    }
}
