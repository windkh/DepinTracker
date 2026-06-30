namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Dtos;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// Import page: enter a reward manually, or pull rewards on-chain for the selected
/// wallet via the explorer plugin that supports its chain. Both paths flow through
/// <see cref="RewardImportService"/>, which records provenance and de-duplicates.
/// </summary>
public sealed class ImportViewModel : ViewModelBase
{
    private readonly IWalletRepository _wallets;
    private readonly RewardImportService _import;
    private readonly Application.Configuration.AppSettings _appSettings;
    private readonly IProjectScope _scope;
    private Wallet? _selectedWallet;
    private string _tokenSymbol = string.Empty;
    private decimal _amount;
    private DateTime _date = DateTime.UtcNow.Date;
    private string _txHash = string.Empty;
    private RewardKind _selectedKind = RewardKind.Reward;

    // Disposal form state.
    private string _disposalTokenSymbol = string.Empty;
    private decimal _disposalAmount;
    private decimal _disposalProceedsPerUnit;
    private DateTime _disposalDate = DateTime.UtcNow.Date;
    private string _disposalTxHash = string.Empty;
    private string _disposalNotes = string.Empty;
    private DispositionKind _disposalKind = DispositionKind.Sale;

    public ImportViewModel(
        IWalletRepository wallets, RewardImportService import,
        Application.Configuration.AppSettings appSettings, IProjectScope scope) : base("Import")
    {
        _wallets = wallets;
        _import = import;
        _appSettings = appSettings;
        _scope = scope;
        ImportManualCommand = new AsyncRelayCommand(ImportManualAsync, CanImportManual, ShowError);
        ImportOnChainCommand = new AsyncRelayCommand(ImportOnChainAsync, () => SelectedWallet is not null, ShowError);
        ImportAllActiveCommand = new AsyncRelayCommand(ImportAllActiveAsync, () => Wallets.Any(w => w.IsActive), ShowError);
        AddDisposalCommand = new AsyncRelayCommand(AddDisposalAsync, CanAddDisposal, ShowError);

        _scope.ActiveProjectChanged += async (_, _) => await OnActivatedAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public ObservableCollection<Wallet> Wallets { get; } = new();

    public IReadOnlyList<RewardKind> Kinds { get; } = Enum.GetValues<RewardKind>();
    public IReadOnlyList<DispositionKind> DisposalKinds { get; } = Enum.GetValues<DispositionKind>();

    public string ProceedsCurrency => _appSettings.ReportingCurrency;

    public AsyncRelayCommand ImportManualCommand { get; }
    public AsyncRelayCommand ImportOnChainCommand { get; }
    public AsyncRelayCommand ImportAllActiveCommand { get; }
    public AsyncRelayCommand AddDisposalCommand { get; }

    public Wallet? SelectedWallet { get => _selectedWallet; set => SetProperty(ref _selectedWallet, value); }
    public string TokenSymbol { get => _tokenSymbol; set => SetProperty(ref _tokenSymbol, value); }
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public string TxHash { get => _txHash; set => SetProperty(ref _txHash, value); }
    public RewardKind SelectedKind { get => _selectedKind; set => SetProperty(ref _selectedKind, value); }

    public string DisposalTokenSymbol { get => _disposalTokenSymbol; set => SetProperty(ref _disposalTokenSymbol, value); }
    public decimal DisposalAmount { get => _disposalAmount; set => SetProperty(ref _disposalAmount, value); }
    public decimal DisposalProceedsPerUnit { get => _disposalProceedsPerUnit; set => SetProperty(ref _disposalProceedsPerUnit, value); }
    public DateTime DisposalDate { get => _disposalDate; set => SetProperty(ref _disposalDate, value); }
    public string DisposalTxHash { get => _disposalTxHash; set => SetProperty(ref _disposalTxHash, value); }
    public string DisposalNotes { get => _disposalNotes; set => SetProperty(ref _disposalNotes, value); }
    public DispositionKind SelectedDisposalKind { get => _disposalKind; set => SetProperty(ref _disposalKind, value); }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        var all = await _wallets.GetAllAsync(cancellationToken).ConfigureAwait(true);
        var scoped = _scope.ActiveProjectId is { } pid
            ? all.Where(w => w.ProjectId == pid).ToList()
            : (IReadOnlyList<Wallet>)all;

        Wallets.Clear();
        foreach (var wallet in scoped)
        {
            Wallets.Add(wallet);
        }

        // Re-select if the previous choice still belongs to the current scope.
        if (SelectedWallet is null || Wallets.All(w => w.Id != SelectedWallet.Id))
        {
            SelectedWallet = Wallets.FirstOrDefault();
        }

        StatusMessage = Wallets.Count == 0
            ? (_scope.ActiveProjectId is null ? "Add a wallet first (Projects page)." : $"No wallets in '{_scope.ActiveProjectName}'.")
            : $"{Wallets.Count} wallet(s) in scope '{_scope.ActiveProjectName}'.";
    }

