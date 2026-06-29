namespace DepinTracker.Domain.Entities;

using DepinTracker.Domain.Enums;

/// <summary>
/// An audit record of a single import run. Every imported reward references the
/// session that produced it, giving full provenance: who/what/when imported the
/// data and how many rows were added versus skipped.
/// </summary>
public sealed class ImportSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Provider key responsible for the import (e.g. <c>"blockscout"</c>, <c>"manual"</c>).</summary>
    public string ProviderKey { get; set; } = string.Empty;

    public ImportSource Source { get; set; } = ImportSource.Manual;
    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    /// <summary>Wallet the import targeted, when applicable.</summary>
    public Guid? WalletId { get; set; }

    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedUtc { get; set; }

    public int ItemsImported { get; set; }
    public int ItemsSkipped { get; set; }
    public string? Message { get; set; }
}
