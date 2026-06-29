namespace DepinTracker.Application.Services;

using DepinTracker.Application.Abstractions;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>
/// Use-case operations for projects. Thin orchestration over the repository plus
/// input validation; holds no project-specific business rules (the system is generic).
/// </summary>
public sealed class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IClock _clock;

    public ProjectService(IProjectRepository projects, IClock clock)
    {
        _projects = projects;
        _clock = clock;
    }

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken) =>
        _projects.GetAllAsync(cancellationToken);

    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _projects.GetAsync(id, cancellationToken);

    public async Task<Project> CreateAsync(
        string name, string? description, string? notes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        var project = new Project
        {
            Name = name.Trim(),
            Description = description,
            Notes = notes,
            IsActive = true,
            CreatedUtc = _clock.UtcNow,
        };

        await _projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
        return project;
    }

    public Task UpdateAsync(Project project, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            throw new ArgumentException("Project name is required.", nameof(project));
        }

        return _projects.UpdateAsync(project, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _projects.DeleteAsync(id, cancellationToken);
}
