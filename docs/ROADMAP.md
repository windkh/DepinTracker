# Roadmap

The current release is the **foundation + reward-tracking vertical slice + tax report**.
This file tracks what is shipped versus what is still scaffolded.

## Implemented

- Clean-Architecture solution (Domain / Abstractions / Application / Infrastructure / App)
- Portable folder layout; no installer / AppData / registry
- Three-store SQLite persistence (config / imported / generated) with versioned migrations
- Plugin system: discovery, isolated load contexts, six extension interfaces, sample plugin
- Built-in providers:
  - **Prices:** DeFiLlama (contract-keyed, primary) → CoinGecko (symbol-keyed, fallback)
  - **FX:** Frankfurter / ECB
  - **EVM explorer:** Etherscan v2 unified API (Ethereum, Polygon, Optimism, Base, Arbitrum, BNB, Gnosis)
  - **Solana explorer:** Helius enhanced-transactions API with batched token-metadata
- Offline-first price/FX engines with local cache write-back; per-row HTTP + valuation logging
- Reward import (manual + on-chain) with provenance, no-overwrite dedupe, and per-row raw-data trail
- Per-project source-address allow-list filter (rejects unrelated airdrops at import time)
- "Import all active wallets" multi-wallet path with truncation warning
- Valuation (price × FX → reporting currency) with explicit missing-price handling
- Backfill engine for `TokenContract` / `FromAddress` from saved raw responses
- Dashboard:
  - Headline portfolio + counts cards
  - Rewards-value-by-month chart + tokens-per-month chart (one bar per token)
  - Per-year income table; clicking a year filters both charts; locked Y-axis ≥ 0
- Transactions page with `Date · Token · Amount · From · Unit price · FX rate · Fiat value`
  and a tooltip showing the full `amount × price × FX = EUR` calculation
- Project / wallet management — create, edit, delete, activate, plus per-project
  "Clear imported data" that wipes rewards + raw responses + sessions in one transaction
- App-wide **project scope** picker in the nav rail drives Dashboard / Transactions /
  Reports / Import in one click; persisted across runs
- User-settings store (config.db) + Settings page editors for the Etherscan and Helius API keys
- Tax report (German Finanzamt) — year + project scoped, **HTML / DOCX / XLSX / CSV**,
  Save-as dialog, "Methodik & Quellen" methodology section, omits wallet/source addresses
- **FIFO disposal tracking & Veräußerungsgewinne.** Manual disposal entry
  (sale / swap / spend / transfer-out / loss), FIFO matcher splits reward lots
  per token, cost-basis-per-unit from the reward valuation, realised gain in
  EUR and §23-EStG holding-period flag (≥1 year ⇒ steuerfrei). Tax report
  gains a *Veräußerungsgewinne (FIFO)* section that appears only when the year
  has disposals.
- Backup / restore / verify (hashed zip of the three stores)
- Auto-versioning from `git rev-list --count HEAD` stamped into the window title
- xUnit test suite + GitHub Actions CI with coverage

## Next

- **On-chain disposal detection.** Both Etherscan v2 and Helius already return
  outgoing transfers; persist them as `DispositionKind.TransferOut` so FIFO
  doesn't depend on manual entry. DEX-swap recognition (router-call →
  outgoing leg + incoming counter-leg in the same tx) is a follow-up.
- **Rebuild engine.** Re-derive `imported.db` and `generated.db` from saved
  raw responses + online sources (per project / wallet / chain / year). Stores
  are already separated to make this clean; useful whenever an upstream source
  corrects historical data.
- **Holdings-over-time analytics.** Average acquisition cost per token,
  holdings value vs cost chart per project. Application-layer compute over
  existing data.
- **Settings UI for runtime knobs.** Editable reporting currency, provider
  priorities, and the symbol→CoinGecko-id map so power users don't have to
  edit `appsettings.json`.

## Later

- Native PDF exporter (today: HTML → browser "Save as PDF" or DOCX → Word "Export as PDF")
- Scheduled background sync
- Multi-currency reporting
- Import from CSV statements
- More chains beyond EVM + Solana (Cosmos, etc.) — interface is open, needs explorer plugins
- Localization (UI is English today; tax report already German)
