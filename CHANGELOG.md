# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-06-30

First public milestone. Foundation + reward-tracking vertical slice + tax
report with FIFO disposal tracking.

### Added
- Clean-Architecture .NET 9 / WPF solution: Domain / Plugins.Abstractions /
  Application / Infrastructure / App with sample plugin and xUnit suite.
- Portable folder layout (config / data / cache / logs / exports / plugins /
  backups) — no installer, no AppData, no registry.
- Three-store SQLite persistence with versioned, embedded-SQL migrations.
- Plugin system with isolated load contexts and six extension interfaces.
- Built-in providers:
  - Prices: DeFiLlama (contract-keyed, primary) → CoinGecko (symbol-keyed,
    fallback). Offline-first cache write-back.
  - FX: Frankfurter / ECB.
  - EVM explorer: Etherscan v2 unified API (Ethereum, Polygon, Optimism,
    Base, Arbitrum, BNB, Gnosis) — single key, configurable in Settings.
  - Solana explorer: Helius enhanced-transactions API with batched
    token-metadata symbol resolution.
- Reward import (manual + on-chain) with provenance, no-overwrite dedupe and
  per-row raw-data trail.
- Per-project allow-list of source addresses — only transfers from approved
  distributors are kept as rewards.
- "Import all active wallets" multi-wallet path with truncation warning.
- Valuation (price × FX → reporting currency) with explicit missing-price
  handling and tooltip showing the full `amount × price × FX = EUR` math.
- Dashboard with portfolio metrics, monthly fiat chart, tokens-per-month
  grouped bar chart, per-year income table; clicking a year filters the
  charts; Y-axis pinned to ≥ 0.
- Transactions page: flat, date-sorted view of every imported reward with
  Date / Token / Amount / From / Unit price / FX rate / Fiat value columns.
- Project / wallet management — create, edit, delete, activate, plus
  per-project "Clear imported data" that wipes rewards + raw responses +
  sessions in one transaction.
- App-wide **project scope** picker in the nav rail drives Dashboard /
  Transactions / Reports / Import in one click. Persisted across runs.
- User-settings store (config.db) + Settings page editors for the
  Etherscan and Helius API keys.
- Tax report for the German Finanzamt — year + project scoped, **HTML /
  DOCX / XLSX / CSV**, Save dialog destination picker, "Methodik & Quellen"
  methodology section, German labels, EUR totals, monthly + per-token
  breakdowns. Wallet and source addresses are never included.
- **FIFO disposal tracking & Veräußerungsgewinne.** Manual disposal entry
  (sale / swap / spend / transfer-out / loss). Pure FIFO matcher splits
  reward lots per token, cost-basis-per-unit from the reward valuation,
  realised gain in EUR, §23-EStG holding-period flag (≥ 1 year ⇒
  steuerfrei). Tax report adds a *Veräußerungsgewinne (FIFO)* section that
  appears only when the year has disposals.
- Backup / restore / verify of the three stores (hashed zip with a manifest).
- Auto-versioning from `git rev-list --count HEAD` stamped into the window
  title (`DePIN Tracker {Major}.{Minor}.{count}+{sha}`).
- GitHub Actions CI with build + tests on `windows-latest` + coverage.

### Documentation
- README "Getting started" walkthrough with concrete GEODNET (Polygon)
  filter address and a generalisable method for finding the ONOCOY (Solana)
  distributor address.
- CLAUDE.md, ROADMAP, ARCHITECTURE, DATABASE, PLUGIN_GUIDE, SECURITY, and
  CONTRIBUTING docs.

### Conventions
- `IPriceProvider.CanResolve` / `GetHistoricalPriceAsync` receive the
  reward's `blockchainKey` and `tokenContract` so contract-aware providers
  resolve long-tail DePIN tokens without a hand-maintained symbol map.
- Line-ending policy pinned to LF everywhere via `.gitattributes` and
  `.editorconfig`.
