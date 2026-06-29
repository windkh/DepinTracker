namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// Transactions page: a flat, date-sorted view of every imported reward across all
/// wallets, paired with its valuation. Lets the user audit the raw data behind the
/// dashboard's "X rewards / Y missing prices" headline.
/// </summary>
public sealed class TransactionsViewModel : ViewModelBase
{
    private readonly IRewardRepository _rewards;
    private readonly IWalletRepository _wallets;
    private readonly ValuationService _valuation;
    private readonly AppSettings _settings;
    private readonly IProjectScope _scope;

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

        _scope.ActiveProjectChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public string ReportingCurrency { get; }

    public ObservableCollection<TransactionRow> Rows { get; } = new();

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

            Rows.Clear();
            // Descending so the most recent rewards land at the top — the typical audit view.
            foreach (var valuation in valuations.OrderByDescending(v => v.Reward.TimestampUtc))
            {
                wallets.TryGetValue(valuation.Reward.WalletId, out var wallet);
                Rows.Add(new TransactionRow(valuation.Reward, wallet, valuation, _settings.ReportingCurrency));
            }

            var missing = Rows.Count(r => !r.HasPrice);
            StatusMessage = $"{Rows.Count} transaction(s), {missing} missing price(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>Flattened, view-friendly row shown by the transactions DataGrid.</summary>
public sealed class TransactionRow
{
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    public TransactionRow(RewardTransaction reward, Wallet? wallet, Application.Dtos.RewardValuation valuation, string reportingCurrency)
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
    public string FiatValue { get; }
    public string UnitPrice { get; }
    public string FxRate { get; }
    public string Calculation { get; }
}
