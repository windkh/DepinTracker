namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Plugins.Abstractions;

/// <summary>
/// Settings/diagnostics page: shows the portable folder layout, reporting currencies,
/// every loaded provider and plugin (audit view), the editable Etherscan API key, and
/// lets the user create a full backup.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    // Mirrors of the constants in the Infrastructure explorer types — kept here as
    // strings so the App project doesn't have to take an Infrastructure dependency
    // for one shared name. Update both sides if you rename.
    private const string EtherscanApiKeyName = "Etherscan.ApiKey";
    private const string HeliusApiKeyName = "Helius.ApiKey";

    private readonly IBackupService _backup;
    private readonly IUserSettingsStore _userSettings;
    private string _etherscanApiKey = string.Empty;
    private string _solscanApiKey = string.Empty;

    public SettingsViewModel(
        IAppPaths paths,
        AppSettings settings,
        IUserSettingsStore userSettings,
        IEnumerable<IPriceProvider> priceProviders,
        IEnumerable<IExchangeRateProvider> fxProviders,
        IEnumerable<IBlockchainExplorer> explorers,
        IReadOnlyList<PluginManifest> plugins,
        IBackupService backup)
        : base("Settings")
    {
        _backup = backup;
        _userSettings = userSettings;
        RootPath = paths.Root;
        ReportingCurrency = settings.ReportingCurrency;
        PriceCurrency = settings.PriceCurrency;

        Paths = new ObservableCollection<string>(new[]
        {
            $"config:  {paths.Config}",
            $"data:    {paths.Data}",
            $"cache:   {paths.Cache}",
            $"logs:    {paths.Logs}",
            $"exports: {paths.Exports}",
            $"plugins: {paths.Plugins}",
            $"backups: {paths.Backups}",
        });

        Providers = new ObservableCollection<string>(
            priceProviders.Select(p => $"Price: {p.ProviderKey} (priority {p.Priority})")
                .Concat(fxProviders.Select(p => $"FX: {p.ProviderKey} (priority {p.Priority})"))
                .Concat(explorers.Select(e => $"Explorer: {e.ProviderKey} → {string.Join(", ", e.SupportedChains.Select(c => c.Key))}")));

        Plugins = new ObservableCollection<string>(
            plugins.Count == 0
                ? new[] { "(no external plugins loaded)" }
                : plugins.Select(p => $"{p.Name} v{p.Version} — {p.Description}").ToArray());

        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, onError: ex => StatusMessage = $"Error: {ex.Message}");
        SaveEtherscanApiKeyCommand = new AsyncRelayCommand(SaveEtherscanApiKeyAsync, onError: ex => StatusMessage = $"Error: {ex.Message}");
        SaveHeliusApiKeyCommand = new AsyncRelayCommand(SaveHeliusApiKeyAsync, onError: ex => StatusMessage = $"Error: {ex.Message}");
    }

    public string RootPath { get; }
    public string ReportingCurrency { get; }
    public string PriceCurrency { get; }
    public ObservableCollection<string> Paths { get; }
    public ObservableCollection<string> Providers { get; }
    public ObservableCollection<string> Plugins { get; }
    public AsyncRelayCommand CreateBackupCommand { get; }
    public AsyncRelayCommand SaveEtherscanApiKeyCommand { get; }
    public AsyncRelayCommand SaveHeliusApiKeyCommand { get; }

    public string EtherscanApiKey
    {
        get => _etherscanApiKey;
        set => SetProperty(ref _etherscanApiKey, value);
    }

    public string HeliusApiKey
    {
        get => _solscanApiKey;
        set => SetProperty(ref _solscanApiKey, value);
    }

    public override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        EtherscanApiKey = await _userSettings.GetAsync(EtherscanApiKeyName, cancellationToken)
            .ConfigureAwait(true) ?? string.Empty;
        HeliusApiKey = await _userSettings.GetAsync(HeliusApiKeyName, cancellationToken)
            .ConfigureAwait(true) ?? string.Empty;
    }

    private async Task CreateBackupAsync()
    {
        StatusMessage = "Creating backup…";
        var path = await _backup.CreateBackupAsync(CancellationToken.None).ConfigureAwait(true);
        StatusMessage = $"Backup created: {path}";
    }

    private async Task SaveEtherscanApiKeyAsync()
    {
        var trimmed = (EtherscanApiKey ?? string.Empty).Trim();
        await _userSettings.SetAsync(EtherscanApiKeyName, trimmed, CancellationToken.None)
            .ConfigureAwait(true);
        StatusMessage = string.IsNullOrEmpty(trimmed)
            ? "Etherscan API key cleared. EVM on-chain import will fail until a key is set."
            : "Etherscan API key saved.";
    }

    private async Task SaveHeliusApiKeyAsync()
    {
        var trimmed = (HeliusApiKey ?? string.Empty).Trim();
        await _userSettings.SetAsync(HeliusApiKeyName, trimmed, CancellationToken.None)
            .ConfigureAwait(true);
        StatusMessage = string.IsNullOrEmpty(trimmed)
            ? "Helius API key cleared. Solana on-chain import will fail until a key is set."
            : "Helius API key saved.";
    }
}
