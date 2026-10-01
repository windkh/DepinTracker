namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;
using DepinTracker.Domain.ValueObjects;

/// <summary>
/// Projects page: create projects, add wallets to the selected project (choosing a
/// chain from the runtime plugin registry), toggle wallet activation, and edit or
/// delete projects and wallets. Imported financial rows are intentionally not touched
/// by delete here — the rebuild engine (future) is the auditable way to drop them.
/// </summary>
public sealed class ProjectsViewModel : ViewModelBase
{
    // Map of chain family → the user-settings key that must be populated for the
    // family to be usable. The Add-Wallet picker hides chains whose key is unset
    // (no point letting the user create a wallet they can't import from).
    private static readonly Dictionary<ChainType, string> RequiredKeyByChainType = new()
    {
        [ChainType.Evm] = "Etherscan.ApiKey",
        [ChainType.Solana] = "Helius.ApiKey",
    };

    private readonly ProjectService _projects;
    private readonly WalletService _wallets;
    private readonly RewardImportService _import;
    private readonly IProjectScope _scope;
    private readonly IBlockchainRegistry _chains;
    private readonly IUserSettingsStore _userSettings;
    private Project? _selectedProject;
    private Wallet? _selectedWallet;
    private string _newProjectName = string.Empty;
    private string _newWalletAddress = string.Empty;
    private string _newWalletLabel = string.Empty;
    private Blockchain? _selectedChain;
    private string _editWalletAddress = string.Empty;
    private string _editWalletLabel = string.Empty;
    private string _editWalletNotes = string.Empty;
    private string _allowedSourceAddresses = string.Empty;

    public ProjectsViewModel(
        ProjectService projects, WalletService wallets, RewardImportService import,
        IBlockchainRegistry chains, IProjectScope scope, IUserSettingsStore userSettings)
        : base("Projects")
    {
        _projects = projects;
        _wallets = wallets;
        _import = import;
        _scope = scope;
        _chains = chains;
        _userSettings = userSettings;
        AvailableChains = new ObservableCollection<Blockchain>();
        _selectedChain = AvailableChains.FirstOrDefault();

        AddProjectCommand = new AsyncRelayCommand(AddProjectAsync, CanAddProject, ShowError);
        DeleteProjectCommand = new AsyncRelayCommand(DeleteProjectAsync, () => SelectedProject is not null, ShowError);
        ClearProjectDataCommand = new AsyncRelayCommand(ClearProjectDataAsync, () => SelectedProject is not null, ShowError);
        SaveAllowedSourcesCommand = new AsyncRelayCommand(SaveAllowedSourcesAsync, () => SelectedProject is not null, ShowError);
        AddWalletCommand = new AsyncRelayCommand(AddWalletAsync, CanAddWallet, ShowError);
        ToggleWalletActiveCommand = new AsyncRelayCommand(ToggleWalletActiveAsync, () => SelectedWallet is not null, ShowError);
        SaveWalletCommand = new AsyncRelayCommand(SaveWalletAsync, () => SelectedWallet is not null && !string.IsNullOrWhiteSpace(EditWalletAddress), ShowError);
        DeleteWalletCommand = new AsyncRelayCommand(DeleteWalletAsync, () => SelectedWallet is not null, ShowError);
    }

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<Wallet> Wallets { get; } = new();
    public ObservableCollection<Blockchain> AvailableChains { get; }

