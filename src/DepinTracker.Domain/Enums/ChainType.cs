namespace DepinTracker.Domain.Enums;

/// <summary>
/// The broad family a blockchain belongs to. Plugins declare which family a
/// chain implements so the rest of the system can reason about address formats,
/// explorer styles and signing schemes without hard-coding individual chains.
/// </summary>
public enum ChainType
{
    Unknown = 0,
    Evm = 1,
    Solana = 2,
    Cosmos = 3,
}
