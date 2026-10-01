namespace DepinTracker.App;

using System.IO;
using System.Windows;
using DepinTracker.App.Services;
using DepinTracker.App.ViewModels;
using DepinTracker.Application;
using DepinTracker.Application.Abstractions;
using DepinTracker.Infrastructure;
using DepinTracker.Infrastructure.Logging;
using DepinTracker.Infrastructure.Paths;
using DepinTracker.Infrastructure.Persistence.Maintenance;
using DepinTracker.Infrastructure.Persistence.Migrations;
using DepinTracker.Infrastructure.Plugins;
using DepinTracker.Plugins.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Composition root. Builds the generic host (configuration → logging → DI), loads
/// plugins from the portable folder, migrates the databases, then shows the shell.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // appsettings.json ships beside the executable and decides where the portable data
        // root lives, so read it before resolving paths. Debug builds also emit
        // appsettings.Debug.json pointing the data root at <solution>/data.
        var appDirectory = AppContext.BaseDirectory;
        AppPaths paths;
        try
        {
            var configuration = AddAppSettings(new ConfigurationBuilder(), appDirectory).Build();

            // Portable paths must exist before anything reads/writes config or data.
            paths = AppPaths.FromSetting(configuration[AppPaths.DataRootSettingName], appDirectory);
            paths.EnsureCreated();
        }
        catch (Exception ex)
        {
            // No log folder yet — the dialog is the only place this can surface.
            MessageBox.Show($"Could not read config\\appsettings.json or create the data folder: {ex.Message}",
                "DePIN Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        // Last-resort crash logging so startup/UI failures are diagnosable in a portable install.
        var crashLog = Path.Combine(paths.Logs, "crash.log");
        DispatcherUnhandledException += (_, args) => WriteCrash(crashLog, "Dispatcher", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrash(crashLog, "AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => WriteCrash(crashLog, "Task", args.Exception);

        try
        {
            Bootstrap(paths, appDirectory);
        }
        catch (Exception ex)
        {
            WriteCrash(crashLog, "Startup", ex);
            MessageBox.Show($"Startup failed: {ex.Message}\n\nSee {crashLog}", "DePIN Tracker", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static IConfigurationBuilder AddAppSettings(IConfigurationBuilder config, string appDirectory) =>
        config
            .SetBasePath(Path.Combine(appDirectory, "config"))
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Debug.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "DEPIN_");

    private void Bootstrap(AppPaths paths, string appDirectory)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) => AddAppSettings(config, appDirectory))
            .ConfigureLogging((_, logging) =>
            {
                logging.ClearProviders();
                logging.AddProvider(new FileLoggerProvider(paths.Logs));
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IAppPaths>(paths);
                services.AddApplication();
                services.AddInfrastructure(context.Configuration);

                LoadPlugins(services, paths);

                services.AddSingleton<IProjectScope, ProjectScope>();
                services.AddSingleton<WelcomeViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ProjectsViewModel>();
                services.AddSingleton<ImportViewModel>();
                services.AddSingleton<TransactionsViewModel>();
                services.AddSingleton<ReportsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        // Apply database migrations to all three stores before the UI loads.
        _host.Services.GetRequiredService<MigrationRunner>().MigrateAll();

        // Patch up TokenContract on rewards imported by older builds that didn't read
        // Blockscout v2's address_hash. Best-effort and idempotent.
        _host.Services.GetRequiredService<TokenContractBackfiller>()
            .BackfillAsync(CancellationToken.None).GetAwaiter().GetResult();

        // Hydrate the persisted "active project" before the shell binds — the picker
        // and the page VMs read from this scope at construction time.
        _host.Services.GetRequiredService<IProjectScope>()
            .InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    private static void WriteCrash(string path, string source, Exception? ex)
    {
        try
        {
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} [{source}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never let crash logging itself throw.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static void LoadPlugins(IServiceCollection services, IAppPaths paths)
    {
        using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider(paths.Logs)));
        var loader = new PluginLoader(bootstrapLoggerFactory.CreateLogger<PluginLoader>());
        var manifests = loader.LoadInto(services, paths.Plugins);
        services.AddSingleton<IReadOnlyList<PluginManifest>>(manifests);
    }
}
