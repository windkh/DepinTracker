namespace DepinTracker.Infrastructure;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Application.Configuration;
using DepinTracker.Infrastructure.Backup;
using DepinTracker.Infrastructure.Blockchains;
using DepinTracker.Infrastructure.Paths;
using DepinTracker.Infrastructure.Persistence;
using DepinTracker.Infrastructure.Persistence.Maintenance;
using DepinTracker.Infrastructure.Persistence.Migrations;
using DepinTracker.Infrastructure.Persistence.Repositories;
using DepinTracker.Infrastructure.Providers;
using DepinTracker.Infrastructure.Reports;
using DepinTracker.Infrastructure.Time;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Registers the Infrastructure layer: portable paths, the three SQLite stores and
/// their repositories, the migration runner, backup, the built-in HTTP providers,
/// and the blockchain registry. Stateless services are singletons; repositories open
/// a fresh connection per call so they are safe to share.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Dapper handlers for types SQLite stores as TEXT (Guid/DateTimeOffset/DateOnly/decimal).
        SqliteTypeHandlers.Register();

        // Options bound from configuration (manual binding keeps the dependency surface minimal).
        var appSettings = configuration.GetSection("App").Get<AppSettings>() ?? new AppSettings();
        services.AddSingleton(appSettings);

        var providerOptions = configuration.GetSection(ProvidersOptions.SectionName).Get<ProvidersOptions>()
            ?? new ProvidersOptions();
        services.AddSingleton(Options.Create(providerOptions));

        // Portable paths + clock.
        // The host registers the configured layout first; this default only fills in when it didn't.
        services.TryAddSingleton<IAppPaths>(_ => new AppPaths());
        services.AddSingleton<IClock, SystemClock>();

        // Persistence.
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<TokenContractBackfiller>();
        services.AddSingleton<IProjectRepository, ProjectRepository>();
        services.AddSingleton<IWalletRepository, WalletRepository>();
        services.AddSingleton<IRewardRepository, RewardRepository>();
        services.AddSingleton<IImportSessionRepository, ImportSessionRepository>();
        services.AddSingleton<IRawResponseRepository, RawResponseRepository>();
        services.AddSingleton<IPriceCache, PriceCacheRepository>();
        services.AddSingleton<IExchangeRateCache, ExchangeRateCacheRepository>();
        services.AddSingleton<IUserSettingsStore, UserSettingsStore>();
        services.AddSingleton<IDispositionRepository, DispositionRepository>();
        services.AddSingleton<IBackupService, BackupService>();

        // Report exporters — print-styled HTML serves as the PDF path
        // (browser → "Save as PDF"), no native dependency required.
        services.AddSingleton<IReportExporter, CsvReportExporter>();
        services.AddSingleton<IReportExporter, ExcelReportExporter>();
        services.AddSingleton<IReportExporter, HtmlReportExporter>();
        services.AddSingleton<IReportExporter, WordReportExporter>();

        // HTTP clients for the built-in providers.
        services.AddHttpClient(CoinGeckoPriceProvider.Key, ConfigureClient);
        services.AddHttpClient(DefiLlamaPriceProvider.Key, ConfigureClient);
        services.AddHttpClient(FrankfurterExchangeRateProvider.Key, ConfigureClient);
        services.AddHttpClient(EtherscanV2EvmExplorer.Key, ConfigureClient);
        services.AddHttpClient(HeliusSolanaExplorer.Key, ConfigureClient);

        // Built-in providers (plugins may register additional implementations).
        // DeFiLlama is tried first (priority 50) — contract-keyed lookups resolve
        // long-tail DePIN tokens that CoinGecko (priority 100) does not index by symbol.
        services.AddSingleton<IPriceProvider, DefiLlamaPriceProvider>();
        services.AddSingleton<IPriceProvider, CoinGeckoPriceProvider>();
        services.AddSingleton<IExchangeRateProvider, FrankfurterExchangeRateProvider>();
        services.AddSingleton<IBlockchainExplorer, EtherscanV2EvmExplorer>();
        services.AddSingleton<IBlockchainExplorer, HeliusSolanaExplorer>();

        // Blockchain registry aggregates chains from all registered explorers (incl. plugins).
        services.AddSingleton<IBlockchainRegistry, BlockchainRegistry>();

        return services;
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DePINTracker/0.1 (+offline-first)");
    }
}
