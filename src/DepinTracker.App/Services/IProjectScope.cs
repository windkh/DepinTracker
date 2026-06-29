namespace DepinTracker.App.Services;

/// <summary>
/// Shared "active project" state for the running app. A single scope drives the
/// Dashboard, Transactions, Reports and Import pages so the user picks a project
/// once and every view filters to it. <see cref="ActiveProjectId"/> = null means
/// "all projects" (the default). The selection persists across runs via the
/// user-settings store.
/// </summary>
public interface IProjectScope
{
    /// <summary>Currently-selected project, or <c>null</c> for "all projects".</summary>
    Guid? ActiveProjectId { get; }

    /// <summary>Human label for the current scope. "Alle Projekte" when no project is selected.</summary>
    string ActiveProjectName { get; }

    /// <summary>Raised when the active project changes.</summary>
    event EventHandler? ActiveProjectChanged;

    /// <summary>
    /// Raised when the project list itself changes (project added/deleted/renamed).
    /// Lets the nav-rail picker reload without each subscriber re-querying repos.
    /// </summary>
    event EventHandler? ProjectListChanged;

    /// <summary>Switch the active project. Persists the choice and raises the event.</summary>
    Task SetActiveAsync(Guid? projectId, string? name, CancellationToken cancellationToken);

    /// <summary>Hydrate from the user-settings store on app startup.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Tell listeners that the project list has changed. Call from CRUD code paths.</summary>
    void NotifyProjectListChanged();
}
