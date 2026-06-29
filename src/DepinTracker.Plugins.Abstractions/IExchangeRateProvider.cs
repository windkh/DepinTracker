namespace DepinTracker.Plugins.Abstractions;

/// <summary>
/// Supplies historical fiat exchange rates. Used to convert token valuations from
/// the price currency (often USD) into the user's reporting currency (e.g. EUR).
/// </summary>
public interface IExchangeRateProvider
{
    string ProviderKey { get; }

    /// <summary>Relative priority; lower numbers are consulted first.</summary>
    int Priority { get; }

    /// <summary>
    /// Returns how many units of <paramref name="quoteCurrency"/> equal one unit of
    /// <paramref name="baseCurrency"/> on <paramref name="date"/>, or <c>null</c> if unavailable.
    /// </summary>
    Task<decimal?> GetRateAsync(
        string baseCurrency,
        string quoteCurrency,
        DateOnly date,
        CancellationToken cancellationToken);
}
