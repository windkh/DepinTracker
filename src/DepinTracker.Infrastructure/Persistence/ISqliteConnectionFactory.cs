namespace DepinTracker.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;

/// <summary>Opens connections to one of the three portable SQLite stores.</summary>
public interface ISqliteConnectionFactory
{
    /// <summary>Returns the on-disk file path for a store.</summary>
    string GetDatabasePath(StoreKind store);

    /// <summary>Creates and opens a new connection to the given store.</summary>
    SqliteConnection CreateOpenConnection(StoreKind store);
}
