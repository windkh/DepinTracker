namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IPriceCache"/> over the generated store.</summary>
public sealed class PriceCacheRepository : IPriceCache
{
    private readonly ISqliteConnectionFactory _factory;

    public PriceCacheRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<PricePoint?> GetAsync(
        string tokenId, DateOnly date, string currency, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Generated);
        return await connection.QuerySingleOrDefaultAsync<PricePoint>(new CommandDefinition(
            "SELECT * FROM price_cache WHERE TokenId = @tokenId AND Date = @date AND Currency = @currency;",
            new { tokenId, date, currency }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(PricePoint price, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Generated);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO price_cache (TokenId, Date, Currency, Price, ProviderKey, RetrievedUtc)
            VALUES (@TokenId, @Date, @Currency, @Price, @ProviderKey, @RetrievedUtc)
            ON CONFLICT (TokenId, Date, Currency)
            DO UPDATE SET Price = excluded.Price, ProviderKey = excluded.ProviderKey, RetrievedUtc = excluded.RetrievedUtc;
            """,
            price, cancellationToken: cancellationToken));
    }
}
