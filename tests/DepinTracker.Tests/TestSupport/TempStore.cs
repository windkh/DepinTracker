namespace DepinTracker.Tests.TestSupport;

using DepinTracker.Infrastructure.Paths;
using DepinTracker.Infrastructure.Persistence;
using DepinTracker.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Spins up the three real SQLite stores in a throwaway temp directory and runs all
/// migrations, so integration tests exercise the actual schema + Dapper mapping.
/// Disposing clears SQLite connection pools and deletes the directory.
/// </summary>
public sealed class TempStore : IDisposable
{
    public TempStore()
    {
        Root = Path.Combine(Path.GetTempPath(), "depintracker-tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
        SqliteTypeHandlers.Register();
        Factory = new SqliteConnectionFactory(Paths);
        Clock = new TestClock();
        new MigrationRunner(Factory, Clock, NullLogger<MigrationRunner>.Instance).MigrateAll();
    }

    public string Root { get; }
    public AppPaths Paths { get; }
    public SqliteConnectionFactory Factory { get; }
    public TestClock Clock { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Temp files occasionally linger behind WAL handles; not worth failing a test over.
        }
    }
}
