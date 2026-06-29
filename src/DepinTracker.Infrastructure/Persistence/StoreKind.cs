namespace DepinTracker.Infrastructure.Persistence;

/// <summary>
/// The three physically-separate SQLite databases, mirroring the conceptual
/// data-origin separation. Keeping them in separate files means generated/cache
/// data can be wiped and rebuilt without ever touching authoritative imported data.
/// </summary>
public enum StoreKind
{
    /// <summary>config.db — user-owned configuration (projects, wallets, tags).</summary>
    Config,

    /// <summary>imported.db — authoritative imported rewards, raw responses, sessions.</summary>
    Imported,

    /// <summary>generated.db — rebuildable price/FX caches and computed data.</summary>
    Generated,
}
