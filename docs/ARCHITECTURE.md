# Architecture

DePIN Tracker follows **Clean Architecture**: dependencies point inward, the domain
knows nothing about infrastructure, and everything crosses boundaries through interfaces.

```
┌─────────────────────────────────────────────────────────┐
│ DepinTracker.App (WPF / MVVM / DI host)                   │
│   composition root, views, view models                    │
└───────────────┬───────────────────────────────────────────┘
                │ depends on
┌───────────────▼───────────────────────────────────────────┐
│ DepinTracker.Infrastructure                                │
│   SQLite/Dapper repos, migrations, HTTP providers,         │
│   plugin loader, backup, file logging                      │
└───────────────┬───────────────────────────────────────────┘
                │ implements ports of
┌───────────────▼───────────────────────────────────────────┐
│ DepinTracker.Application                                   │
│   use-case services, repository/provider PORTS, DTOs       │
└───────────────┬───────────────────────────────────────────┘
                │ uses
┌───────────────▼───────────────────────────────────────────┐
│ DepinTracker.Domain          DepinTracker.Plugins.Abstractions│
│   entities, value objects      the six plugin interfaces      │
└─────────────────────────────────────────────────────────────┘
```

## Layers

- **Domain** — pure model: `Project`, `Wallet`, `Blockchain`, `RewardTransaction`,
  `ImportSession`, `RawProviderResponse`, `PricePoint`, `ExchangeRate`, `Holding`, `Tag`;
  value objects `Money`, `TokenAmount`, `DateRange`; enums incl. `DataOrigin`. No external deps.
- **Plugins.Abstractions** — `IBlockchainExplorer`, `IPriceProvider`,
  `IExchangeRateProvider`, `IRewardClassifier`, `IAnalyticsProvider`, `IReportExporter`,
  plus `IPlugin` + `PluginManifest`. All methods are async and take a `CancellationToken`.
- **Application** — orchestration only. Repository/cache ports (`IProjectRepository`,
  `IRewardRepository`, `IPriceCache`, …), services (`ProjectService`, `WalletService`,
  `RewardImportService`, `PriceEngine`, `ExchangeRateEngine`, `ValuationService`,
  `AnalyticsService`, `DashboardService`), and DTOs. Depends only on Domain + Abstractions.
- **Infrastructure** — concrete adapters: Dapper repositories over three SQLite stores,
  the embedded-SQL `MigrationRunner`, HTTP providers (CoinGecko/Frankfurter/Blockscout),
  the `PluginLoader`, `BackupService`, and a minimal file logger.
- **App** — WPF shell. A generic host wires configuration → logging → DI, loads plugins,
  migrates the databases, and shows the MVVM shell. MVVM primitives are hand-rolled.

## Key decisions

- **Determinism over ambient culture.** Every serialization/parse boundary uses
  `CultureInfo.InvariantCulture` and `decimal` (never floating point) for money.
  `InvariantGlobalization` is deliberately *not* enabled because it breaks WPF binding.
- **Dependency surface matches the spec.** MVVM base types and the file logger are
  hand-rolled rather than pulling in extra packages.
- **Offline-first valuation.** `PriceEngine`/`ExchangeRateEngine` read the local cache
  first and only call a provider on a miss, writing the result back — so the database can
  always be rebuilt from online sources but normally runs fully offline.
- **Provenance & no silent overwrite.** Imported rewards carry their tx hash, block,
  provider and import session; writes are insert-or-ignore on a unique dedup key.
- **Testability.** `IClock` abstracts time; engines accept provider collections so they
  can be unit-tested with fakes/mocks.
