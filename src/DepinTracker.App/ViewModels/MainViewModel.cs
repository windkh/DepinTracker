namespace DepinTracker.App.ViewModels;

using System.Collections.ObjectModel;
using DepinTracker.App.Mvvm;
using DepinTracker.App.Services;
using DepinTracker.Application.Services;

/// <summary>
/// Shell view model. Owns the navigable pages, the current selection, and the
/// app-wide project scope shown in the nav rail. Selecting a page triggers its
/// <see cref="ViewModelBase.OnActivatedAsync"/> so data loads lazily; changing the
/// scope raises an event the page VMs subscribe to.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ProjectService _projects;
    private readonly IProjectScope _scope;
    private readonly DashboardViewModel _dashboard;
    private readonly WelcomeViewModel _welcome;
    private ViewModelBase? _selectedPage;
    private ProjectScopeChoice? _selectedScope;
    private bool _scopeUpdateInFlight;

    public MainViewModel(
        ProjectService projects,
        IProjectScope scope,
        WelcomeViewModel welcome,
        DashboardViewModel dashboard,
        ProjectsViewModel projectsVm,
        ImportViewModel import,
        TransactionsViewModel transactions,
        ReportsViewModel reports,
        SettingsViewModel settings)
    {
        _projects = projects;
        _scope = scope;
        _dashboard = dashboard;
        _welcome = welcome;
        // Welcome is intentionally NOT in Pages — it's an onboarding screen, not a
        // permanent nav-rail entry. We swap SelectedPage to it on empty start and
        // let the "Continue to dashboard" button switch back to the regular pages.
        Pages = new ObservableCollection<ViewModelBase> { dashboard, projectsVm, import, transactions, reports, settings };
        _welcome.CompletionRequested += (_, _) => SelectedPage = _dashboard;

        AvailableScopes.Add(ProjectScopeChoice.All);
        _scope.ProjectListChanged += async (_, _) => await RefreshScopesAsync().ConfigureAwait(false);

        // Initial load — fire and forget. RefreshScopesAsync also decides whether to
        // show the Welcome page (when the projects list is empty) or the Dashboard.
        _ = RefreshScopesAsync(selectInitialPage: true);
    }

    public ObservableCollection<ViewModelBase> Pages { get; }
    public ObservableCollection<ProjectScopeChoice> AvailableScopes { get; } = new();

    public ViewModelBase? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value) && value is not null)
            {
                _ = value.OnActivatedAsync(CancellationToken.None);
            }
        }
    }

    public ProjectScopeChoice? SelectedScope
    {
        get => _selectedScope;
        set
        {
            if (!SetProperty(ref _selectedScope, value) || _scopeUpdateInFlight || value is null)
            {
                return;
            }

            // Push into the shared scope service; the change notification fans out to
            // every page VM, which re-runs OnActivatedAsync with the new filter.
            _ = _scope.SetActiveAsync(value.Id, value.Name, CancellationToken.None);
        }
    }

    private async Task RefreshScopesAsync(bool selectInitialPage = false)
    {
        try
        {
            var projects = await _projects.GetAllAsync(CancellationToken.None).ConfigureAwait(true);

            // First-run UX: when the DB is empty, land on the Welcome page instead of
            // the (empty) Dashboard so the user knows exactly what to do next.
            if (selectInitialPage)
            {
                SelectedPage = projects.Count == 0 ? _welcome : _dashboard;
            }

            var rebuilt = new List<ProjectScopeChoice> { ProjectScopeChoice.All };
            rebuilt.AddRange(projects
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new ProjectScopeChoice(p.Id, p.Name)));

            // SetProperty would fire SelectedScope changed and re-trigger SetActiveAsync;
            // guard so we don't spin while syncing the list.
            _scopeUpdateInFlight = true;
            try
            {
                AvailableScopes.Clear();
                foreach (var choice in rebuilt)
                {
                    AvailableScopes.Add(choice);
                }

                // Re-bind SelectedScope to whichever entry matches the scope service's
                // current id, defaulting to "All" when the scoped project was deleted.
                var current = AvailableScopes.FirstOrDefault(c => c.Id == _scope.ActiveProjectId)
                              ?? ProjectScopeChoice.All;
                SelectedScope = current;

                // If the persisted id had no name attached (post-hydration), bring the
                // scope label into sync now that we know it.
                if (_scope.ActiveProjectId is { } id && current.Id == id)
                {
                    await _scope.SetActiveAsync(id, current.Name, CancellationToken.None).ConfigureAwait(true);
                }
            }
            finally
            {
                _scopeUpdateInFlight = false;
            }
        }
        catch
        {
            // Best-effort UI refresh; failing here shouldn't crash the shell.
        }
    }

    /// <summary>An entry in the nav-rail scope picker. <c>Id == null</c> means "all projects".</summary>
    public sealed record ProjectScopeChoice(Guid? Id, string Name)
    {
        public static ProjectScopeChoice All { get; } = new(null, "Alle Projekte");
    }
}
