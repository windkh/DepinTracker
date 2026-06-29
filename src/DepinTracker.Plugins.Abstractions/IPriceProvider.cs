namespace DepinTracker.Plugins.Abstractions;

/// <summary>
/// Supplies historical unit prices for a token. Providers are queried in priority
/// order by the price engine; returning <c>null</c> means "no data" and lets the
/// engine fall back to the next provider rather than guessing a value.
///
/// Calls carry the originating reward's chain and contract address (when known) so
/// contract-aware providers (e.g. DeFiLlama) can resolve long-tail DePIN tokens
/// without a hand-maintained symbol map. Providers that only deal in symbols can
/// ignore those parameters.
/// </summary>
public interface IPriceProvider
{
    string ProviderKey { get; }

    /// <summary>Relative priority; lower numbers are consulted first.</summary>
    int Priority { get; }

    /// <summary>
    /// Whether this provider can attempt a lookup for the given token. Implementations
    /// should fail fast here (e.g. require a contract address) so the engine skips
    /// providers that have no chance of resolving the request.
    /// </summary>
    bool CanResolve(string tokenId, string? blockchainKey, string? tokenContract);

    /// <summary>
    /// Returns the unit price of <paramref name="tokenId"/> on <paramref name="date"/>
    /// in <paramref name="currency"/>, or <c>null</c> if unavailable.
    /// <paramref name="blockchainKey"/> and <paramref name="tokenContract"/> are the
    /// originating reward's chain key and ERC-20 contract address when known.
    /// </summary>
    Task<decimal?> GetHistoricalPriceAsync(
        string tokenId,
        string? blockchainKey,
        string? tokenContract,
        DateOnly date,
        string currency,
        CancellationToken cancellationToken);
}
