namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>Persistence port for verbatim <see cref="RawProviderResponse"/> payloads (imported.db).</summary>
public interface IRawResponseRepository
{
    Task AddAsync(RawProviderResponse response, CancellationToken cancellationToken);
}
