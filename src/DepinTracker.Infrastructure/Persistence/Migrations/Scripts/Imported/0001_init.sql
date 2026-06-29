-- Imported store schema v1: authoritative imported financial data + provenance.

CREATE TABLE import_sessions (
    Id            TEXT PRIMARY KEY,
    ProviderKey   TEXT NOT NULL,
    Source        INTEGER NOT NULL,
    Status        INTEGER NOT NULL,
    WalletId      TEXT NULL,
    StartedUtc    TEXT NOT NULL,
    CompletedUtc  TEXT NULL,
    ItemsImported INTEGER NOT NULL DEFAULT 0,
    ItemsSkipped  INTEGER NOT NULL DEFAULT 0,
    Message       TEXT NULL
);

CREATE TABLE raw_provider_responses (
    Id                 TEXT PRIMARY KEY,
    ImportSessionId    TEXT NOT NULL,
    ProviderKey        TEXT NOT NULL,
    RequestDescription TEXT NOT NULL,
    ResponseBody       TEXT NOT NULL,
    RetrievedUtc       TEXT NOT NULL
);

CREATE TABLE reward_transactions (
    Id              TEXT PRIMARY KEY,
    WalletId        TEXT NOT NULL,
    BlockchainKey   TEXT NOT NULL,
    TxHash          TEXT NOT NULL,
    BlockNumber     INTEGER NULL,
    TimestampUtc    TEXT NOT NULL,
    TokenSymbol     TEXT NOT NULL,
    TokenContract   TEXT NULL,
    -- Amount stored as TEXT to preserve exact decimal precision (SQLite has no decimal type).
    Amount          TEXT NOT NULL,
    Kind            INTEGER NOT NULL,
    ProviderKey     TEXT NOT NULL,
    ImportSessionId TEXT NOT NULL,
    RawResponseId   TEXT NULL,
    CreatedUtc      TEXT NOT NULL,
    -- Natural identity; the UNIQUE constraint backs insert-or-ignore dedupe so
    -- imported financial data is never silently overwritten or double-counted.
    DedupKey        TEXT NOT NULL UNIQUE
);

CREATE INDEX ix_rewards_wallet ON reward_transactions (WalletId);
CREATE INDEX ix_rewards_timestamp ON reward_transactions (TimestampUtc);
