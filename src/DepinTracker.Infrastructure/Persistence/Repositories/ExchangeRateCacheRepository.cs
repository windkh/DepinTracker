namespace DepinTracker.Infrastructure.Persistence.Repositories;

using Dapper;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Domain.Entities;

/// <summary>Dapper-backed <see cref="IExchangeRateCache"/> over the generated store.</summary>
public sealed class ExchangeRateCacheRepository : IExchangeRateCache
{
    private readonly ISqliteConnectionFactory _factory;

    public ExchangeRateCacheRepository(ISqliteConnectionFactory factory) => _factory = factory;

    public async Task<ExchangeRate?> GetAsync(
        string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Generated);
        return await connection.QuerySingleOrDefaultAsync<ExchangeRate>(new CommandDefinition(
            """
            SELECT * FROM fx_cache
             WHERE BaseCurrency = @baseCurrency AND QuoteCurrency = @quoteCurrency AND Date = @date;
            """,
            new { baseCurrency, quoteCurrency, date }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken)
    {
        await using var connection = _factory.CreateOpenConnection(StoreKind.Generated);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO fx_cache (BaseCurrency, QuoteCurrency, Date, Rate, ProviderKey, RetrievedUtc)
            VALUES (@BaseCurrency, @QuoteCurrency, @Date, @Rate, @ProviderKey, @RetrievedUtc)
            ON CONFLICT (BaseCurrency, QuoteCurrency, Date)
            DO UPDATE SET Rate = excluded.Rate, ProviderKey = excluded.ProviderKey, RetrievedUtc = excluded.RetrievedUtc;
            """,
            rate, cancellationToken: cancellationToken));
    }
}
