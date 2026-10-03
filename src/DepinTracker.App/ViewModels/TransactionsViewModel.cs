namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Application.Dtos;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// Transactions page: every imported reward and recorded disposal in the active scope,
/// paired with its valuation, plus everything that changes that list. One "Import / Add"
/// menu pulls rewards on-chain (all active wallets or a single one) or opens the manual
/// reward / disposal form; the form is only visible while an entry is being added. Rows
/// written by the last import or entry are highlighted. Remove deletes the selected rows,
/// Clear wipes everything in the scope. Below the grid, a per-source-address summary
/// (count + cumulated value) lets the user copy an address into the project filter and
/// toggle which sources are shown in the grid above.
/// </summary>
public sealed class TransactionsViewModel : ViewModelBase
{
    private const string NoSourceKey = "(manual / no source)";

    private readonly IRewardRepository _rewards;
    private readonly IDispositionRepository _dispositions;
    private readonly IWalletRepository _wallets;
    private readonly ValuationService _valuation;
    private readonly RewardImportService _import;
    private readonly AppSettings _settings;
    private readonly IProjectScope _scope;
    private readonly IDialogService _dialogs;

    // Full, unfiltered set; Rows is the source-filtered view bound to the grid.
    private readonly List<TransactionRow> _allRows = new();

    // Sources the user unticked survive reloads (imports and removals reload the grid).
    private readonly HashSet<string> _hiddenSources = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<TransactionRow> _selectedRows = Array.Empty<TransactionRow>();

    // Rows created at or after this instant are highlighted as "new".
    private DateTimeOffset? _highlightSince;

    // True while this page imports, adds or removes; suppresses the reload the page's own
    // DataChanged notification would trigger, so the result message isn't overwritten.
    private bool _isWorking;

    // Manual entry form state.
    private EntryMode _entry;
    private string _entryError = string.Empty;
    private Wallet? _entryWallet;
    private string _tokenSymbol = string.Empty;
    private decimal _amount;
    private DateTime _date = DateTime.UtcNow.Date;
    private string _txHash = string.Empty;
    private RewardKind _selectedKind = RewardKind.Reward;
    private decimal _proceedsPerUnit;
    private DispositionKind _selectedDisposalKind = DispositionKind.Sale;
    private string _notes = string.Empty;

    public TransactionsViewModel(
        IRewardRepository rewards,
        IDispositionRepository dispositions,
        IWalletRepository wallets,
        ValuationService valuation,
        RewardImportService import,
        AppSettings settings,
        IProjectScope scope,
        IDialogService dialogs)
        : base("Transactions")
    {
        _rewards = rewards;
        _dispositions = dispositions;
        _wallets = wallets;
        _valuation = valuation;
        _import = import;
        _settings = settings;
        _scope = scope;
        _dialogs = dialogs;
        ReportingCurrency = settings.ReportingCurrency;

        RefreshCommand = new AsyncRelayCommand(() => OnActivatedAsync(CancellationToken.None), () => CanAct, ShowError);
        ImportAllActiveCommand = new AsyncRelayCommand(ImportAllActiveAsync, () => CanAct && Wallets.Any(w => w.IsActive), ShowError);
        AddRewardCommand = new RelayCommand(() => OpenEntry(EntryMode.Reward), () => CanAct && Wallets.Count > 0);
        AddDisposalCommand = new RelayCommand(() => OpenEntry(EntryMode.Disposal), () => CanAct);
        SaveEntryCommand = new AsyncRelayCommand(SaveEntryAsync, CanSaveEntry, ex => EntryError = ex.Message);
        CancelEntryCommand = new RelayCommand(CloseEntry, () => IsEntryOpen);
        RemoveSelectedCommand = new AsyncRelayCommand(RemoveSelectedAsync, () => CanAct && _selectedRows.Count > 0, ShowError);
        ClearAllCommand = new AsyncRelayCommand(ClearAllAsync, () => CanAct && _allRows.Count > 0, ShowError);
        CheckAllSourcesCommand = new RelayCommand(() => SetAllSourcesChecked(true));
        UncheckSpamSourcesCommand = new RelayCommand(() => SetSpamSourcesChecked(false));

        _scope.ActiveProjectChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(true);
        _scope.DataChanged += async (_, _) =>
        {
            if (!_isWorking)
            {
                await OnActivatedAsync(CancellationToken.None).ConfigureAwait(true);
            }
        };
    }

