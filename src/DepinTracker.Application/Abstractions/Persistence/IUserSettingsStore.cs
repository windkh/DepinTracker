namespace DepinTracker.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for user-editable runtime settings (config.db <c>settings</c>
/// table). Distinct from the static <c>AppSettings</c> bound at startup from
/// <c>appsettings.json</c> — these are values the user can change in the running
/// app (e.g. provider API keys) without restarting or editing JSON by hand.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>Returns the stored value or <c>null</c> if no row exists for the key.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Upserts the value. Passing <c>null</c> or empty deletes the row, so an empty
    /// string and "not configured" can't drift apart.
    /// </summary>
    Task SetAsync(string key, string? value, CancellationToken cancellationToken);
}
