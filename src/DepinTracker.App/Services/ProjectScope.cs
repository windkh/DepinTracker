namespace DepinTracker.App.Services;

using DepinTracker.Application.Abstractions.Persistence;

/// <inheritdoc cref="IProjectScope"/>
public sealed class ProjectScope : IProjectScope
{
    private const string SettingKey = "ActiveProject.Id";
    private const string AllProjectsLabel = "Alle Projekte";

    private readonly IUserSettingsStore _settings;
    private Guid? _activeProjectId;
    private string _activeProjectName = AllProjectsLabel;

    public ProjectScope(IUserSettingsStore settings) => _settings = settings;

    public Guid? ActiveProjectId => _activeProjectId;
    public string ActiveProjectName => _activeProjectName;

    public event EventHandler? ActiveProjectChanged;
    public event EventHandler? ProjectListChanged;
    public event EventHandler? DataChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var raw = await _settings.GetAsync(SettingKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var id))
        {
            _activeProjectId = id;
            // Name is filled in by MainViewModel once the projects are loaded; until then
            // the picker shows the GUID stub, which is acceptable for the brief moment
            // before the first refresh.
            _activeProjectName = "(loading…)";
        }
    }

    public async Task SetActiveAsync(Guid? projectId, string? name, CancellationToken cancellationToken)
    {
        if (_activeProjectId == projectId)
        {
            // Name might have changed even if the id didn't — e.g. project renamed.
            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, _activeProjectName, StringComparison.Ordinal))
            {
                _activeProjectName = name;
            }

            return;
        }

        _activeProjectId = projectId;
        _activeProjectName = string.IsNullOrWhiteSpace(name) ? AllProjectsLabel : name;
        await _settings.SetAsync(SettingKey, projectId?.ToString("D"), cancellationToken).ConfigureAwait(false);
        ActiveProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyProjectListChanged() => ProjectListChanged?.Invoke(this, EventArgs.Empty);

    public void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
}
