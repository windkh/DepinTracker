namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>
/// Local cache of historical token prices (generated/cache store). The price
/// engine reads here first to stay offline-first and only calls providers on a miss.
/// </summary>
public interface IPriceCache
{
    Task<PricePoint?> GetAsync(string tokenId, DateOnly date, string currency, CancellationToken cancellationToken);
    Task UpsertAsync(PricePoint price, CancellationToken cancellationToken);
}