    private enum EntryMode
    {
        None,
        Reward,
        Disposal,
    }

    /// <summary>Raised after an import or entry so the view can scroll the first new row into view.</summary>
    public event EventHandler<TransactionRow>? NewRowsAdded;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ImportAllActiveCommand { get; }
    public RelayCommand AddRewardCommand { get; }
    public RelayCommand AddDisposalCommand { get; }
    public AsyncRelayCommand SaveEntryCommand { get; }
    public RelayCommand CancelEntryCommand { get; }
    public AsyncRelayCommand RemoveSelectedCommand { get; }
    public AsyncRelayCommand ClearAllCommand { get; }
    public RelayCommand CheckAllSourcesCommand { get; }
    public RelayCommand UncheckSpamSourcesCommand { get; }

    public string ReportingCurrency { get; }

    /// <summary>Source-filtered rows shown in the grid.</summary>
    public ObservableCollection<TransactionRow> Rows { get; } = new();

    /// <summary>Per-source-address summary shown below the grid.</summary>
    public ObservableCollection<SourceSummaryRow> Sources { get; } = new();

    /// <summary>Wallets in the active scope (manual entry picker).</summary>
    public ObservableCollection<Wallet> Wallets { get; } = new();

    /// <summary>One "Import this wallet" entry per wallet in scope, for the import submenu.</summary>
    public ObservableCollection<WalletImportItem> WalletImports { get; } = new();

    public IReadOnlyList<RewardKind> Kinds { get; } = Enum.GetValues<RewardKind>();
    public IReadOnlyList<DispositionKind> DisposalKinds { get; } = Enum.GetValues<DispositionKind>();

    /// <summary>False while an import / entry / removal runs or the entry form is open.</summary>
    public bool CanAct => !_isWorking && !IsEntryOpen;

    public bool IsEntryOpen => _entry != EntryMode.None;
    public bool IsRewardEntry => _entry == EntryMode.Reward;
    public bool IsDisposalEntry => _entry == EntryMode.Disposal;
    public string EntryTitle => _entry == EntryMode.Disposal
        ? "Add disposal (sale / swap / transfer-out / spend / loss)"
        : "Add reward manually";
    public string SaveEntryLabel => _entry == EntryMode.Disposal ? "Add disposal" : "Add reward";
    public string EntryError { get => _entryError; private set => SetProperty(ref _entryError, value); }

