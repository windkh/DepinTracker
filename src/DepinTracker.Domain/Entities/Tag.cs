namespace DepinTracker.Domain.Entities;

/// <summary>A free-form label that can be attached to projects and wallets.</summary>
public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
