namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// Transactions page: a flat, date-sorted view of every imported reward across all
/// wallets, paired with its valuation. Rows detected as spam/scam airdrops are flagged
/// so they stand out while scrolling. Below the grid, a per-source-address summary
/// (count + cumulated value) lets the user copy an address into the project filter and
/// toggle which sources are shown in the grid above.
/// </summary>
public sealed class TransactionsViewModel : ViewModelBase
{
    private const string NoSourceKey = "(manual / no source)";

    private readonly IRewardRepository _rewards;
    private readonly IWalletRepository _wallets;
    private readonly ValuationService _valuation;
    private readonly AppSettings _settings;
    private readonly IProjectScope _scope;

    // Full, unfiltered set; Rows is the source-filtered view bound to the grid.
    private readonly List<TransactionRow> _allRows = new();

    public TransactionsViewModel(
        IRewardRepository rewards,
        IWalletRepository wallets,
        ValuationService valuation,
        AppSettings settings,
        IProjectScope scope)
        : base("Transactions")
    {
        _rewards = rewards;
        _wallets = wallets;
        _valuation = valuation;
        _settings = settings;
        _scope = scope;
        ReportingCurrency = settings.ReportingCurrency;
        RefreshCommand = new AsyncRelayCommand(
            () => OnActivatedAsync(CancellationToken.None),
            onError: ex => StatusMessage = $"Error: {ex.Message}");
        CheckAllSourcesCommand = new RelayCommand(() => SetAllSourcesChecked(true));
        UncheckSpamSourcesCommand = new RelayCommand(() => SetSpamSourcesChecked(false));

        _scope.ActiveProjectChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(false);
        _scope.DataChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CheckAllSourcesCommand { get; }
    public RelayCommand UncheckSpamSourcesCommand { get; }

    public string ReportingCurrency { get; }

    /// <summary>Source-filtered rows shown in the grid.</summary>
    public ObservableCollection<TransactionRow> Rows { get; } = new();

    /// <summary>Per-source-address summary shown below the grid.</summary>
    public ObservableCollection<SourceSummaryRow> Sources { get; } = new();

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        StatusMessage = "Loading transactions…";
        try
        {
            var rewards = await _rewards.GetAllAsync(cancellationToken).ConfigureAwait(true);
            var allWallets = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(true);
            var wallets = allWallets.ToDictionary(w => w.Id);

            // Constrain to the active scope's wallets when a project is selected.
            if (_scope.ActiveProjectId is { } pid)
            {
                var walletIds = allWallets.Where(w => w.ProjectId == pid).Select(w => w.Id).ToHashSet();
                rewards = rewards.Where(r => walletIds.Contains(r.WalletId)).ToList();
            }

            var valuations = await _valuation.ValueManyAsync(rewards, cancellationToken).ConfigureAwait(true);

            _allRows.Clear();
            foreach (var valuation in valuations.OrderByDescending(v => v.Reward.TimestampUtc))
            {
                wallets.TryGetValue(valuation.Reward.WalletId, out var wallet);
                _allRows.Add(new TransactionRow(valuation.Reward, wallet, valuation, _settings.ReportingCurrency));
            }

            BuildSourceSummary();
            ApplyFilter();

            var missing = _allRows.Count(r => !r.HasPrice);
            var spam = _allRows.Count(r => r.IsSpam);
            StatusMessage = $"{_allRows.Count} transaction(s), {spam} flagged spam, {missing} missing price(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildSourceSummary()
    {
        Sources.Clear();
        // Group by (source address, token): a single sender can drop several different
        // tokens (e.g. one scam sender airdropping six), and mixing their quantities/values
        // in one row would be meaningless.
        var groups = _allRows
            .GroupBy(r => (Address: SourceAddress(r), Token: r.TokenSymbol))
            .Select(g => new SourceSummaryRow(
                address: g.Key.Address,
                token: g.Key.Token,
                count: g.Count(),
                unpricedCount: g.Count(r => !r.HasPrice),
                totalValue: g.Where(r => r.FiatAmount is not null).Sum(r => r.FiatAmount!.Value),
                currency: ReportingCurrency,
                isSpam: g.Any(r => r.IsSpam),
                onCheckedChanged: ApplyFilter))
            // Sorted by token by default; cumulated value (then count) breaks ties within a token.
            .OrderBy(s => s.Token, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(s => s.TotalValue)
            .ThenByDescending(s => s.Count);

        foreach (var source in groups)
        {
            Sources.Add(source);
        }
    }

    private void ApplyFilter()
    {
        var allowed = Sources.Where(s => s.IsChecked).Select(s => s.FilterKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Rows.Clear();
        foreach (var row in _allRows)
        {
            if (allowed.Contains(SourceSummaryRow.MakeFilterKey(SourceAddress(row), row.TokenSymbol)))
            {
                Rows.Add(row);
            }
        }
    }

    private static string SourceAddress(TransactionRow row) =>
        string.IsNullOrWhiteSpace(row.FromAddress) ? NoSourceKey : row.FromAddress;

    private void SetAllSourcesChecked(bool value)
    {
        foreach (var source in Sources)
        {
            source.SetCheckedSilently(value);
        }

        ApplyFilter();
    }

    private void SetSpamSourcesChecked(bool value)
    {
        foreach (var source in Sources.Where(s => s.IsSpam))
        {
            source.SetCheckedSilently(value);
        }

        ApplyFilter();
    }
}

/// <summary>
/// One row of the per-source-address summary: how many transfers came from an address,
/// their cumulated priced value, whether it looks like a spam source, and a checkbox
/// that filters the grid above. The address can be copied straight into the project
/// allow-list filter.
/// </summary>
public sealed class SourceSummaryRow : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly Action _onCheckedChanged;
    private bool _isChecked = true;

    public SourceSummaryRow(
        string address, string token, int count, int unpricedCount, decimal totalValue,
        string currency, bool isSpam, Action onCheckedChanged)
    {
        Address = address;
        Token = token;
        Count = count;
        UnpricedCount = unpricedCount;
        TotalValue = totalValue;
        Currency = currency;
        IsSpam = isSpam;
        _onCheckedChanged = onCheckedChanged;
        CopyAddressCommand = new RelayCommand(CopyAddress);
    }

    public string Address { get; }
    public string Token { get; }

    /// <summary>Composite key identifying this (source, token) pair for grid filtering.</summary>
    public string FilterKey => MakeFilterKey(Address, Token);

    public static string MakeFilterKey(string address, string token) => $"{address}|{token}";

    public int Count { get; }
    public int UnpricedCount { get; }
    public decimal TotalValue { get; }
    public string Currency { get; }
    public bool IsSpam { get; }
    public RelayCommand CopyAddressCommand { get; }

    public string TotalDisplay => UnpricedCount == 0
        ? $"{TotalValue.ToString("N2", Inv)} {Currency}"
        : $"{TotalValue.ToString("N2", Inv)} {Currency} (+{UnpricedCount} unpriced)";

    public string SpamFlag => IsSpam ? "⚠ spam" : string.Empty;

    /// <summary>Checkbox state; toggling re-filters the grid above.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                _onCheckedChanged();
            }
        }
    }

