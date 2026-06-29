# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, test, run

```sh
dotnet build DepinTracker.sln                          # build everything (Release: --configuration Release)
dotnet test                                            # run the whole xUnit suite
dotnet test --filter "FullyQualifiedName~ValuationServiceTests"   # one class
dotnet test --filter "FullyQualifiedName~ValuationServiceTests.Values_using_cached_price"   # one test
dotnet run --project src/DepinTracker.App             # launch the WPF app (Windows only — net9.0-windows)
```

The .NET 9 SDK version is pinned by `global.json`. VS Code tasks `build` / `test` / `run-app` and the *Launch DePIN Tracker (WPF)* configuration are pre-wired. CI builds and tests on `windows-latest` only — WPF can't build on Linux/macOS runners.

## Architecture

Clean Architecture; dependencies point inward. Crossing a layer means going through an interface.

- **`DepinTracker.Domain`** — pure entities/value objects/enums. No external dependencies. `Money` and `TokenAmount` are `decimal`-backed (never float).
- **`DepinTracker.Plugins.Abstractions`** — the six extension interfaces (`IBlockchainExplorer`, `IPriceProvider`, `IExchangeRateProvider`, `IRewardClassifier`, `IAnalyticsProvider`, `IReportExporter`) + `IPlugin` / `PluginManifest`. Every method is `async` and takes a `CancellationToken`.
- **`DepinTracker.Application`** — orchestration only: ports (`IProjectRepository`, `IRewardRepository`, `IPriceCache`, …), services (`PriceEngine`, `ExchangeRateEngine`, `ValuationService`, `RewardImportService`, `DashboardService`, …), DTOs. Depends only on Domain + Abstractions.
- **`DepinTracker.Infrastructure`** — adapters: Dapper repositories, `MigrationRunner`, HTTP providers (CoinGecko / Frankfurter / Blockscout), `PluginLoader`, `BackupService`, file logger. Targets `net9.0` (no WPF coupling).
- **`DepinTracker.App`** — composition root. `App.xaml.cs` builds the generic host (config → logging → DI), calls `LoadPlugins`, runs `MigrationRunner.MigrateAll()`, then shows the MVVM shell. MVVM primitives are hand-rolled — there is no MVVM toolkit dependency.

## Persistence: three physical SQLite files

`SqliteConnectionFactory` opens one of three databases per call, matching the `DataOrigin` enum and `StoreKind`:

| Store | File | Why separate |
| --- | --- | --- |
| Config | `config/config.db` | Projects, wallets, tags, settings |
| Imported | `data/imported.db` | Authoritative imported rewards + raw provider responses + import sessions |
| Generated | `cache/generated.db` | Rebuildable price and FX caches |

The split exists so generated data can be wiped/rebuilt without ever touching imported financial data. Connections enable `PRAGMA foreign_keys = ON` and WAL journaling.

### Schema migrations — embedded SQL, never edit shipped scripts

Migration scripts live under `src/DepinTracker.Infrastructure/Persistence/Migrations/Scripts/{Config,Imported,Generated}/NNNN_name.sql` and are compiled as embedded resources (`EmbeddedResource Include="Persistence\Migrations\Scripts\**\*.sql"`). `MigrationRunner` applies any script whose version exceeds each store's `schema_version`, each inside a transaction.

To change schema: **add** `0002_*.sql` (etc.) to the relevant store folder. Do not modify an already-shipped script — re-running migrations must be idempotent.

### Dapper type handlers (registered by `SqliteTypeHandlers.Register`)

SQLite has no native types for several CLR types, so these are stored as TEXT in a deterministic, culture-invariant form: `Guid` → `D`, `DateTimeOffset` → ISO-8601 `O`, `DateOnly` → `yyyy-MM-dd`, `decimal` → invariant string (never REAL). Tests register them via `TempStore`; production registers them in `Infrastructure.DependencyInjection.AddInfrastructure`.

### Reward dedupe

`reward_transactions` has a unique `DedupKey` (`wallet|chain|txhash|symbol|amount`). Imports use `INSERT OR IGNORE` — re-importing is counted as "skipped", never overwritten. Never bypass this to "fix" an import; preserve provenance and add a new import session instead.

## Portable runtime layout (created beside the executable, gitignored)

`config/ data/ cache/ logs/ exports/ plugins/ backups/` — there is no installer, no AppData, no registry. `AppPaths.EnsureCreated()` is the first thing `App.OnStartup` calls; `IAppPaths` is the only correct way to resolve these folders.

## Plugins

A plugin is a .NET assembly referencing `DepinTracker.Plugins.Abstractions`. `PluginLoader` loads each `.dll` from `plugins/` into its **own collectible `AssemblyLoadContext`**, but resolves shared contract assemblies (Abstractions + anything the host already loaded) from the host context — without this, the plugin's `IPlugin` is a different `Type` than the host's and registration fails silently. A plugin that throws on load is logged and skipped; it can never bring down the app.

The reference plugin (`samples/DepinTracker.Plugins.SampleCsvPrice`) is **built alongside the app but not linked** (`<ReferenceOutputAssembly>false</ReferenceOutputAssembly>`); the `CopySamplePlugin` target in `DepinTracker.App.csproj` drops its DLL + `prices.csv` into the output `plugins/` folder to exercise the real loader at runtime.

## Conventions that bite if ignored

- **Do not enable `InvariantGlobalization`** — it breaks WPF data binding (culture resolution). Determinism is instead enforced by using `CultureInfo.InvariantCulture` explicitly at every serialization/parse boundary. `Directory.Build.props` documents this.
- **Money is `decimal`.** Never `double`/`float`. Every parse/format passes `CultureInfo.InvariantCulture`.
- **`async` + `CancellationToken` on every I/O and provider method.** This is part of the public contract for plugin authors.
- **Providers return `null` on "no data"**, never throw or guess — engines fall through to the next provider on `null`.
- **Offline-first engines (`PriceEngine`, `ExchangeRateEngine`) read the cache first**, only call a provider on miss, and write the result back. New providers must respect this — don't bypass the cache.
- **File-scoped namespaces, `_camelCase` private fields, 4-space indent, CRLF.** Enforced by `.editorconfig`.
- **No extra packages without a clear need.** The dependency surface is deliberately small (see `PROJECT_SPECIFICATION.md` § Dependencies); MVVM base types and the file logger are hand-rolled rather than pulled from a toolkit.

## Tests

xUnit + FluentAssertions + Moq. Integration tests use the `TempStore` helper (`tests/DepinTracker.Tests/TestSupport/TempStore.cs`) which spins up the three real SQLite stores in a temp directory and runs all migrations — prefer this over mocking repositories so the actual schema and Dapper mapping are exercised. `IClock` is abstracted; use `TestClock` for time-sensitive tests.