    private bool CanImportManual() =>
        SelectedWallet is not null && !string.IsNullOrWhiteSpace(TokenSymbol) && Amount > 0;

    private async Task ImportManualAsync()
    {
        var result = await _import.ImportManualAsync(
            SelectedWallet!.Id, TokenSymbol, Amount,
            new DateTimeOffset(DateTime.SpecifyKind(Date, DateTimeKind.Utc)),
            string.IsNullOrWhiteSpace(TxHash) ? null : TxHash,
            SelectedKind, CancellationToken.None).ConfigureAwait(true);
        Report(result);
        TxHash = string.Empty;
    }

    private async Task ImportOnChainAsync()
    {
        StatusMessage = "Fetching on-chain rewards…";
        var result = await _import.ImportFromChainAsync(SelectedWallet!.Id, range: null, CancellationToken.None)
            .ConfigureAwait(true);
        Report(result);
    }

    private async Task ImportAllActiveAsync()
    {
        var scopeLabel = _scope.ActiveProjectId is null ? "every active wallet" : $"active wallets in '{_scope.ActiveProjectName}'";
        StatusMessage = $"Fetching on-chain rewards for {scopeLabel}…";
        var result = await _import.ImportAllActiveAsync(_scope.ActiveProjectId, range: null, CancellationToken.None)
            .ConfigureAwait(true);
        Report(result);
    }

    private bool CanAddDisposal() => !string.IsNullOrWhiteSpace(DisposalTokenSymbol) && DisposalAmount > 0;

    private async Task AddDisposalAsync()
    {
        var result = await _import.AddManualDispositionAsync(
            walletId: SelectedWallet?.Id,
            tokenSymbol: DisposalTokenSymbol,
            amount: DisposalAmount,
            timestampUtc: new DateTimeOffset(DateTime.SpecifyKind(DisposalDate, DateTimeKind.Utc)),
            proceedsPerUnit: DisposalProceedsPerUnit > 0m ? DisposalProceedsPerUnit : null,
            proceedsCurrency: DisposalProceedsPerUnit > 0m ? _appSettings.ReportingCurrency : null,
            txHash: string.IsNullOrWhiteSpace(DisposalTxHash) ? null : DisposalTxHash,
            kind: SelectedDisposalKind,
            notes: DisposalNotes,
            CancellationToken.None).ConfigureAwait(true);

        StatusMessage = result.Success
            ? (result.Imported == 0
                ? $"Disposal already recorded (deduped); skipped {result.Skipped}."
                : $"Recorded disposal: {DisposalAmount} {DisposalTokenSymbol} ({SelectedDisposalKind}).")
            : $"Disposal entry failed: {result.Message}";

        if (result.Success && result.Imported > 0)
        {
            // Reset only the values you'd want different next time; keep token/kind
            // since a user often enters multiple disposals of the same kind back-to-back.
            DisposalAmount = 0m;
            DisposalProceedsPerUnit = 0m;
            DisposalTxHash = string.Empty;
            DisposalNotes = string.Empty;
        }
    }

    private void Report(ImportResult result) =>
        StatusMessage = result.Success
            ? $"Imported {result.Imported}, skipped {result.Skipped}."
            : $"Import failed: {result.Message}";

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";
}
