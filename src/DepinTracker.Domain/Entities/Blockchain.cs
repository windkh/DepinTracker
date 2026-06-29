namespace DepinTracker.Domain.Entities;

using DepinTracker.Domain.Enums;

/// <summary>
/// A blockchain descriptor contributed by a blockchain plugin. The set of
/// available blockchains is open-ended: plugins register new ones at runtime,
/// so nothing here is hard-coded to a specific chain.
/// </summary>
public sealed class Blockchain
{
    /// <summary>Stable lookup key, e.g. <c>"polygon"</c>, <c>"solana"</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ChainType ChainType { get; set; } = ChainType.Unknown;

    /// <summary>Native gas/currency symbol, e.g. <c>"MATIC"</c>, <c>"SOL"</c>.</summary>
    public string NativeSymbol { get; set; } = string.Empty;

    /// <summary>EVM numeric chain id, when applicable (null for non-EVM chains).</summary>
    public int? EvmChainId { get; set; }

    public override string ToString() => $"{Name} ({Key})";
}