    /// <summary>Sets the checkbox without triggering the filter (used for bulk operations).</summary>
    public void SetCheckedSilently(bool value)
    {
        if (SetProperty(ref _isChecked, value, nameof(IsChecked)))
        {
            // no per-row callback; caller re-filters once.
        }
    }

    private void CopyAddress()
    {
        try
        {
            Clipboard.SetText(Address);
        }
        catch
        {
            // Clipboard can be transiently locked by another process; ignore.
        }
    }
}

/// <summary>Flattened, view-friendly row shown by the transactions DataGrid.</summary>
public sealed class TransactionRow
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public TransactionRow(RewardTransaction reward, Wallet? wallet, DepinTracker.Application.Dtos.RewardValuation valuation, string reportingCurrency)
    {
        TimestampUtc = reward.TimestampUtc;
        Chain = reward.BlockchainKey;
        TokenSymbol = reward.TokenSymbol;
        Amount = reward.Amount;
        Kind = reward.Kind;
        TxHash = reward.TxHash;
        FromAddress = reward.FromAddress ?? string.Empty;
        Provider = reward.ProviderKey;
        WalletLabel = wallet?.Label ?? wallet?.Address ?? reward.WalletId.ToString("N");
        WalletAddress = wallet?.Address ?? string.Empty;
        HasPrice = valuation.HasPrice;
        FiatAmount = valuation.Value?.Amount;

        // A row is spam if it was marked at import (Kind) or the symbol still looks like spam
        // (so data imported before spam-marking existed is flagged immediately, no re-import).
        IsSpam = reward.Kind == RewardKind.Spam || SpamHeuristics.IsLikelySpam(reward.TokenSymbol);

        UnitPrice = valuation.UnitPrice is { } p
            ? $"{p.ToString("0.######", Inv)} {valuation.PriceCurrency}"
            : "—";
        FxRate = valuation.ExchangeRate is { } r
            ? r.ToString("0.######", Inv)
            : "—";

        FiatValue = valuation.Value is { } money
            ? money.Amount.ToString("0.##", Inv)
            : "no price";

        // Tooltip lets the user verify the EUR figure: amount × USD price × USD→EUR rate.
        if (valuation.Value is { } v)
        {
            Calculation =
                $"{reward.Amount.ToString("0.######", Inv)} {reward.TokenSymbol} × " +
                $"{valuation.UnitPrice!.Value.ToString("0.######", Inv)} {valuation.PriceCurrency} × " +
                $"{valuation.ExchangeRate!.Value.ToString("0.######", Inv)} ({valuation.PriceCurrency}→{v.Currency}) " +
                $"= {v.Amount.ToString("0.##", Inv)} {v.Currency}";
        }
        else if (valuation.UnitPrice is { } unitOnly)
        {
            Calculation =
                $"Price resolved ({unitOnly.ToString("0.######", Inv)} {valuation.PriceCurrency}) but no FX rate to " +
                $"{reportingCurrency} on that date.";
        }
        else
        {
            Calculation = $"No {valuation.PriceCurrency ?? "USD"} price available for {reward.TokenSymbol} on {DateOnly.FromDateTime(reward.TimestampUtc.UtcDateTime):yyyy-MM-dd}.";
        }
    }

    public DateTimeOffset TimestampUtc { get; }
    public string WalletLabel { get; }
    public string WalletAddress { get; }
    public string Chain { get; }
    public string TokenSymbol { get; }
    public decimal Amount { get; }
    public RewardKind Kind { get; }
    public string TxHash { get; }
    public string FromAddress { get; }
    public string Provider { get; }
    public bool HasPrice { get; }
    public bool IsSpam { get; }
    public string SpamFlag => IsSpam ? "⚠" : string.Empty;
    public decimal? FiatAmount { get; }
    public string FiatValue { get; }
    public string UnitPrice { get; }
    public string FxRate { get; }
    public string Calculation { get; }
}
