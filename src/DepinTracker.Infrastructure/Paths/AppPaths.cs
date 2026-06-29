namespace DepinTracker.Infrastructure.Paths;

using DepinTracker.Application.Abstractions;

/// <summary>
/// Portable folder layout rooted at the application directory. No data is ever
/// written to AppData or the registry, so the entire install can be copied or run
/// from removable media. The root can be overridden (used by tests) but defaults
/// to <see cref="AppContext.BaseDirectory"/>.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? AppContext.BaseDirectory;
        Config = Path.Combine(Root, "config");
        Data = Path.Combine(Root, "data");
        Cache = Path.Combine(Root, "cache");
        Logs = Path.Combine(Root, "logs");
        Exports = Path.Combine(Root, "exports");
        Plugins = Path.Combine(Root, "plugins");
        Backups = Path.Combine(Root, "backups");
    }

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
