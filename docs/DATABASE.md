# Database

Persistence is **SQLite + Dapper**, split across **three physical database files** that
mirror the conceptual data-origin separation (`DataOrigin` enum). Keeping them separate
means generated/cache data can be wiped and rebuilt without ever touching authoritative
imported data.

| Store | File | Origin | Contents |
| --- | --- | --- | --- |
| Config | `config/config.db` | Configuration | `projects`, `wallets`, `tags`, `project_tags`, `wallet_tags`, `settings` |
| Imported | `data/imported.db` | Imported (authoritative) | `import_sessions`, `raw_provider_responses`, `reward_transactions` |
| Generated | `cache/generated.db` | Generated (rebuildable) | `price_cache`, `fx_cache` |

Connections are opened per operation by `SqliteConnectionFactory` with
`PRAGMA foreign_keys = ON` and WAL journaling.

## Versioned migrations

Each store has its own `schema_version` table. `MigrationRunner` loads ordered,
**embedded** SQL scripts (`Persistence/Migrations/Scripts/<Store>/NNNN_name.sql`), and on
startup applies every script whose version exceeds the current one, each inside a
transaction. Re-running is idempotent. To evolve the schema, add `0002_*.sql` (etc.) to
the relevant store folder — never edit an applied script.

## Type mapping

SQLite has no native type for several CLR types, so Dapper type handlers
(`SqliteTypeHandlers`) store them as TEXT in a deterministic, culture-invariant form:

- `Guid` → canonical `D` string
- `DateTimeOffset` → ISO-8601 round-trip (`O`)
- `DateOnly` → `yyyy-MM-dd`
- `decimal` → invariant decimal string (exact; never REAL/float)

## No silent overwrite

`reward_transactions` has a unique `DedupKey` (`wallet|chain|txhash|symbol|amount`).
Imports use `INSERT OR IGNORE`, so re-importing the same data is skipped and counted,
never overwritten or double-counted.

## Backup & restore

`BackupService` checkpoints WAL, then zips all three database files (plus `-wal`/`-shm`
sidecars) with a `manifest.json` of SHA-256 hashes. Restore verifies every hash before
overwriting; verify checks an archive without touching live data.
