namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>Persistence port for <see cref="ImportSession"/> audit records (imported.db).</summary>
public interface IImportSessionRepository
{
    Task AddAsync(ImportSession session, CancellationToken cancellationToken);
    Task UpdateAsync(ImportSession session, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportSession>> GetRecentAsync(int count, CancellationToken cancellationToken);
}
