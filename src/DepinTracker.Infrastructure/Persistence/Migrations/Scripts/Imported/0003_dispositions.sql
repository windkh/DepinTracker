-- Dispositions: sells, swaps, transfers-out, or any event that reduces a token
-- holding. Tracked separately from reward_transactions because they have their
-- own provenance and the FIFO matcher consumes them against the reward stream.
-- Amounts are stored as TEXT (invariant decimal) like reward_transactions.

CREATE TABLE dispositions (
    Id              TEXT PRIMARY KEY,
    WalletId        TEXT NULL,
    BlockchainKey   TEXT NULL,
    TxHash          TEXT NOT NULL DEFAULT '',
    TimestampUtc    TEXT NOT NULL,
    TokenSymbol     TEXT NOT NULL,
    TokenContract   TEXT NULL,
    Amount          TEXT NOT NULL,
    -- Per-unit proceeds in the report's price currency at disposal time. Null
    -- means "no proceeds recorded" (e.g. a transfer to your own cold wallet);
    -- such rows are still useful as a holdings reduction and the FIFO matcher
    -- treats them as zero-proceeds disposals.
    ProceedsPerUnit TEXT NULL,
    ProceedsCurrency TEXT NULL,
    Kind            INTEGER NOT NULL,
    ProviderKey     TEXT NOT NULL,
    ImportSessionId TEXT NOT NULL,
    Notes           TEXT NULL,
    CreatedUtc      TEXT NOT NULL,
    -- Natural identity guards against double-counting a disposal across imports.
    DedupKey        TEXT NOT NULL UNIQUE
);

CREATE INDEX ix_dispositions_token_time ON dispositions (TokenSymbol, TimestampUtc);
CREATE INDEX ix_dispositions_wallet ON dispositions (WalletId);
