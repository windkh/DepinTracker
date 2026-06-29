namespace DepinTracker.Plugins.SampleCsvPrice;

using System.Globalization;
using System.Reflection;
using DepinTracker.Plugins.Abstractions;

/// <summary>
/// Offline price provider that reads a <c>prices.csv</c> file located next to the
/// plugin or in the working directory. CSV columns: <c>tokenId,date,currency,price</c>
/// (date as yyyy-MM-dd). Higher priority than the online providers so locally curated
/// prices win when present — useful for fully offline / reproducible runs.
/// </summary>
public sealed class CsvPriceProvider : IPriceProvider
{
    private const string FileName = "prices.csv";

    private readonly Lazy<IReadOnlyDictionary<string, decimal>> _rows;

    public CsvPriceProvider() => _rows = new Lazy<IReadOnlyDictionary<string, decimal>>(Load);

    public string ProviderKey => "sample-csv";

    public int Priority => 10;

    public bool CanResolve(string tokenId, string? blockchainKey, string? tokenContract) => _rows.Value.Count > 0;

    public Task<decimal?> GetHistoricalPriceAsync(
        string tokenId,
        string? blockchainKey,
        string? tokenContract,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken)
    {
        var key = MakeKey(tokenId, date, currency);
        return Task.FromResult(_rows.Value.TryGetValue(key, out var price) ? price : (decimal?)null);
    }

    private static string MakeKey(string tokenId, DateOnly date, string currency) =>
        $"{tokenId.ToUpperInvariant()}|{date:yyyy-MM-dd}|{currency.ToUpperInvariant()}";

    private static IReadOnlyDictionary<string, decimal> Load()
    {
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var path = LocateFile();
        if (path is null)
        {
            return result;
        }

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 4 ||
                !DateOnly.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                !decimal.TryParse(parts[3].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                continue;
            }

            result[MakeKey(parts[0].Trim(), date, parts[2].Trim())] = price;
        }

        return result;
    }

    private static string? LocateFile()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var candidates = new[]
        {
            assemblyDir is null ? null : Path.Combine(assemblyDir, FileName),
            Path.Combine(Directory.GetCurrentDirectory(), FileName),
        };

        return candidates.FirstOrDefault(c => c is not null && File.Exists(c));
    }
}
