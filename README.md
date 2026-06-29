# DePIN Tracker

A production-grade, **offline-first, portable Windows desktop application** for tracking
DePIN rewards, managing wallets, producing analytics, and generating reproducible tax
reports. Built on .NET 9 / WPF / MVVM with Clean Architecture and a plugin system.

> Status: **foundation + one working vertical slice**. The architecture, persistence,
> plugin system, providers, and an end-to-end flow (create project → add wallet →
> import rewards → value them → dashboard) are implemented. Remaining features
> (full tax/FIFO engine, rebuild engine, PDF export, more chains) are wired as
> interfaces with clear extension points — see [ROADMAP](docs/ROADMAP.md).

## Core principles

- Offline-first and portable (no installer, no AppData, no registry — everything lives next to the executable)
- Clean Architecture, generic (no project-specific logic), extensible through plugins
- Deterministic, reproducible calculations; the database is rebuildable from online sources
- Auditability over convenience — imported financial data is never silently overwritten

## Requirements

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download) (pinned via `global.json`)
- Opens in **Visual Studio 2026** (open `DepinTracker.sln`) or **VS Code** (C# Dev Kit)

## Build & run

```sh
dotnet build DepinTracker.sln                 # build everything
dotnet test                                   # run the test suite
dotnet run --project src/DepinTracker.App     # launch the app
```

On first launch the app creates its portable folder layout next to the executable and
migrates three SQLite databases. From VS Code, use the bundled **build** / **test** /
**run-app** tasks and the *Launch DePIN Tracker (WPF)* debug configuration.

## Portable folder layout

Created automatically beside the executable; none of it is committed:

```
config/   user configuration + appsettings.json + config.db
data/     imported.db (authoritative imported financial data)
cache/    generated.db (rebuildable price/FX caches)
logs/     daily log files
exports/  generated reports
plugins/  drop-in plugin assemblies (discovered at startup)
backups/  verified backup archives
```

## Architecture at a glance

| Project | Responsibility |
| --- | --- |
| `DepinTracker.Domain` | Entities, value objects, enums. No dependencies. |
| `DepinTracker.Plugins.Abstractions` | The six plugin interfaces + `IPlugin`. |
| `DepinTracker.Application` | Use-case services, repository/provider ports, DTOs. |
| `DepinTracker.Infrastructure` | SQLite/Dapper, migrations, HTTP providers, plugin loader, backup. |
| `DepinTracker.App` | WPF MVVM shell, DI host, ScottPlot dashboard. |

See [ARCHITECTURE](docs/ARCHITECTURE.md), [DATABASE](docs/DATABASE.md), and
[PLUGIN_GUIDE](docs/PLUGIN_GUIDE.md) for detail.

## Built-in providers

Real, keyless HTTP providers (all offline-tolerant — cache first, network on miss):

- **Prices:** CoinGecko (`coingecko`) + the bundled offline CSV sample plugin
- **Exchange rates:** Frankfurter / ECB (`frankfurter`)
- **On-chain explorer:** Blockscout v2 (`blockscout`) for Ethereum, Polygon, Optimism, Base, Gnosis

## License

[Business Source License 1.1](LICENSE) — source-available, converting to Apache 2.0 four
years after each release. See the LICENSE file for the Additional Use Grant.
