namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Services;
using DepinTracker.Domain.Entities;
using DepinTracker.Domain.Enums;

/// <summary>
/// First-run onboarding page. Shown automatically the first time the app starts
/// with an empty config store (no projects yet). Bundles the three setup steps a
/// new user has to perform — paste API keys, create a project, add a wallet —
/// into a single scrollable card stack so the user never has to discover which
/// nav-rail page to open in which order. Once the user clicks "Continue to
/// dashboard" the shell switches to Dashboard and this page becomes inaccessible
/// for the remainder of the session.
/// </summary>
public sealed class WelcomeViewModel : ViewModelBase
{
    // Mirrors of the API-key setting names in the explorer types — duplicated as
    // string consts so the App project doesn't depend on Infrastructure for one name.
    private const string EtherscanApiKeyName = "Etherscan.ApiKey";
    private const string HeliusApiKeyName = "Helius.ApiKey";

    // Curated quick-start preset, so a brand-new user can wire GEODNET in two clicks.
    private const string GeodnetFilterAddress = "0x8fb9dd00b9a3d893da96d444817d0b77330d5478";

    private readonly ProjectService _projects;
    private readonly WalletService _wallets;
    private readonly IBlockchainRegistry _chains;
    private readonly IUserSettingsStore _userSettings;
    private readonly IProjectScope _scope;

    private string _etherscanApiKey = string.Empty;
    private string _heliusApiKey = string.Empty;
    private string _newProjectName = string.Empty;
    private string _newWalletAddress = string.Empty;
    private string _newWalletLabel = string.Empty;
    private string _allowedSourceAddresses = string.Empty;
    private Blockchain? _selectedChain;
    private Project? _createdProject;
    private bool _walletAdded;
    private bool _apiKeysSaved;

