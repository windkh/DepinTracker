-- Generated store schema v1: rebuildable price/FX caches. Safe to wipe and rebuild.

CREATE TABLE price_cache (
    TokenId      TEXT NOT NULL,
    Date         TEXT NOT NULL,
    Currency     TEXT NOT NULL,
    Price        TEXT NOT NULL,
    ProviderKey  TEXT NOT NULL,
    RetrievedUtc TEXT NOT NULL,
    PRIMARY KEY (TokenId, Date, Currency)
);

CREATE TABLE fx_cache (
    BaseCurrency  TEXT NOT NULL,
    QuoteCurrency TEXT NOT NULL,
    Date          TEXT NOT NULL,
    Rate          TEXT NOT NULL,
    ProviderKey   TEXT NOT NULL,
    RetrievedUtc  TEXT NOT NULL,
    PRIMARY KEY (BaseCurrency, QuoteCurrency, Date)
);
