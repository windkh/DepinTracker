namespace DepinTracker.Infrastructure.Persistence;

using DepinTracker.Application.Abstractions;
using Microsoft.Data.Sqlite;

/// <summary>
/// Maps each <see cref="StoreKind"/> to a file inside the portable folder layout
/// and opens connections with foreign keys enabled. config.db lives under config/,
/// imported.db under data/, and the rebuildable generated.db under cache/.
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly IAppPaths _paths;

    public SqliteConnectionFactory(IAppPaths paths) => _paths = paths;

    public string GetDatabasePath(StoreKind store) => store switch
    {
        StoreKind.Config => Path.Combine(_paths.Config, "config.db"),
        StoreKind.Imported => Path.Combine(_paths.Data, "imported.db"),
        StoreKind.Generated => Path.Combine(_paths.Cache, "generated.db"),
        _ => throw new ArgumentOutOfRangeException(nameof(store), store, "Unknown store."),
    };

    public SqliteConnection CreateOpenConnection(StoreKind store)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = GetDatabasePath(store),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
