namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IRawResponseRepository"/> over the imported store.</summary>
public sealed class RawResponseRepository : IRawResponseRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public RawResponseRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task AddAsync(RawProviderResponse response, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Imported);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO raw_provider_responses
                (Id, ImportSessionId, ProviderKey, RequestDescription, ResponseBody, RetrievedUtc)
            VALUES
                (@Id, @ImportSessionId, @ProviderKey, @RequestDescription, @ResponseBody, @RetrievedUtc);
            """,
            response, cancellationToken: cancellationToken));
    }
}