    public AsyncRelayCommand AddProjectCommand { get; }
    public AsyncRelayCommand DeleteProjectCommand { get; }
    public AsyncRelayCommand ClearProjectDataCommand { get; }
    public AsyncRelayCommand SaveAllowedSourcesCommand { get; }
    public AsyncRelayCommand AddWalletCommand { get; }
    public AsyncRelayCommand ToggleWalletActiveCommand { get; }
    public AsyncRelayCommand SaveWalletCommand { get; }
    public AsyncRelayCommand DeleteWalletCommand { get; }

    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                AllowedSourceAddresses = value is null
                    ? string.Empty
                    : string.Join(Environment.NewLine, value.RewardSourceAddresses);
                _ = LoadWalletsAsync();
            }
        }
    }

    public Wallet? SelectedWallet
    {
        get => _selectedWallet;
        set
        {
            if (SetProperty(ref _selectedWallet, value))
            {
                // Mirror into the editor fields so text boxes show the current state.
                EditWalletAddress = value?.Address ?? string.Empty;
                EditWalletLabel = value?.Label ?? string.Empty;
                EditWalletNotes = value?.Notes ?? string.Empty;
            }
        }
    }

    public string NewProjectName { get => _newProjectName; set => SetProperty(ref _newProjectName, value); }
    public string NewWalletAddress { get => _newWalletAddress; set => SetProperty(ref _newWalletAddress, value); }
    public string NewWalletLabel { get => _newWalletLabel; set => SetProperty(ref _newWalletLabel, value); }
    public Blockchain? SelectedChain { get => _selectedChain; set => SetProperty(ref _selectedChain, value); }

    public string EditWalletAddress { get => _editWalletAddress; set => SetProperty(ref _editWalletAddress, value); }
    public string EditWalletLabel { get => _editWalletLabel; set => SetProperty(ref _editWalletLabel, value); }
    public string EditWalletNotes { get => _editWalletNotes; set => SetProperty(ref _editWalletNotes, value); }
    public string AllowedSourceAddresses { get => _allowedSourceAddresses; set => SetProperty(ref _allowedSourceAddresses, value); }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        await RefreshAvailableChainsAsync(cancellationToken).ConfigureAwait(true);
        await LoadProjectsAsync().ConfigureAwait(true);
    }

    private async Task RefreshAvailableChainsAsync(CancellationToken cancellationToken)
    {
        // Build the set of chain families whose required user-setting (API key) is set.
        var enabled = new HashSet<ChainType>();
        foreach (var (chainType, settingKey) in RequiredKeyByChainType)
        {
            var value = await _userSettings.GetAsync(settingKey, cancellationToken).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(value))
            {
                enabled.Add(chainType);
            }
        }

        // Anything not gated by an API-key requirement is always shown.
        var visible = _chains.All
            .Where(c => !RequiredKeyByChainType.ContainsKey(c.ChainType) || enabled.Contains(c.ChainType))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AvailableChains.Clear();
        foreach (var chain in visible)
        {
            AvailableChains.Add(chain);
        }

        // Re-select an existing choice if still usable, otherwise the first available.
        if (SelectedChain is null || AvailableChains.All(c => c.Key != SelectedChain.Key))
        {
            SelectedChain = AvailableChains.FirstOrDefault();
        }
    }

    private async Task LoadProjectsAsync()
    {
        var projects = await _projects.GetAllAsync(CancellationToken.None).ConfigureAwait(true);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        SelectedProject ??= Projects.FirstOrDefault();
        StatusMessage = $"{Projects.Count} project(s).";
    }

    private async Task LoadWalletsAsync()
    {
        Wallets.Clear();
        if (SelectedProject is null)
        {
            return;
        }

        var wallets = await _wallets.GetByProjectAsync(SelectedProject.Id, CancellationToken.None).ConfigureAwait(true);
        foreach (var wallet in wallets)
        {
            Wallets.Add(wallet);
        }
    }

    private bool CanAddProject() => !string.IsNullOrWhiteSpace(NewProjectName);

    private async Task AddProjectAsync()
    {
        var project = await _projects.CreateAsync(NewProjectName, null, null, CancellationToken.None).ConfigureAwait(true);
        NewProjectName = string.Empty;
        Projects.Add(project);
        SelectedProject = project;
        _scope.NotifyProjectListChanged();
        StatusMessage = $"Created project '{project.Name}'.";
    }

    private async Task ClearProjectDataAsync()
    {
        var project = SelectedProject!;
        var deleted = await _import.ClearForProjectAsync(project.Id, CancellationToken.None).ConfigureAwait(true);
        _scope.NotifyDataChanged();
        StatusMessage = deleted == 0
            ? $"No imported rewards to clear for '{project.Name}'."
            : $"Cleared {deleted} reward(s) for '{project.Name}'. Wallets kept.";
    }

    private async Task SaveAllowedSourcesAsync()
    {
        var project = SelectedProject!;
        var addresses = (AllowedSourceAddresses ?? string.Empty)
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        project.RewardSourceAddresses = addresses;
        await _projects.UpdateAsync(project, CancellationToken.None).ConfigureAwait(true);
        // The allowed-source list is also an income filter applied at display time, so the
        // dashboard/transactions must re-evaluate what counts as income right away.
        _scope.NotifyDataChanged();
        StatusMessage = addresses.Count == 0
            ? "Source-address filter cleared (project counts every incoming transfer as income)."
            : $"Saved {addresses.Count} allowed source address(es) for '{project.Name}'.";
    }

    private async Task DeleteProjectAsync()
    {
        var project = SelectedProject!;
        await _projects.DeleteAsync(project.Id, CancellationToken.None).ConfigureAwait(true);
        Projects.Remove(project);
        SelectedProject = Projects.FirstOrDefault();
        // Reload wallets for whatever's selected now (or clear).
        await LoadWalletsAsync().ConfigureAwait(true);
        _scope.NotifyProjectListChanged();
        StatusMessage = $"Deleted project '{project.Name}' and its wallets (imported rewards kept).";
    }

    private bool CanAddWallet() =>
        SelectedProject is not null && SelectedChain is not null && !string.IsNullOrWhiteSpace(NewWalletAddress);

    private async Task AddWalletAsync()
    {
        if (WalletAddressFormat.Validate(SelectedChain!.ChainType, NewWalletAddress) is { } addressError)
        {
            StatusMessage = addressError;
            return;
        }

        var wallet = await _wallets.CreateAsync(
            SelectedProject!.Id, SelectedChain.Key, NewWalletAddress.Trim(),
            string.IsNullOrWhiteSpace(NewWalletLabel) ? null : NewWalletLabel,
            CancellationToken.None).ConfigureAwait(true);
        NewWalletAddress = string.Empty;
        NewWalletLabel = string.Empty;
        Wallets.Add(wallet);
        StatusMessage = $"Added wallet on {wallet.BlockchainKey}.";
    }

    private async Task ToggleWalletActiveAsync()
    {
        var wallet = SelectedWallet!;
        await _wallets.SetActiveAsync(wallet.Id, !wallet.IsActive, CancellationToken.None).ConfigureAwait(true);
        await LoadWalletsAsync().ConfigureAwait(true);
    }

    private async Task SaveWalletAsync()
    {
        var wallet = SelectedWallet!;
        if (_chains.TryGet(wallet.BlockchainKey, out var chain) &&
            WalletAddressFormat.Validate(chain.ChainType, EditWalletAddress) is { } addressError)
        {
            StatusMessage = addressError;
            return;
        }

        wallet.Address = EditWalletAddress.Trim();
        wallet.Label = string.IsNullOrWhiteSpace(EditWalletLabel) ? null : EditWalletLabel.Trim();
        wallet.Notes = string.IsNullOrWhiteSpace(EditWalletNotes) ? null : EditWalletNotes;
        await _wallets.UpdateAsync(wallet, CancellationToken.None).ConfigureAwait(true);
        // The Wallet entity doesn't raise PropertyChanged, so refresh the DataGrid from the store.
        await LoadWalletsAsync().ConfigureAwait(true);
        StatusMessage = $"Saved wallet {wallet.Address}.";
    }

    private async Task DeleteWalletAsync()
    {
        var wallet = SelectedWallet!;
        await _wallets.DeleteAsync(wallet.Id, CancellationToken.None).ConfigureAwait(true);
        Wallets.Remove(wallet);
        SelectedWallet = null;
        StatusMessage = $"Deleted wallet {wallet.Address} (imported rewards kept).";
    }

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";
}
