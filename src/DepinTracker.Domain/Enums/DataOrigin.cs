namespace DepinTracker.Domain.Enums;

/// <summary>
/// Where a piece of data originated. This is the conceptual backbone of the
/// three-store separation (config.db / imported.db / generated.db): imported
/// data is authoritative and must never be silently overwritten, generated data
/// can always be rebuilt, and configuration is user-owned.
/// </summary>
public enum DataOrigin
{
    /// <summary>User-owned configuration (projects, wallets, settings).</summary>
    Configuration = 0,

    /// <summary>Authoritative imported financial data. Never silently overwritten.</summary>
    Imported = 1,

    /// <summary>Derived/computed data that can be rebuilt from imported + online sources.</summary>
    Generated = 2,
}