    public WelcomeViewModel(
        ProjectService projects,
        WalletService wallets,
        IBlockchainRegistry chains,
        IUserSettingsStore userSettings,
        IProjectScope scope)
        : base("Welcome")
    {
        _projects = projects;
        _wallets = wallets;
        _chains = chains;
        _userSettings = userSettings;
        _scope = scope;

        AvailableChains = new ObservableCollection<Blockchain>(chains.All.OrderBy(c => c.Name));
        _selectedChain = AvailableChains.FirstOrDefault(c => string.Equals(c.Key, "polygon", StringComparison.OrdinalIgnoreCase))
            ?? AvailableChains.FirstOrDefault();

        SaveApiKeysCommand = new AsyncRelayCommand(SaveApiKeysAsync, () => CanSaveApiKeys(), ShowError);
        CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, () => !string.IsNullOrWhiteSpace(NewProjectName) && CreatedProject is null, ShowError);
        UseGeodnetPresetCommand = new RelayCommand(() => AllowedSourceAddresses = GeodnetFilterAddress);
        AddWalletCommand = new AsyncRelayCommand(AddWalletAsync, CanAddWallet, ShowError);
        ContinueCommand = new RelayCommand(() => CompletionRequested?.Invoke(this, EventArgs.Empty), CanContinue);
    }

    /// <summary>Fires when the user clicks "Continue to dashboard"; the shell switches the active page.</summary>
    public event EventHandler? CompletionRequested;

    public ObservableCollection<Blockchain> AvailableChains { get; }

    public AsyncRelayCommand SaveApiKeysCommand { get; }
    public AsyncRelayCommand CreateProjectCommand { get; }
    public RelayCommand UseGeodnetPresetCommand { get; }
    public AsyncRelayCommand AddWalletCommand { get; }
    public RelayCommand ContinueCommand { get; }

    public string EtherscanApiKey { get => _etherscanApiKey; set => SetProperty(ref _etherscanApiKey, value); }
    public string HeliusApiKey { get => _heliusApiKey; set => SetProperty(ref _heliusApiKey, value); }
    public string NewProjectName { get => _newProjectName; set => SetProperty(ref _newProjectName, value); }
    public string NewWalletAddress { get => _newWalletAddress; set => SetProperty(ref _newWalletAddress, value); }
    public string NewWalletLabel { get => _newWalletLabel; set => SetProperty(ref _newWalletLabel, value); }
    public string AllowedSourceAddresses { get => _allowedSourceAddresses; set => SetProperty(ref _allowedSourceAddresses, value); }
    public Blockchain? SelectedChain { get => _selectedChain; set => SetProperty(ref _selectedChain, value); }

    /// <summary>The project the user just created, or null until they do.</summary>
    public Project? CreatedProject
    {
        get => _createdProject;
        private set
        {
            if (SetProperty(ref _createdProject, value))
            {
                OnPropertyChanged(nameof(IsProjectReady));
            }
        }
    }

    public bool IsProjectReady => _createdProject is not null;

    /// <summary>True once the user has added their first wallet to the new project.</summary>
    public bool WalletAdded
    {
        get => _walletAdded;
        private set
        {
            if (SetProperty(ref _walletAdded, value))
            {
                OnPropertyChanged(nameof(CanContinueProperty));
            }
        }
    }

    public bool ApiKeysSaved
    {
        get => _apiKeysSaved;
        private set => SetProperty(ref _apiKeysSaved, value);
    }

    /// <summary>Bound to the UI to grey out the Continue button until a wallet exists.</summary>
    public bool CanContinueProperty => CanContinue();

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        EtherscanApiKey = await _userSettings.GetAsync(EtherscanApiKeyName, cancellationToken).ConfigureAwait(true) ?? string.Empty;
        HeliusApiKey = await _userSettings.GetAsync(HeliusApiKeyName, cancellationToken).ConfigureAwait(true) ?? string.Empty;
        ApiKeysSaved = !string.IsNullOrWhiteSpace(EtherscanApiKey) || !string.IsNullOrWhiteSpace(HeliusApiKey);
        StatusMessage = "Welcome — three small steps and you're set up.";
    }

    private bool CanSaveApiKeys() =>
        !string.IsNullOrWhiteSpace(EtherscanApiKey) || !string.IsNullOrWhiteSpace(HeliusApiKey);

    private async Task SaveApiKeysAsync()
    {
        await _userSettings.SetAsync(EtherscanApiKeyName, EtherscanApiKey.Trim(), CancellationToken.None).ConfigureAwait(true);
        await _userSettings.SetAsync(HeliusApiKeyName, HeliusApiKey.Trim(), CancellationToken.None).ConfigureAwait(true);
        ApiKeysSaved = true;
        StatusMessage = "API keys saved. Next: create a project below.";
    }

    private async Task CreateProjectAsync()
    {
        var project = await _projects.CreateAsync(NewProjectName.Trim(), null, null, CancellationToken.None).ConfigureAwait(true);
        CreatedProject = project;
        _scope.NotifyProjectListChanged();
        // Pre-select this project as the global scope so the rest of the app focuses on it.
        await _scope.SetActiveAsync(project.Id, project.Name, CancellationToken.None).ConfigureAwait(true);
        StatusMessage = $"Project '{project.Name}' created. Add your first wallet below.";
    }

    private bool CanAddWallet() =>
        CreatedProject is not null && SelectedChain is not null
        && !string.IsNullOrWhiteSpace(NewWalletAddress);

    private async Task AddWalletAsync()
    {
        var project = CreatedProject!;
        var wallet = await _wallets.CreateAsync(
            project.Id,
            SelectedChain!.Key,
            NewWalletAddress.Trim(),
            string.IsNullOrWhiteSpace(NewWalletLabel) ? null : NewWalletLabel.Trim(),
            CancellationToken.None).ConfigureAwait(true);

        // If the user filled in allowed source addresses, persist them onto the project now.
        var allowList = (AllowedSourceAddresses ?? string.Empty)
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (allowList.Count > 0)
        {
            project.RewardSourceAddresses = allowList;
            await _projects.UpdateAsync(project, CancellationToken.None).ConfigureAwait(true);
        }

        WalletAdded = true;
        StatusMessage = allowList.Count > 0
            ? $"Wallet added and {allowList.Count} source address(es) saved. Click 'Continue to dashboard' to start importing."
            : "Wallet added. Click 'Continue to dashboard' to start importing.";
    }

    private bool CanContinue() => WalletAdded;

    private void ShowError(Exception ex) => StatusMessage = $"Error: {ex.Message}";
}
