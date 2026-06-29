namespace DepinTracker.Application.Abstractions.Persistence;

using DepinTracker.Domain.Entities;

/// <summary>
/// Local cache of historical fiat exchange rates (generated/cache store), read
/// before calling an exchange-rate provider to keep valuations offline-first.
/// </summary>
public interface IExchangeRateCache
{
    Task<ExchangeRate?> GetAsync(
        string baseCurrency, string quoteCurrency, DateOnly date, CancellationToken cancellationToken);
    Task UpsertAsync(ExchangeRate rate, CancellationToken cancellationToken);
}
