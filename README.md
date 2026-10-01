# DePIN Tracker

A production-grade, **offline-first, portable Windows desktop application** for tracking
DePIN rewards, managing wallets, producing analytics, and generating reproducible tax
reports. Built on .NET 10 / WPF / MVVM with Clean Architecture and a plugin system.

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
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned via `global.json`)
- Opens in **Visual Studio 2026** (open `DepinTracker.slnx`) or **VS Code** (C# Dev Kit)

## Build & run

```sh
dotnet build DepinTracker.slnx                 # build everything
dotnet test                                   # run the test suite
dotnet run --project src/DepinTracker.App     # launch the app
```

On first launch the app creates its portable folder layout next to the executable and
migrates three SQLite databases. From VS Code, use the bundled **build** / **test** /
**run-app** tasks and the *Launch DePIN Tracker (WPF)* debug configuration.

## Getting started

A guided walkthrough for your first project. Replace the example chain/address with
your own as you go — the steps don't change.

### 1. Add the explorer API keys (Settings page)

Open the **Settings** tab in the nav rail. The two API-key cards correspond to the
two built-in on-chain explorers; you only need the one(s) for the chain(s) you use.

- **Etherscan v2 API key** — covers every supported EVM chain (Ethereum, Polygon,
  Optimism, Base, Arbitrum, BNB, Gnosis) through one key. Free tier is enough for
  personal use: create one at <https://etherscan.io/apis>, paste it in, click *Save*.
- **Helius API key** — covers Solana. Free tier (≈100k credits/month) at
  <https://www.helius.dev>. Paste, *Save*.

Chains whose key isn't set are hidden from the *Add wallet* dropdown on the Projects
page, so you can't accidentally create a wallet you can't import from.

### 2. Create a project (Projects page)

A project is just a named grouping (e.g. "GEODNET" or "ONOCOY"). Type a name into
**New project name** → **Add project**. The project shows up in the **Project scope**
picker at the top of the nav rail — switching to it focuses the Dashboard,
Transactions, Reports, and Import pages on this project.

### 3. Add the reward-receiving wallet

In the Projects page's *Add wallet* panel:

- Pick **Chain** (e.g. *Polygon* for GEODNET, *Solana* for ONOCOY).
- Paste your wallet **Address**. Optional **Label** for readability.
- Click **Add wallet**.

### 4. Set the source-address filter (recommended)

DePIN reward contracts pay out from a fixed address. Limiting imports to that
address keeps unrelated airdrops, scams, and personal transfers out of your tax
calculation.

In the project's *Allowed source addresses* card, paste one address per line and
click *Save filter*. Leaving it blank means "accept every incoming token transfer"
(works, but you'll have to manually sort out noise later).

#### GEODNET (Polygon)

```
0x8fb9dd00b9a3d893da96d444817d0b77330d5478
```

This is the GEODNET reward distribution contract on Polygon. Every daily GEOD
payout originates here. Verify it on Polygonscan if you want — it shows hundreds
of outgoing GEOD transfers per day, one per active station.

#### ONOCOY (Solana) — how to find the distributor address yourself

ONOCOY's distribution address isn't hard-coded into the app on purpose: DePIN
projects sometimes migrate distributors, and you want to control which address you
trust. Two ways to find the current one:

- **Easiest — let the app tell you.** Save the project with an empty allow-list
  first, run **Import all active wallets** (Import page), then open the
  **Transactions** tab. Sort by date, look at the **From** column for the ONOCOY
  rows: the same address repeats on every payout — that's the distributor. Copy
  it into the project's allow-list, **Clear imported data** (Projects page), and
  re-import to filter cleanly.
- **Manual — verify on a public explorer.** Open <https://solscan.io> and paste
  your wallet address. Filter the SPL token transfers to ONOCOY. The `from`
  field on any incoming reward is the distributor. Cross-check that the same
  address shows up across multiple historical payouts — if it does, that's the
  one.

The same recipe works for any DePIN token, on any chain: the reward source is
always the address that pays *every* reward.

### 5. Import

Open the **Import** tab. Click **Import all active wallets** — the explorer
walks every active wallet in the current project scope, the per-project filter
drops anything not on the allow-list, prices flow through DeFiLlama →
CoinGecko, and FX flows through Frankfurter. Re-running is safe: imports are
deduplicated by `(wallet, chain, txhash, symbol, amount)` — already-imported
rows are skipped, not double-counted.

### 6. Cross-check on the Dashboard / Transactions pages

- **Dashboard** — portfolio value (rounded to 2 decimals), monthly fiat chart,
  monthly token chart, per-year income table (click a year to filter the charts).
- **Transactions** — every imported reward with `Date · Token · Amount · From ·
  Unit price · FX rate · Fiat value`. Hover the *Fiat value* cell to see the
  exact `amount × price × FX = EUR` calculation that produced it.

### 7. Record disposals (only if you sold, swapped, spent or transferred out)

DePIN rewards are taxed as ordinary income on the day they arrive — that's
what step 5–6 already covers. If you then **dispose** of those tokens (sell
them on an exchange, swap them on a DEX, spend them, transfer them to a
self-custody wallet, or lose them), §23 EStG kicks in: gains realised within
one year of acquisition are taxable, gains after ≥ 1 year are *steuerfrei*.

Open the **Import** tab and scroll to **Manual disposal entry**. Fill in:
*Token symbol · Amount disposed · Date · Kind* (Sale / Swap / Spend /
TransferOut / Loss) · *Proceeds per unit* in your reporting currency (leave
0 for transfers between your own wallets) · optional *Tx hash* and *Notes*.
Click **Add disposal**. The tax report will FIFO-match it against the
oldest unsold reward lots of that token automatically.

> On-chain disposal detection is on the roadmap — for now disposals are
> entered by hand. The dedup key is `wallet | chain | tx | symbol | amount |
> timestamp`, so adding the same disposal twice is harmless.

### 8. Generate the tax report

Open the **Reports** tab. The active project comes from the nav-rail scope —
no per-page picker. Pick the **Tax year**, choose a **Format** (HTML for
print-to-PDF, DOCX for Word, XLSX, CSV), tick *Include per-transaction detail*
if your Finanzamt asks for line items, click **Generate report**, and pick the
destination folder. Default filename is `{year}_{project}_report.{ext}`.

The report has German labels, EUR totals, monthly and per-token breakdowns,
a *Methodik & Quellen* section that documents data sources and the calculation
formula, and — when the year has disposals — a *Veräußerungsgewinne (FIFO)*
section with `Veräußerung · Anschaffung · Token · Art · Menge ·
Anschaffungskosten/Einheit · Erlös/Einheit · Gewinn (EUR) · Haltedauer ·
Steuerstatus` per matched lot. Wallet and source addresses are never included.

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

All offline-tolerant — local cache first, network on miss; successful lookups
are written back so the same query runs offline next time.

- **Prices:** DeFiLlama (`defillama`, contract-keyed — primary) → CoinGecko
  (`coingecko`, symbol-keyed — fallback). The bundled offline CSV sample plugin
  is loaded automatically and wins over both for any symbol it knows.
- **Exchange rates:** Frankfurter / ECB (`frankfurter`).
- **EVM explorer:** Etherscan v2 unified API (`etherscan-v2`) — one API key
  serves Ethereum, Polygon, Optimism, Base, Arbitrum, BNB, Gnosis through the
  `chainid` query parameter.
- **Solana explorer:** Helius enhanced-transactions API (`helius`) with batched
  token-metadata symbol resolution.

## License

[Business Source License 1.1](LICENSE) — source-available, converting to Apache 2.0 four
years after each release. See the LICENSE file for the Additional Use Grant.
