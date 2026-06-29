namespace DepinTracker.Domain.Entities;

/// <summary>
/// A tracked DePIN project. Projects own one or more <see cref="Wallet"/>s and are
/// pure configuration data (stored in config.db). There is no project-specific
/// logic anywhere in the system — a project is just a named grouping.
/// </summary>
public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Tags applied to this project. Loaded/saved by the repository.</summary>
    public IList<Tag> Tags { get; set; } = new List<Tag>();

    /// <summary>
    /// Lower-cased on-chain addresses that count as legitimate reward sources for this
    /// project. When non-empty, an on-chain import only keeps token transfers whose
    /// <c>from</c> address is in this list — letting the user reject unrelated airdrops
    /// or transfers that happen to land on the same wallet. An empty list means
    /// "accept everything", preserving backwards-compatible behaviour.
    /// </summary>
    public IList<string> RewardSourceAddresses { get; set; } = new List<string>();
}
