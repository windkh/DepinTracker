# Security Policy

## Reporting a vulnerability

Please report security issues privately to the maintainers rather than opening a public
issue. Include steps to reproduce, affected versions, and any relevant logs (with secrets
redacted). We aim to acknowledge reports promptly and to coordinate disclosure.

## Security posture

DePIN Tracker is **offline-first and portable** by design, which shapes its threat model:

- **No secrets required.** The bundled providers (CoinGecko, Frankfurter/ECB, Blockscout)
  are keyless. If you add a provider that needs an API key, store it in
  `config/appsettings.json` (or an environment variable prefixed `DEPIN_`) — never commit it.
- **Local data only.** All data lives next to the executable under `config/`, `data/`,
  `cache/`. Nothing is sent anywhere except outbound HTTPS calls to the configured
  price/FX/explorer endpoints. The portable folders are git-ignored.
- **Read-only on-chain access.** The app reads public blockchain data via explorers; it
  never holds private keys, signs, or broadcasts transactions.
- **Plugins are code.** A plugin runs with full trust inside the app. Only install plugins
  you trust; review their source and manifest (shown under Settings → Plugins).
- **Auditability.** Imported financial data preserves raw provider responses and import
  provenance and is never silently overwritten, so tampering is detectable.

## Backups

Backups are SHA-256-hashed zip archives; restore verifies integrity before overwriting.
Store backups somewhere appropriate to the sensitivity of your financial data.
