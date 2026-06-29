# Roadmap

The current release is the **foundation + one vertical slice**. This roadmap tracks what
is implemented versus what is scaffolded as an extension point.

## Implemented

- Clean-Architecture solution (Domain / Abstractions / Application / Infrastructure / App)
- Portable folder layout; no installer/AppData/registry
- Three-store SQLite persistence (config / imported / generated) with versioned migrations
- Plugin system: discovery, isolated load contexts, six extension interfaces, sample plugin
- Real keyless HTTP providers: CoinGecko (prices), Frankfurter/ECB (FX), Blockscout (EVM explorer)
- Offline-first price/FX engines with local cache write-back
- Reward import (manual + on-chain) with provenance and no-overwrite dedupe
- Valuation (price × FX → reporting currency) with explicit missing-price handling
- Dashboard (portfolio, counts, missing prices, sync/health, monthly rewards chart)
- Projects/wallets management with tags and activation
- Backup/restore/verify (hashed zip of the stores)
- CSV + Excel (Open XML) report exporters
- xUnit test suite + GitHub Actions CI with coverage

## Next (scaffolded as interfaces / partial)

- **Tax engine:** FIFO lot tracking, yearly reports, gains/losses. Architecture is
  FIFO-ready; the report model and exporters exist.
- **PDF export:** a third `IReportExporter` (CSV + XLSX are implemented).
- **Rebuild engine:** rebuild DB / wallet / project / chain / year / prices / FX from
  imported data + online sources. Stores are already separated to make this clean.
- **More chains:** additional `IBlockchainExplorer` plugins (Solana, Cosmos, …) and
  native-token reward parsing.
- **Analytics:** richer `IAnalyticsProvider`s (holdings value over time, average
  acquisition cost, per-project/per-wallet breakdowns).
- **Settings UI:** editable reporting currency, provider priorities, token-id mappings.

## Later

- Scheduled background sync
- Multi-currency reporting
- Import from CSV statements
- Localization