    public Wallet? EntryWallet { get => _entryWallet; set => SetProperty(ref _entryWallet, value); }
    public string TokenSymbol { get => _tokenSymbol; set => SetProperty(ref _tokenSymbol, value); }
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public string TxHash { get => _txHash; set => SetProperty(ref _txHash, value); }
    public RewardKind SelectedKind { get => _selectedKind; set => SetProperty(ref _selectedKind, value); }
    public decimal ProceedsPerUnit { get => _proceedsPerUnit; set => SetProperty(ref _proceedsPerUnit, value); }
    public DispositionKind SelectedDisposalKind { get => _selectedDisposalKind; set => SetProperty(ref _selectedDisposalKind, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public bool HasNoRows => _allRows.Count == 0;
    public string EmptyHint => Wallets.Count == 0
        ? "No wallets in this scope yet.\nAdd a project and its wallets on the Projects page first."
        : "No transactions yet.\nUse 'Import / Add ▾' to pull rewards on-chain or enter one manually.";

    public string RemoveLabel => _selectedRows.Count > 1 ? $"Remove {_selectedRows.Count} selected" : "Remove selected";

    /// <summary>Called by the view when the grid selection changes (DataGrid.SelectedItems isn't bindable).</summary>
    public void SetSelection(IEnumerable<TransactionRow> rows)
    {
        _selectedRows = rows.ToList();
        OnPropertyChanged(nameof(RemoveLabel));
        CommandManager.InvalidateRequerySuggested();
    }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        StatusMessage = "Loading transactions…";
        try
        {
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = Summary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var rewards = await _rewards.GetAllAsync(cancellationToken).ConfigureAwait(true);
        var disposals = await _dispositions.GetAllAsync(cancellationToken).ConfigureAwait(true);
        var allWallets = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(true);
        var wallets = allWallets.ToDictionary(w => w.Id);

        // Constrain to the active scope's wallets when a project is selected. Disposals
        // without a wallet only show under "all projects".
        var scopedWallets = allWallets.ToList();
        if (_scope.ActiveProjectId is { } pid)
        {
            scopedWallets = allWallets.Where(w => w.ProjectId == pid).ToList();
            var walletIds = scopedWallets.Select(w => w.Id).ToHashSet();
            rewards = rewards.Where(r => walletIds.Contains(r.WalletId)).ToList();
            disposals = disposals.Where(d => d.WalletId is { } id && walletIds.Contains(id)).ToList();
        }

        RefreshWallets(scopedWallets);

        var valuations = await _valuation.ValueManyAsync(rewards, cancellationToken).ConfigureAwait(true);

        _allRows.Clear();
        foreach (var valuation in valuations)
        {
            wallets.TryGetValue(valuation.Reward.WalletId, out var wallet);
            _allRows.Add(new TransactionRow(valuation.Reward, wallet, valuation, _settings.ReportingCurrency, IsNew(valuation.Reward.CreatedUtc)));
        }

        foreach (var disposal in disposals)
        {
            Wallet? wallet = null;
            if (disposal.WalletId is { } id)
            {
                wallets.TryGetValue(id, out wallet);
            }

            _allRows.Add(new TransactionRow(disposal, wallet, IsNew(disposal.CreatedUtc)));
        }

        _allRows.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));

        BuildSourceSummary();
        ApplyFilter();
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private bool IsNew(DateTimeOffset createdUtc) => _highlightSince is { } since && createdUtc >= since;

    private string Summary()
    {
        var disposals = _allRows.Count(r => r.IsDisposal);
        var missing = _allRows.Count(r => !r.IsDisposal && !r.HasPrice);
        var spam = _allRows.Count(r => r.IsSpam);
        return $"{_allRows.Count - disposals} reward(s), {disposals} disposal(s), {spam} flagged spam, {missing} missing price(s).";
    }

    private void RefreshWallets(IReadOnlyList<Wallet> scoped)
    {
        var previous = EntryWallet?.Id;
        Wallets.Clear();
        WalletImports.Clear();
        foreach (var wallet in scoped)
        {
            Wallets.Add(wallet);
            WalletImports.Add(new WalletImportItem(wallet, new AsyncRelayCommand(() => ImportWalletAsync(wallet), () => CanAct, ShowError)));
        }

        EntryWallet = Wallets.FirstOrDefault(w => w.Id == previous) ?? Wallets.FirstOrDefault();
    }

    // ---------------------------------------------------------------- import

    private Task ImportAllActiveAsync()
    {
        var scopeLabel = _scope.ActiveProjectId is null ? "every active wallet" : $"active wallets in '{_scope.ActiveProjectName}'";
        return RunAsync(
            $"Fetching on-chain rewards for {scopeLabel}…",
            async () => ImportMessage(await _import.ImportAllActiveAsync(_scope.ActiveProjectId, range: null, CancellationToken.None).ConfigureAwait(true)));
    }

    private Task ImportWalletAsync(Wallet wallet) =>
        RunAsync(
            $"Fetching on-chain rewards for {WalletImportItem.Describe(wallet)}…",
            async () => ImportMessage(await _import.ImportFromChainAsync(wallet.Id, range: null, CancellationToken.None).ConfigureAwait(true)));

    private static string ImportMessage(ImportResult result) =>
        result.Success
            ? $"Imported {result.Imported}, skipped {result.Skipped} duplicate(s)."
            : $"Import failed: {result.Message}";

    /// <summary>
    /// Runs a data-changing operation: locks the page, highlights rows it creates, reloads
    /// the grid, tells the other pages, and keeps the operation's own result in the status bar.
    /// </summary>
    private async Task RunAsync(string progress, Func<Task<string>> operation, DateTimeOffset? highlightSince = null)
    {
        _isWorking = true;
        IsBusy = true;
        StatusMessage = progress;
        OnPropertyChanged(nameof(CanAct));
        CommandManager.InvalidateRequerySuggested();
        try
        {
            _highlightSince = highlightSince ?? DateTimeOffset.UtcNow;
            var message = await operation().ConfigureAwait(true);
            await LoadAsync(CancellationToken.None).ConfigureAwait(true);
            _scope.NotifyDataChanged();

            var newRows = _allRows.Count(r => r.IsNew);
            StatusMessage = newRows > 0 ? $"{message} New rows are highlighted." : message;
            if (Rows.FirstOrDefault(r => r.IsNew) is { } first)
            {
                NewRowsAdded?.Invoke(this, first);
            }
        }
        finally
        {
            _isWorking = false;
            IsBusy = false;
            OnPropertyChanged(nameof(CanAct));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ---------------------------------------------------------- manual entry

    private void OpenEntry(EntryMode mode)
    {
        _entry = mode;
        EntryError = string.Empty;
        Amount = 0m;
        ProceedsPerUnit = 0m;
        TxHash = string.Empty;
        Notes = string.Empty;
        Date = DateTime.UtcNow.Date;
        EntryWallet ??= Wallets.FirstOrDefault();
        RaiseEntryStateChanged();
    }

    private void CloseEntry()
    {
        _entry = EntryMode.None;
        EntryError = string.Empty;
        RaiseEntryStateChanged();
    }

    private void RaiseEntryStateChanged()
    {
        OnPropertyChanged(nameof(IsEntryOpen));
        OnPropertyChanged(nameof(IsRewardEntry));
        OnPropertyChanged(nameof(IsDisposalEntry));
        OnPropertyChanged(nameof(EntryTitle));
        OnPropertyChanged(nameof(SaveEntryLabel));
        OnPropertyChanged(nameof(CanAct));
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanSaveEntry() =>
        IsEntryOpen && !_isWorking &&
        !string.IsNullOrWhiteSpace(TokenSymbol) && Amount > 0 &&
        (_entry != EntryMode.Reward || EntryWallet is not null);

    private async Task SaveEntryAsync()
    {
        EntryError = string.Empty;
        var startedUtc = DateTimeOffset.UtcNow;
        var timestamp = new DateTimeOffset(DateTime.SpecifyKind(Date, DateTimeKind.Utc));
        var txHash = string.IsNullOrWhiteSpace(TxHash) ? null : TxHash;

        ImportResult result;
        if (_entry == EntryMode.Reward)
        {
            result = await _import.ImportManualAsync(
                EntryWallet!.Id, TokenSymbol, Amount, timestamp, txHash, SelectedKind, CancellationToken.None).ConfigureAwait(true);
        }
        else
        {
            result = await _import.AddManualDispositionAsync(
                walletId: EntryWallet?.Id,
                tokenSymbol: TokenSymbol,
                amount: Amount,
                timestampUtc: timestamp,
                proceedsPerUnit: ProceedsPerUnit > 0m ? ProceedsPerUnit : null,
                proceedsCurrency: ProceedsPerUnit > 0m ? _settings.ReportingCurrency : null,
                txHash: txHash,
                kind: SelectedDisposalKind,
                notes: Notes,
                CancellationToken.None).ConfigureAwait(true);
        }

        if (!result.Success)
        {
            EntryError = result.Message ?? "The entry could not be saved.";
            return;
        }

        if (result.Imported == 0)
        {
            EntryError = "An identical entry already exists (same wallet, token, amount and transaction hash), so nothing was added.";
            return;
        }

        var what = _entry == EntryMode.Reward
            ? $"Added reward: {Amount.ToString(CultureInfo.InvariantCulture)} {TokenSymbol.Trim().ToUpperInvariant()}."
            : $"Added disposal: {Amount.ToString(CultureInfo.InvariantCulture)} {TokenSymbol.Trim().ToUpperInvariant()} ({SelectedDisposalKind}).";
        CloseEntry();
        await RunAsync("Saving…", () => Task.FromResult(what), startedUtc).ConfigureAwait(true);
    }

    // -------------------------------------------------------- remove / clear

    private async Task RemoveSelectedAsync()
    {
        var rows = _selectedRows.ToList();
        var onChain = rows.Count(r => !r.IsDisposal && r.Provider != "manual");
        var message = $"Remove {rows.Count} selected transaction(s)?";
        if (onChain > 0)
        {
            message += $"\n\n{onChain} of them came from an on-chain import and will come back the next time that wallet is imported. " +
                       "To keep unwanted transfers out for good, add the legitimate sender to the project's allowed source addresses.";
        }

        if (!_dialogs.Confirm("Remove transactions", message))
        {
            return;
        }

        await RunAsync("Removing…", async () =>
        {
            var removed = await _import.RemoveAsync(
                rows.Where(r => !r.IsDisposal).Select(r => r.Id),
                rows.Where(r => r.IsDisposal).Select(r => r.Id),
                CancellationToken.None).ConfigureAwait(true);
            return $"Removed {removed.Rewards} reward(s) and {removed.Disposals} disposal(s).";
        }).ConfigureAwait(true);
    }

    private async Task ClearAllAsync()
    {
        var scope = _scope.ActiveProjectId is null ? "all projects" : $"'{_scope.ActiveProjectName}'";
        var disposals = _allRows.Count(r => r.IsDisposal);
        if (!_dialogs.Confirm(
                "Clear transactions",
                $"Delete all {_allRows.Count - disposals} reward(s) and {disposals} disposal(s) in {scope}?\n\n" +
                "Projects and wallets are kept. On-chain rewards can be imported again; manual entries are gone for good."))
        {
            return;
        }

        await RunAsync("Clearing…", async () =>
        {
            var removed = await _import.ClearAsync(_scope.ActiveProjectId, CancellationToken.None).ConfigureAwait(true);
            return $"Cleared {removed.Rewards} reward(s) and {removed.Disposals} disposal(s) in {scope}.";
        }).ConfigureAwait(true);
    }

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";

    // --------------------------------------------------------- source filter

    private void BuildSourceSummary()
    {
        Sources.Clear();
        // Group by (source address, token): a single sender can drop several different
        // tokens (e.g. one scam sender airdropping six), and mixing their quantities/values
        // in one row would be meaningless. Disposals are outgoing, so they have no source.
        var groups = _allRows
            .Where(r => !r.IsDisposal)
            .GroupBy(r => (Address: SourceAddress(r), Token: r.TokenSymbol))
            .Select(g => new SourceSummaryRow(
                address: g.Key.Address,
                token: g.Key.Token,
                count: g.Count(),
                unpricedCount: g.Count(r => !r.HasPrice),
                totalValue: g.Where(r => r.FiatAmount is not null).Sum(r => r.FiatAmount!.Value),
                currency: ReportingCurrency,
                isSpam: g.Any(r => r.IsSpam),
                isChecked: !_hiddenSources.Contains(SourceSummaryRow.MakeFilterKey(g.Key.Address, g.Key.Token)),
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
        _hiddenSources.Clear();
        foreach (var source in Sources.Where(s => !s.IsChecked))
        {
            _hiddenSources.Add(source.FilterKey);
        }

        Rows.Clear();
        foreach (var row in _allRows)
        {
            if (row.IsDisposal || !_hiddenSources.Contains(SourceSummaryRow.MakeFilterKey(SourceAddress(row), row.TokenSymbol)))
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

/// <summary>An "import this wallet" entry in the Import / Add menu.</summary>
public sealed class WalletImportItem
{
    public WalletImportItem(Wallet wallet, ICommand command)
    {
        Header = Describe(wallet) + (wallet.IsActive ? string.Empty : " (inactive)");
        Command = command;
    }

    public string Header { get; }
    public ICommand Command { get; }

    public static string Describe(Wallet wallet)
    {
        var address = wallet.Address.Length > 14 ? $"{wallet.Address[..6]}…{wallet.Address[^4..]}" : wallet.Address;
        return string.IsNullOrWhiteSpace(wallet.Label)
            ? $"{wallet.BlockchainKey} · {address}"
            : $"{wallet.Label} ({wallet.BlockchainKey} · {address})";
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
    private bool _isChecked;

    public SourceSummaryRow(
        string address, string token, int count, int unpricedCount, decimal totalValue,
        string currency, bool isSpam, bool isChecked, Action onCheckedChanged)
    {
        Address = address;
        Token = token;
        Count = count;
        UnpricedCount = unpricedCount;
        TotalValue = totalValue;
        Currency = currency;
        IsSpam = isSpam;
        _isChecked = isChecked;
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

/// <summary>
/// Flattened, view-friendly row shown by the transactions DataGrid: either an imported
/// reward (income, valued at the market price) or a disposal (outgoing, shown with a
/// negative amount and valued at its recorded proceeds).
/// </summary>
public sealed class TransactionRow
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public TransactionRow(RewardTransaction reward, Wallet? wallet, RewardValuation valuation, string reportingCurrency, bool isNew)
    {
        Id = reward.Id;
        IsNew = isNew;
        TimestampUtc = reward.TimestampUtc;
        Chain = reward.BlockchainKey;
        TokenSymbol = reward.TokenSymbol;
        Amount = reward.Amount;
        Kind = reward.Kind.ToString();
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

    public TransactionRow(DispositionRecord disposal, Wallet? wallet, bool isNew)
    {
        Id = disposal.Id;
        IsDisposal = true;
        IsNew = isNew;
        TimestampUtc = disposal.TimestampUtc;
        Chain = disposal.BlockchainKey ?? string.Empty;
        TokenSymbol = disposal.TokenSymbol;
        Amount = -disposal.Amount;
        Kind = disposal.Kind.ToString();
        TxHash = disposal.TxHash;
        FromAddress = string.Empty;
        Provider = disposal.ProviderKey;
        WalletLabel = wallet?.Label ?? wallet?.Address ?? (disposal.WalletId is null ? "(no wallet)" : disposal.WalletId.Value.ToString("N"));
        WalletAddress = wallet?.Address ?? string.Empty;
        FxRate = "—";

        if (disposal.ProceedsPerUnit is { } proceeds)
        {
            HasPrice = true;
            FiatAmount = proceeds * disposal.Amount;
            UnitPrice = $"{proceeds.ToString("0.######", Inv)} {disposal.ProceedsCurrency}";
            FiatValue = FiatAmount.Value.ToString("0.##", Inv);
            Calculation = $"Proceeds: {disposal.Amount.ToString("0.######", Inv)} {disposal.TokenSymbol} × " +
                          $"{proceeds.ToString("0.######", Inv)} {disposal.ProceedsCurrency} = {FiatValue} {disposal.ProceedsCurrency}";
        }
        else
        {
            UnitPrice = "—";
            FiatValue = "no proceeds";
            Calculation = "No sale price recorded; the tax report treats this disposal as zero proceeds.";
        }

        if (!string.IsNullOrWhiteSpace(disposal.Notes))
        {
            Calculation += $"\nNotes: {disposal.Notes}";
        }
    }

    public Guid Id { get; }

    /// <summary>True for a disposal (sale / swap / transfer-out / …), false for a reward.</summary>
    public bool IsDisposal { get; }

    /// <summary>Written by the last import or manual entry on this page.</summary>
    public bool IsNew { get; }

    public DateTimeOffset TimestampUtc { get; }
    public string WalletLabel { get; }
    public string WalletAddress { get; }
    public string Chain { get; }
    public string TokenSymbol { get; }
    public decimal Amount { get; }
    public string Kind { get; }
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
