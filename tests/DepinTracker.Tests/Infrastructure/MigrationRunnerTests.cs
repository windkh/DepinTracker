namespace DepinTracker.Tests.Infrastructure;

using DepinTracker.Infrastructure.Persistence;
using DepinTracker.Infrastructure.Persistence.Migrations;
using DepinTracker.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

public class MigrationRunnerTests
{
    // Highest shipped migration per store. Bump when adding a new NNNN_*.sql script.
    private static readonly Dictionary<StoreKind, int> ExpectedVersions = new()
    {
        [StoreKind.Config] = 2,    // 0001_init + 0002_project_reward_sources
        [StoreKind.Imported] = 2,  // 0001_init + 0002_reward_from_address
        [StoreKind.Generated] = 1, // 0001_init
    };

    [Fact]
    public void All_stores_migrate_to_their_latest_version()
    {
        using var store = new TempStore();
        var runner = new MigrationRunner(store.Factory, store.Clock, NullLogger<MigrationRunner>.Instance);

        foreach (var kind in Enum.GetValues<StoreKind>())
        {
            using var connection = store.Factory.CreateOpenConnection(kind);
            runner.GetCurrentVersion(connection).Should().Be(
                ExpectedVersions[kind], "store {0} should be at its latest shipped schema version", kind);
        }
    }

    [Fact]
    public void Migrating_again_is_idempotent()
    {
        using var store = new TempStore();
        var runner = new MigrationRunner(store.Factory, store.Clock, NullLogger<MigrationRunner>.Instance);

        // TempStore already migrated once; a second run must not throw or re-apply.
        runner.MigrateAll();

        using var connection = store.Factory.CreateOpenConnection(StoreKind.Imported);
        runner.GetCurrentVersion(connection).Should().Be(ExpectedVersions[StoreKind.Imported]);
    }
}
