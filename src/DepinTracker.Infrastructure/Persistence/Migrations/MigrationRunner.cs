namespace DepinTracker.Infrastructure.Persistence.Migrations;

using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DepinTracker.Application.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// Applies versioned, embedded SQL migration scripts to each store. A
/// <c>schema_version</c> table per database records the highest applied version;
/// on startup every script with a higher version is run in order, each inside a
/// transaction, so schema upgrades are deterministic and idempotent.
/// </summary>
public sealed partial class MigrationRunner
{
    private const string ScriptRoot = "DepinTracker.Infrastructure.Persistence.Migrations.Scripts.";

    private readonly ISqliteConnectionFactory _factory;
    private readonly IClock _clock;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(ISqliteConnectionFactory factory, IClock clock, ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Migrates all stores to their latest embedded schema version.</summary>
    public void MigrateAll()
    {
        foreach (var store in Enum.GetValues<StoreKind>())
        {
            Migrate(store);
        }
    }

    public void Migrate(StoreKind store)
    {
        using var connection = _factory.CreateOpenConnection(store);
        EnsureVersionTable(connection);
        var current = GetCurrentVersion(connection);

        var pending = GetScriptsFor(store)
            .Where(s => s.Version > current)
            .OrderBy(s => s.Version)
            .ToList();

        foreach (var script in pending)
        {
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = script.Sql;
                command.ExecuteNonQuery();
            }

            using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText =
                    "INSERT INTO schema_version (Version, AppliedUtc) VALUES ($v, $u);";
                versionCommand.Parameters.AddWithValue("$v", script.Version);
                versionCommand.Parameters.AddWithValue("$u", _clock.UtcNow.ToString("O"));
                versionCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            _logger.LogInformation("Applied {Store} migration v{Version} ({Name})",
                store, script.Version, script.Name);
        }
    }

    /// <summary>The highest schema version currently applied to a store (0 if none).</summary>
    public int GetCurrentVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM schema_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS schema_version (Version INTEGER NOT NULL, AppliedUtc TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }

    private static IEnumerable<MigrationScript> GetScriptsFor(StoreKind store)
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var prefix = ScriptRoot + store + ".";

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal) ||
                !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resource[prefix.Length..];
            var match = VersionPattern().Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            var version = int.Parse(match.Groups["v"].Value, CultureInfo.InvariantCulture);
            yield return new MigrationScript(version, fileName, ReadResource(assembly, resource));
        }
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^(?<v>\d+)_", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    private sealed record MigrationScript(int Version, string Name, string Sql);
}
