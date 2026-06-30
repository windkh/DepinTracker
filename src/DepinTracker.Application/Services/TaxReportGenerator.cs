namespace DepinTracker.Application.Services;

using System.Globalization;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Dtos;
using DepinTracker.Plugins.Abstractions.Models;

/// <summary>
/// Assembles a year-scoped tax report for the German Finanzamt. The output is a
/// format-neutral <see cref="ReportDocument"/> — render it through any
/// <c>IReportExporter</c> (HTML for print/PDF, CSV, XLSX). The document deliberately
/// omits wallet addresses and on-chain source addresses; the data the tax office
/// needs is income aggregated in EUR plus per-reward valuation context.
/// </summary>
public sealed class TaxReportGenerator
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private readonly IRewardRepository _rewards;
    private readonly IProjectRepository _projects;
    private readonly IWalletRepository _wallets;
    private readonly IDispositionRepository _dispositions;
    private readonly ValuationService _valuation;
    private readonly FifoMatcher _fifo;
    private readonly AppSettings _settings;

    public TaxReportGenerator(
        IRewardRepository rewards,
        IProjectRepository projects,
        IWalletRepository wallets,
        IDispositionRepository dispositions,
        ValuationService valuation,
        FifoMatcher fifo,
        AppSettings settings)
    {
        _rewards = rewards;
        _projects = projects;
        _wallets = wallets;
        _dispositions = dispositions;
        _valuation = valuation;
        _fifo = fifo;
        _settings = settings;
    }

    /// <summary>Returns the calendar years that have at least one imported reward, descending.</summary>
    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken)
    {
        var all = await _rewards.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Select(r => r.TimestampUtc.UtcDateTime.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }

    /// <summary>
    /// Builds the tax report for <paramref name="year"/>, optionally scoped to a single
    /// project's wallets. Includes summary + monthly + per-token tables; appends a detail
    /// table only if <paramref name="includeDetail"/> is true.
    /// </summary>
    public async Task<ReportDocument> BuildAsync(int year, Guid? projectId, bool includeDetail, CancellationToken cancellationToken)
    {
        var currency = _settings.ReportingCurrency;
        var allRewards = await _rewards.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var allDisposals = await _dispositions.GetAllAsync(cancellationToken).ConfigureAwait(false);

        HashSet<Guid>? walletIdSet = null;
        var projectLabel = "Alle Projekte";
        if (projectId is { } pid)
        {
            var project = await _projects.GetAsync(pid, cancellationToken).ConfigureAwait(false);
            projectLabel = project?.Name ?? "Unbekannt";
            var wallets = await _wallets.GetByProjectAsync(pid, cancellationToken).ConfigureAwait(false);
            walletIdSet = wallets.Select(w => w.Id).ToHashSet();
        }

        // FIFO needs the full reward history (across all years) so older lots can
        // back fresher disposals; the year/project filter only constrains what
        // appears in the report tables, not what cost-basis the matcher sees.
        var rewardsInScope = walletIdSet is null
            ? allRewards
            : allRewards.Where(r => walletIdSet.Contains(r.WalletId)).ToList();
        var disposalsInScope = walletIdSet is null
            ? allDisposals
            : allDisposals.Where(d => d.WalletId is not { } w || walletIdSet.Contains(w)).ToList();

        var rewardsInYear = rewardsInScope.Where(r => r.TimestampUtc.UtcDateTime.Year == year).ToList();
        var valuationsInYear = await _valuation.ValueManyAsync(rewardsInYear, cancellationToken).ConfigureAwait(false);
        var totalEur = valuationsInYear.Where(v => v.HasPrice).Sum(v => v.Value!.Value.Amount);
        var missing = valuationsInYear.Count(v => !v.HasPrice);

        // Cost basis comes from valuing every historical reward in scope, not just the
        // ones in the report year. The matcher then keys lots by reward id.
        var allValuations = await _valuation.ValueManyAsync(rewardsInScope, cancellationToken).ConfigureAwait(false);
        var valuationByReward = allValuations.ToDictionary(v => v.Reward.Id, v => v);
        var matches = _fifo.Match(rewardsInScope, valuationByReward, disposalsInScope);
        var matchesInYear = matches
            .Where(m => m.DisposedUtc.UtcDateTime.Year == year)
            .ToList();

        var priceCurrency = valuationsInYear.FirstOrDefault(v => v.PriceCurrency is not null)?.PriceCurrency
            ?? _settings.PriceCurrency;
        var monthly = BuildMonthlyTable(valuationsInYear, year, currency);
        var perToken = BuildPerTokenTable(valuationsInYear, currency);
        var tables = new List<ReportTable>
        {
            BuildSummaryTable(year, currency, valuationsInYear.Count, missing, totalEur),
            monthly,
            perToken,
            BuildMethodologyTable(currency, priceCurrency, projectLabel, projectId is not null),
        };

        // Only surface the disposals/FIFO section when there actually were disposals in
        // the year — empty section adds nothing for HODL-only users.
        if (matchesInYear.Count > 0)
        {
            tables.Add(BuildDisposalsTable(matchesInYear, currency));
        }

        if (includeDetail)
        {
            tables.Add(BuildDetailTable(valuationsInYear, currency));
        }

        var realisedGain = matchesInYear
            .Where(m => m.RealisedGain.HasValue)
            .Sum(m => m.RealisedGain!.Value);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Steuerjahr"] = year.ToString(CultureInfo.InvariantCulture),
            ["Projekt"] = projectLabel,
            ["Berichtswährung"] = currency,
            ["Erstellt (UTC)"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            ["Transaktionen"] = valuationsInYear.Count.ToString(CultureInfo.InvariantCulture),
            ["Ohne Marktpreis"] = missing.ToString(CultureInfo.InvariantCulture),
            ["Summe Rewards (EUR)"] = totalEur.ToString("N2", De),
            ["Veräußerungen"] = matchesInYear.Count.ToString(CultureInfo.InvariantCulture),
            ["Realisierter Gewinn (EUR)"] = realisedGain.ToString("N2", De),
            ["Hinweis"] =
                "Diese Aufstellung basiert auf historischen Marktpreisen und EZB-Devisenkursen. " +
                "Sie ersetzt keine Steuerberatung. Wallet-Adressen wurden zum Schutz der Privatsphäre nicht aufgeführt.",
        };

        return new ReportDocument(
            Title: $"Steuerliche Aufstellung DePIN-Rewards {year} — {projectLabel}",
            Tables: tables,
            Metadata: metadata);
    }

    private static ReportTable BuildSummaryTable(int year, string currency, int total, int missing, decimal sum) =>
        new(
            Title: "Zusammenfassung",
            Columns: new[] { "Kennzahl", "Wert" },
            Rows: new IReadOnlyList<string>[]
            {
                new[] { "Steuerjahr", year.ToString(CultureInfo.InvariantCulture) },
                new[] { "Anzahl Transaktionen", total.ToString(CultureInfo.InvariantCulture) },
                new[] { "Davon ohne Marktpreis", missing.ToString(CultureInfo.InvariantCulture) },
                new[] { $"Summe der Rewards ({currency})", sum.ToString("N2", De) },
            });

    private static ReportTable BuildMonthlyTable(IReadOnlyList<RewardValuation> valuations, int year, string currency)
    {
        // Always show all 12 months so a zero-month is visible rather than implied by omission.
        var rows = new List<IReadOnlyList<string>>();
        for (var month = 1; month <= 12; month++)
        {
            var inMonth = valuations
                .Where(v => v.Reward.TimestampUtc.UtcDateTime.Month == month)
                .ToList();
            var withPrice = inMonth.Where(v => v.HasPrice).ToList();
            var monthEur = withPrice.Sum(v => v.Value!.Value.Amount);
            rows.Add(new[]
            {
                $"{year:D4}-{month:D2}",
                inMonth.Count.ToString(CultureInfo.InvariantCulture),
                monthEur.ToString("N2", De),
            });
        }

        return new ReportTable(
            Title: "Monatsübersicht",
            Columns: new[] { "Monat", "Transaktionen", $"Wert ({currency})" },
            Rows: rows);
    }

    private static ReportTable BuildPerTokenTable(IReadOnlyList<RewardValuation> valuations, string currency)
    {
        var rows = valuations
            .GroupBy(v => v.Reward.TokenSymbol, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(v => v.HasPrice ? v.Value!.Value.Amount : 0))
            .Select(g =>
            {
                var quantity = g.Sum(v => v.Reward.Amount);
                var fiat = g.Where(v => v.HasPrice).Sum(v => v.Value!.Value.Amount);
                return (IReadOnlyList<string>)new[]
                {
                    g.Key,
                    quantity.ToString("0.########", De),
                    g.Count().ToString(CultureInfo.InvariantCulture),
                    fiat.ToString("N2", De),
                };
            })
            .ToList();

        return new ReportTable(
            Title: "Aufschlüsselung nach Token",
            Columns: new[] { "Token", "Menge", "Transaktionen", $"Wert ({currency})" },
            Rows: rows);
    }

    /// <summary>
    /// Self-documenting section explaining where the numbers come from and how
    /// they were computed. Helps the reader (Finanzamt, tax advisor, future you)
    /// verify the report without reaching into the codebase.
    /// </summary>
    private static ReportTable BuildMethodologyTable(string reportingCurrency, string priceCurrency, string projectLabel, bool isProjectScoped)
    {
        var scopeNote = isProjectScoped
            ? $"Nur Wallets, die dem Projekt „{projectLabel}\" zugeordnet sind."
            : "Alle in der Anwendung registrierten Wallets über alle Projekte hinweg.";

        return new ReportTable(
            Title: "Methodik & Quellen",
            Columns: new[] { "Punkt", "Beschreibung" },
            Rows: new IReadOnlyList<string>[]
            {
                new[]
                {
                    "Datenherkunft (On-Chain)",
                    "Etherscan v2 Unified API für EVM-Chains (Ethereum, Polygon, …) und Helius Enhanced-Transactions API für Solana. Jede importierte Transaktion wird mit Tx-Hash, Block, Provider und Importsitzung gespeichert, sodass jeder Wert nachvollziehbar bleibt.",
                },
                new[]
                {
                    "Preisquelle (Token → " + priceCurrency + ")",
                    "DeFiLlama (vorrangig, per Chain+Vertragsadresse aufgelöst) mit CoinGecko-Fallback per Symbol-ID-Mapping. Historische Preise zum jeweiligen Transaktionsdatum, keine aktuellen Spot-Preise.",
                },
                new[]
                {
                    "Devisenkurs (" + priceCurrency + " → " + reportingCurrency + ")",
                    "Frankfurter API (EZB-Referenzkurse). Bei nicht-Geschäftstagen wird der letzte vorherige Werktagskurs verwendet — entspricht der gängigen steuerlichen Praxis.",
                },
                new[]
                {
                    "Berechnungsformel",
                    "Wert (" + reportingCurrency + ") = Menge (Token) × Preis (" + priceCurrency + "/Token) × FX-Kurs (" + priceCurrency + " → " + reportingCurrency + "). Alle Berechnungen erfolgen mit dezimaler Genauigkeit (kein floating-point).",
                },
                new[]
                {
                    "Filterung der Quelltransaktionen",
                    "Pro Projekt kann eine Allow-Liste von Absender-Adressen konfiguriert werden. Nur Transfers von diesen Adressen zählen als Rewards. Bei leerer Liste werden alle eingehenden Token-Transfers berücksichtigt.",
                },
                new[]
                {
                    "Umfang dieses Berichts",
                    scopeNote,
                },
                new[]
                {
                    "Datenschutz",
                    "Wallet-Adressen und Absender-Adressen sind in diesem Bericht bewusst nicht aufgeführt. Die Tx-Hashes der Einzeltransaktionen genügen für die spätere on-chain Prüfung jeder Position.",
                },
                new[]
                {
                    "Reproduzierbarkeit",
                    "Die Rohdatenbank ist aus den persistierten Provider-Antworten (raw_provider_responses) jederzeit rekonstruierbar. Preise/Devisenkurse werden im lokalen Cache gehalten und auf Anforderung erneut von den Quellen geprüft.",
                },
                new[]
                {
                    "Steuerlicher Hinweis",
                    "Diese Aufstellung dient der Vorbereitung der Steuererklärung und ersetzt keine Steuerberatung. Die Einordnung (sonstige Einkünfte, gewerblich, etc.) ist individuell zu prüfen.",
                },
            });
    }

    /// <summary>
    /// Renders the FIFO matches as the "Veräußerungsgewinne" section. Each row is a
    /// single match (one lot → one disposal slice) with acquisition / disposal dates,
    /// quantity, cost-basis per unit, proceeds per unit, realised gain in the reporting
    /// currency, and a §23-EStG-style holding-period flag.
    /// </summary>
    private static ReportTable BuildDisposalsTable(IReadOnlyList<DisposalMatch> matches, string currency)
    {
        var rows = matches
            .OrderBy(m => m.DisposedUtc)
            .Select(m =>
            {
                var holdingDays = (int)Math.Floor((m.DisposedUtc - m.AcquiredUtc).TotalDays);
                var taxStatus = m.LongTerm ? "steuerfrei (≥1 Jahr)" : "steuerpflichtig (<1 Jahr)";
                return (IReadOnlyList<string>)new[]
                {
                    m.DisposedUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    m.AcquiredUtc.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    m.TokenSymbol,
                    m.Kind.ToString(),
                    m.Quantity.ToString("0.########", De),
                    m.CostBasisPerUnit?.ToString("0.######", De) ?? "—",
                    m.ProceedsPerUnit?.ToString("0.######", De) ?? "—",
                    m.RealisedGain?.ToString("N2", De) ?? "—",
                    holdingDays.ToString(CultureInfo.InvariantCulture),
                    taxStatus,
                };
            })
            .ToList();

        return new ReportTable(
            Title: "Veräußerungsgewinne (FIFO)",
            Columns: new[]
            {
                "Veräußerung",
                "Anschaffung",
                "Token",
                "Art",
                "Menge",
                "Anschaffungskosten/Einheit",
                "Erlös/Einheit",
                $"Gewinn ({currency})",
                "Haltedauer (Tage)",
                "Steuerstatus",
            },
            Rows: rows);
    }

    private static ReportTable BuildDetailTable(IReadOnlyList<RewardValuation> valuations, string currency)
    {
        var rows = valuations
            .OrderBy(v => v.Reward.TimestampUtc)
            .Select(v =>
            {
                var amount = v.Reward.Amount;
                var unit = v.UnitPrice;
                var fx = v.ExchangeRate;
                var eur = v.HasPrice ? v.Value!.Value.Amount : (decimal?)null;
                return (IReadOnlyList<string>)new[]
                {
                    v.Reward.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    v.Reward.TokenSymbol,
                    amount.ToString("0.########", De),
                    unit?.ToString("0.######", De) ?? "—",
                    fx?.ToString("0.######", De) ?? "—",
                    eur?.ToString("N2", De) ?? "—",
                };
            })
            .ToList();

        return new ReportTable(
            Title: "Einzeltransaktionen",
            Columns: new[]
            {
                "Datum (UTC)",
                "Token",
                "Menge",
                $"Preis ({(valuations.FirstOrDefault()?.PriceCurrency) ?? "USD"})",
                "FX-Kurs",
                $"Wert ({currency})",
            },
            Rows: rows);
    }
}
