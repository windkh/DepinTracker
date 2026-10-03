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
/// Projects page: add, edit and remove projects and the wallets of the selected project.
/// The page has two states per column: browsing (list + Add/Edit/Remove toolbar) and
/// editing (an inline form, shown only while adding or editing, that ends with Save or
/// Cancel). While either form is open the lists and toolbars are locked so the form can
/// never drift out of sync with the selection. Removing a project or wallet keeps its
/// imported financial rows — "Clear imported rewards" is the explicit way to drop them.
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
    private readonly IDialogService _dialogs;
    private Project? _selectedProject;
    private Wallet? _selectedWallet;

    // Project form state.
    private EditorMode _projectEditor;
    private string _projectName = string.Empty;
    private string _projectDescription = string.Empty;
    private string _allowedSourceAddresses = string.Empty;
    private string _projectEditorError = string.Empty;

    // Wallet form state.
    private EditorMode _walletEditor;
    private Blockchain? _selectedChain;
    private string _walletAddress = string.Empty;
    private string _walletLabel = string.Empty;
    private string _walletNotes = string.Empty;
    private string _walletEditorError = string.Empty;

    public ProjectsViewModel(
        ProjectService projects, WalletService wallets, RewardImportService import,
        IBlockchainRegistry chains, IProjectScope scope, IUserSettingsStore userSettings,
        IDialogService dialogs)
        : base("Projects")
    {
        _projects = projects;
        _wallets = wallets;
        _import = import;
        _scope = scope;
        _chains = chains;
        _userSettings = userSettings;
        _dialogs = dialogs;
        AvailableChains = new ObservableCollection<Blockchain>();

        AddProjectCommand = new RelayCommand(BeginAddProject, () => !IsEditing);
        EditProjectCommand = new RelayCommand(BeginEditProject, () => !IsEditing && SelectedProject is not null);
        RemoveProjectCommand = new AsyncRelayCommand(RemoveProjectAsync, () => !IsEditing && SelectedProject is not null, ShowError);
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync, () => IsProjectEditorOpen && !string.IsNullOrWhiteSpace(ProjectName), ShowProjectError);
        CancelProjectCommand = new RelayCommand(CloseProjectEditor, () => IsProjectEditorOpen);
        ClearProjectDataCommand = new AsyncRelayCommand(ClearProjectDataAsync, () => _projectEditor == EditorMode.Edit, ShowProjectError);

        AddWalletCommand = new RelayCommand(BeginAddWallet, () => !IsEditing && SelectedProject is not null);
        EditWalletCommand = new RelayCommand(BeginEditWallet, () => !IsEditing && SelectedWallet is not null);
        RemoveWalletCommand = new AsyncRelayCommand(RemoveWalletAsync, () => !IsEditing && SelectedWallet is not null, ShowError);
        ToggleWalletActiveCommand = new AsyncRelayCommand(ToggleWalletActiveAsync, () => !IsEditing && SelectedWallet is not null, ShowError);
        SaveWalletCommand = new AsyncRelayCommand(SaveWalletAsync, CanSaveWallet, ShowWalletError);
        CancelWalletCommand = new RelayCommand(CloseWalletEditor, () => IsWalletEditorOpen);
    }

    private enum EditorMode
    {
        None,
        Add,
        Edit,
    }

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<Wallet> Wallets { get; } = new();
    public ObservableCollection<Blockchain> AvailableChains { get; }

    public RelayCommand AddProjectCommand { get; }
    public RelayCommand EditProjectCommand { get; }
    public AsyncRelayCommand RemoveProjectCommand { get; }
    public AsyncRelayCommand SaveProjectCommand { get; }
    public RelayCommand CancelProjectCommand { get; }
    public AsyncRelayCommand ClearProjectDataCommand { get; }

    public RelayCommand AddWalletCommand { get; }
    public RelayCommand EditWalletCommand { get; }
    public AsyncRelayCommand RemoveWalletCommand { get; }
    public AsyncRelayCommand ToggleWalletActiveCommand { get; }
    public AsyncRelayCommand SaveWalletCommand { get; }
    public RelayCommand CancelWalletCommand { get; }

    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                OnPropertyChanged(nameof(WalletsHeader));
                _ = LoadWalletsAsync();
            }
        }
    }

    public Wallet? SelectedWallet { get => _selectedWallet; set => SetProperty(ref _selectedWallet, value); }

    /// <summary>True while either form is open; locks lists and toolbars.</summary>
    public bool IsEditing => IsProjectEditorOpen || IsWalletEditorOpen;
    public bool IsBrowsing => !IsEditing;

    public bool IsProjectEditorOpen => _projectEditor != EditorMode.None;
    public bool IsEditingExistingProject => _projectEditor == EditorMode.Edit;
    public string ProjectEditorTitle => _projectEditor == EditorMode.Add ? "New project" : $"Edit project '{SelectedProject?.Name}'";
    public string SaveProjectLabel => _projectEditor == EditorMode.Add ? "Create project" : "Save changes";

    public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }
    public string ProjectDescription { get => _projectDescription; set => SetProperty(ref _projectDescription, value); }
    public string AllowedSourceAddresses { get => _allowedSourceAddresses; set => SetProperty(ref _allowedSourceAddresses, value); }
    public string ProjectEditorError { get => _projectEditorError; private set => SetProperty(ref _projectEditorError, value); }

    public bool IsWalletEditorOpen => _walletEditor != EditorMode.None;
    public bool IsAddingWallet => _walletEditor == EditorMode.Add;
    public bool IsEditingExistingWallet => _walletEditor == EditorMode.Edit;
    public string WalletEditorTitle => _walletEditor == EditorMode.Add
        ? $"New wallet in '{SelectedProject?.Name}'"
        : $"Edit wallet on {SelectedWallet?.BlockchainKey}";
    public string SaveWalletLabel => _walletEditor == EditorMode.Add ? "Add wallet" : "Save changes";

    public Blockchain? SelectedChain { get => _selectedChain; set => SetProperty(ref _selectedChain, value); }
    public string WalletAddress { get => _walletAddress; set => SetProperty(ref _walletAddress, value); }
    public string WalletLabel { get => _walletLabel; set => SetProperty(ref _walletLabel, value); }
    public string WalletNotes { get => _walletNotes; set => SetProperty(ref _walletNotes, value); }
    public string WalletEditorError { get => _walletEditorError; private set => SetProperty(ref _walletEditorError, value); }

    public string WalletsHeader => SelectedProject is null ? "Wallets" : $"Wallets in '{SelectedProject.Name}'";
    public bool HasNoProjects => Projects.Count == 0;
    public bool HasNoWallets => SelectedProject is not null && Wallets.Count == 0;
    public bool ShowNoChainsHint => IsAddingWallet && AvailableChains.Count == 0;

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

        OnPropertyChanged(nameof(ShowNoChainsHint));
    }

    private async Task LoadProjectsAsync()
    {
        var previous = SelectedProject?.Id;
        var projects = await _projects.GetAllAsync(CancellationToken.None).ConfigureAwait(true);
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        OnPropertyChanged(nameof(HasNoProjects));
        SelectedProject = Projects.FirstOrDefault(p => p.Id == previous) ?? Projects.FirstOrDefault();
        StatusMessage = Projects.Count == 0
            ? "No projects yet. Click '+ Add project' to start."
            : $"{Projects.Count} project(s).";
    }

    private async Task LoadWalletsAsync()
    {
        var previous = SelectedWallet?.Id;
        Wallets.Clear();
        if (SelectedProject is not null)
        {
            var wallets = await _wallets.GetByProjectAsync(SelectedProject.Id, CancellationToken.None).ConfigureAwait(true);
            foreach (var wallet in wallets)
            {
                Wallets.Add(wallet);
            }
        }

        SelectedWallet = Wallets.FirstOrDefault(w => w.Id == previous);
        OnPropertyChanged(nameof(HasNoWallets));
    }

    // ---- Project form -------------------------------------------------------------

    private void BeginAddProject()
    {
        ProjectName = string.Empty;
        ProjectDescription = string.Empty;
        AllowedSourceAddresses = string.Empty;
        OpenProjectEditor(EditorMode.Add);
    }

    private void BeginEditProject()
    {
        var project = SelectedProject!;
        ProjectName = project.Name;
        ProjectDescription = project.Description ?? string.Empty;
        AllowedSourceAddresses = string.Join(Environment.NewLine, project.RewardSourceAddresses);
        OpenProjectEditor(EditorMode.Edit);
    }

    private async Task SaveProjectAsync()
    {
        var addresses = ParseAddressList(AllowedSourceAddresses);
        var description = string.IsNullOrWhiteSpace(ProjectDescription) ? null : ProjectDescription.Trim();

        if (_projectEditor == EditorMode.Add)
        {
            var project = await _projects.CreateAsync(ProjectName, description, null, CancellationToken.None).ConfigureAwait(true);
            if (addresses.Count > 0)
            {
                project.RewardSourceAddresses = addresses;
                await _projects.UpdateAsync(project, CancellationToken.None).ConfigureAwait(true);
            }

            Projects.Add(project);
            OnPropertyChanged(nameof(HasNoProjects));
            CloseProjectEditor();
            SelectedProject = project;
            _scope.NotifyProjectListChanged();
            StatusMessage = $"Created project '{project.Name}'. Next: click '+ Add wallet'.";
            return;
        }

        var existing = SelectedProject!;
        var nameChanged = !string.Equals(existing.Name, ProjectName.Trim(), StringComparison.Ordinal);
        existing.Name = ProjectName.Trim();
        existing.Description = description;
        existing.RewardSourceAddresses = addresses;
        await _projects.UpdateAsync(existing, CancellationToken.None).ConfigureAwait(true);
        CloseProjectEditor();

        // Project doesn't raise PropertyChanged; reload so the list shows a renamed project.
        await LoadProjectsAsync().ConfigureAwait(true);
        if (nameChanged)
        {
            _scope.NotifyProjectListChanged();
        }

        // The allowed-source list is also an income filter applied at display time, so the
        // dashboard/transactions must re-evaluate what counts as income right away.
        _scope.NotifyDataChanged();
        StatusMessage = $"Saved project '{existing.Name}'.";
    }

    private async Task RemoveProjectAsync()
    {
        var project = SelectedProject!;
        if (!_dialogs.Confirm(
                "Remove project",
                $"Remove project '{project.Name}' and its {Wallets.Count} wallet(s)?\n\n" +
                "Imported rewards are kept. Use Edit → 'Clear imported rewards' first if you want them gone too."))
        {
            return;
        }

        await _projects.DeleteAsync(project.Id, CancellationToken.None).ConfigureAwait(true);
        Projects.Remove(project);
        OnPropertyChanged(nameof(HasNoProjects));
        SelectedProject = Projects.FirstOrDefault();
        _scope.NotifyProjectListChanged();
        StatusMessage = $"Removed project '{project.Name}' and its wallets (imported rewards kept).";
    }

    private async Task ClearProjectDataAsync()
    {
        var project = SelectedProject!;
        if (!_dialogs.Confirm(
                "Clear imported rewards",
                $"Delete every imported reward of '{project.Name}'?\n\nThe project and its wallets are kept; " +
                "you can re-import from the Transactions page."))
        {
            return;
        }

        var deleted = await _import.ClearForProjectAsync(project.Id, CancellationToken.None).ConfigureAwait(true);
        _scope.NotifyDataChanged();
        StatusMessage = deleted == 0
            ? $"No imported rewards to clear for '{project.Name}'."
            : $"Cleared {deleted} reward(s) for '{project.Name}'. Wallets kept.";
    }

    private void OpenProjectEditor(EditorMode mode)
    {
        ProjectEditorError = string.Empty;
        _projectEditor = mode;
        RaiseEditorStateChanged();
    }

    private void CloseProjectEditor()
    {
        ProjectEditorError = string.Empty;
        _projectEditor = EditorMode.None;
        RaiseEditorStateChanged();
    }

    // ---- Wallet form --------------------------------------------------------------

    private void BeginAddWallet()
    {
        // A project's wallets usually share a chain, so default to the one it already uses.
        var usedChainKey = Wallets.LastOrDefault()?.BlockchainKey;
        SelectedChain = AvailableChains.FirstOrDefault(c => c.Key == usedChainKey)
            ?? AvailableChains.FirstOrDefault(c => c.Key == SelectedChain?.Key)
            ?? AvailableChains.FirstOrDefault();

        WalletAddress = string.Empty;
        WalletLabel = string.Empty;
        WalletNotes = string.Empty;
        OpenWalletEditor(EditorMode.Add);
    }

    private void BeginEditWallet()
    {
        var wallet = SelectedWallet!;
        WalletAddress = wallet.Address;
        WalletLabel = wallet.Label ?? string.Empty;
        WalletNotes = wallet.Notes ?? string.Empty;
        OpenWalletEditor(EditorMode.Edit);
    }

    private bool CanSaveWallet() =>
        IsWalletEditorOpen && !string.IsNullOrWhiteSpace(WalletAddress) &&
        (_walletEditor == EditorMode.Edit || SelectedChain is not null);

    private async Task SaveWalletAsync()
    {
        var label = string.IsNullOrWhiteSpace(WalletLabel) ? null : WalletLabel.Trim();
        var notes = string.IsNullOrWhiteSpace(WalletNotes) ? null : WalletNotes;

        if (_walletEditor == EditorMode.Add)
        {
            if (WalletAddressFormat.Validate(SelectedChain!.ChainType, WalletAddress) is { } addError)
            {
                WalletEditorError = addError;
                return;
            }

            var wallet = await _wallets.CreateAsync(
                SelectedProject!.Id, SelectedChain.Key, WalletAddress.Trim(), label,
                CancellationToken.None).ConfigureAwait(true);
            if (notes is not null)
            {
                wallet.Notes = notes;
                await _wallets.UpdateAsync(wallet, CancellationToken.None).ConfigureAwait(true);
            }

            CloseWalletEditor();
            await LoadWalletsAsync().ConfigureAwait(true);
            SelectedWallet = Wallets.FirstOrDefault(w => w.Id == wallet.Id);
            StatusMessage = $"Added wallet on {wallet.BlockchainKey}. Next: import its rewards on the Transactions page (Import / Add).";
            return;
        }

        var existing = SelectedWallet!;
        if (_chains.TryGet(existing.BlockchainKey, out var chain) &&
            WalletAddressFormat.Validate(chain.ChainType, WalletAddress) is { } editError)
        {
            WalletEditorError = editError;
            return;
        }

        existing.Address = WalletAddress.Trim();
        existing.Label = label;
        existing.Notes = notes;
        await _wallets.UpdateAsync(existing, CancellationToken.None).ConfigureAwait(true);
        CloseWalletEditor();
        // The Wallet entity doesn't raise PropertyChanged, so refresh the DataGrid from the store.
        await LoadWalletsAsync().ConfigureAwait(true);
        StatusMessage = $"Saved wallet {existing.Address}.";
    }

    private async Task RemoveWalletAsync()
    {
        var wallet = SelectedWallet!;
        var name = wallet.Label is null ? wallet.Address : $"{wallet.Label} ({wallet.Address})";
        if (!_dialogs.Confirm("Remove wallet", $"Remove wallet {name}?\n\nImported rewards are kept."))
        {
            return;
        }

        await _wallets.DeleteAsync(wallet.Id, CancellationToken.None).ConfigureAwait(true);
        Wallets.Remove(wallet);
        SelectedWallet = null;
        OnPropertyChanged(nameof(HasNoWallets));
        StatusMessage = $"Removed wallet {wallet.Address} (imported rewards kept).";
    }

    private async Task ToggleWalletActiveAsync()
    {
        var wallet = SelectedWallet!;
        await _wallets.SetActiveAsync(wallet.Id, !wallet.IsActive, CancellationToken.None).ConfigureAwait(true);
        await LoadWalletsAsync().ConfigureAwait(true);
    }

    private void OpenWalletEditor(EditorMode mode)
    {
        WalletEditorError = string.Empty;
        _walletEditor = mode;
        RaiseEditorStateChanged();
    }

    private void CloseWalletEditor()
    {
        WalletEditorError = string.Empty;
        _walletEditor = EditorMode.None;
        RaiseEditorStateChanged();
    }

    // ---- Shared -------------------------------------------------------------------

    private void RaiseEditorStateChanged()
    {
        foreach (var name in new[]
                 {
                     nameof(IsEditing), nameof(IsBrowsing),
                     nameof(IsProjectEditorOpen), nameof(IsEditingExistingProject), nameof(ProjectEditorTitle), nameof(SaveProjectLabel),
                     nameof(IsWalletEditorOpen), nameof(IsAddingWallet), nameof(IsEditingExistingWallet), nameof(WalletEditorTitle), nameof(SaveWalletLabel),
                     nameof(ShowNoChainsHint),
                 })
        {
            OnPropertyChanged(name);
        }

        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private static List<string> ParseAddressList(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";
    private void ShowProjectError(Exception ex) => ProjectEditorError = ex.Message;
    private void ShowWalletError(Exception ex) => WalletEditorError = ex.Message;
}
