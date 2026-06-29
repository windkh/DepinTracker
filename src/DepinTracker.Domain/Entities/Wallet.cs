namespace DepinTracker.Domain.Entities;

/// <summary>
/// A wallet belonging to a <see cref="Project"/>, identified by its on-chain
/// address on a specific blockchain. Wallets are configuration data and can be
/// activated/deactivated without deleting their imported history.
/// </summary>
public sealed class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }

    /// <summary>Key of the owning <see cref="Blockchain"/> (e.g. <c>"polygon"</c>).</summary>
    public string BlockchainKey { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public IList<Tag> Tags { get; set; } = new List<Tag>();
}
