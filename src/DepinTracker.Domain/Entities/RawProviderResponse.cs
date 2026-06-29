namespace DepinTracker.Domain.Entities;

/// <summary>
/// The verbatim response a provider returned during an import, preserved for
/// auditability and reproducibility. Imported financial data can always be
/// re-derived from these raw payloads without re-querying the network.
/// </summary>
public sealed class RawProviderResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportSessionId { get; set; }
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>The request URL or a description of the request that produced the payload.</summary>
    public string RequestDescription { get; set; } = string.Empty;

    /// <summary>The raw response body, stored exactly as received (typically JSON).</summary>
    public string ResponseBody { get; set; } = string.Empty;

    public DateTimeOffset RetrievedUtc { get; set; } = DateTimeOffset.UtcNow;
}
