# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial foundation: Clean-Architecture .NET 9 / WPF solution
  (Domain, Plugins.Abstractions, Application, Infrastructure, App, sample plugin, tests).
- Portable folder layout (config/data/cache/logs/exports/plugins/backups).
- Three-store SQLite persistence with versioned, embedded-SQL migrations.
- Plugin system with six extension interfaces and isolated load contexts.
- Built-in providers: CoinGecko + DeFiLlama (prices), Frankfurter/ECB (FX),
  Blockscout (EVM explorer). DeFiLlama is consulted first for tokens with a known
  contract so long-tail DePIN tokens resolve without a hand-maintained symbol map.
- Offline-first price/FX engines with cache write-back.
- Reward import (manual + on-chain) with provenance and dedupe; valuation with
  explicit missing-price handling.
- Dashboard with portfolio metrics and a monthly rewards chart (ScottPlot).
- Transactions page: flat, date-sorted view of every imported reward with its
  per-row fiat valuation.
- Project/wallet management with tags and activation.
- Backup/restore/verify; CSV + Excel report exporters.
- xUnit test suite and GitHub Actions CI with code coverage.

### Changed
- `IPriceProvider.CanResolve` / `GetHistoricalPriceAsync` now also receive the
  reward's `blockchainKey` and `tokenContract` (both nullable). Symbol-only providers
  can ignore them; contract-aware providers (DeFiLlama) use them to resolve tokens
  without symbol maps.
