namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>Persistence port for <see cref="Project"/> configuration data (config.db).</summary>
public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task UpdateAsync(Project project, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
