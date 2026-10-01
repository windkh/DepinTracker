namespace DepinTracker.Application.Abstractions;

/// <summary>
/// Resolves the portable on-disk folder layout. User data lives under a configurable
/// data root (default <c>data/</c> beside the executable) — never AppData or the
/// registry — so the whole install is xcopy-portable. Implemented in Infrastructure.
/// </summary>
public interface IAppPaths
{
    string Root { get; }
    string Config { get; }
    string Data { get; }
    string Cache { get; }
    string Logs { get; }
    string Exports { get; }
    string Plugins { get; }
    string Backups { get; }

    /// <summary>Creates any missing portable folders. Safe to call repeatedly.</summary>
    void EnsureCreated();
}
