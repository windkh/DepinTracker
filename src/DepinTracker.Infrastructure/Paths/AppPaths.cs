namespace DepinTracker.Infrastructure.Paths;

using DepinTracker.Application.Abstractions;

/// <summary>
/// Portable folder layout. No data is ever written to AppData or the registry, so the
/// entire install can be copied or run from removable media.
/// <para>
/// User data (<c>config/ data/ cache/ logs/ exports/ backups/</c>) lives under
/// <see cref="Root"/>, the data root, which <c>Paths:DataRoot</c> in appsettings.json
/// sets (relative paths resolve against the application directory, default <c>data</c>).
/// <c>plugins/</c> holds code shipped with the app, so it always stays beside the executable.
/// </para>
/// </summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Configuration key holding the data root.</summary>
    public const string DataRootSettingName = "Paths:DataRoot";

    /// <summary>Data root used when <see cref="DataRootSettingName"/> is not set.</summary>
    public const string DefaultDataRoot = "data";

    /// <param name="root">Data root. Defaults to <c>data</c> under <paramref name="appDirectory"/>.</param>
    /// <param name="appDirectory">
    /// Folder of the executable, holding <c>plugins/</c>. Defaults to <paramref name="root"/>
    /// when that is given (keeps tests self-contained), else <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    public AppPaths(string? root = null, string? appDirectory = null)
    {
        AppDirectory = appDirectory ?? root ?? AppContext.BaseDirectory;
        Root = root ?? Path.Combine(AppDirectory, DefaultDataRoot);
        Config = Path.Combine(Root, "config");
        Data = Path.Combine(Root, "data");
        Cache = Path.Combine(Root, "cache");
        Logs = Path.Combine(Root, "logs");
        Exports = Path.Combine(Root, "exports");
        Backups = Path.Combine(Root, "backups");
        Plugins = Path.Combine(AppDirectory, "plugins");
    }

    /// <summary>
    /// Builds the layout from a configured data root. Blank means <see cref="DefaultDataRoot"/>;
    /// environment variables are expanded and relative paths resolve against <paramref name="appDirectory"/>.
    /// </summary>
    public static AppPaths FromSetting(string? configuredRoot, string appDirectory)
    {
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? DefaultDataRoot
            : Environment.ExpandEnvironmentVariables(configuredRoot.Trim());
        return new AppPaths(Path.GetFullPath(root, appDirectory), appDirectory);
    }

    /// <summary>Folder of the executable (holds <c>plugins/</c>).</summary>
    public string AppDirectory { get; }

    public string Root { get; }
    public string Config { get; }
    public string Data { get; }
    public string Cache { get; }
    public string Logs { get; }
    public string Exports { get; }
    public string Plugins { get; }
    public string Backups { get; }

    public void EnsureCreated()
    {
        foreach (var dir in new[] { Config, Data, Cache, Logs, Exports, Plugins, Backups })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
