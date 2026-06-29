-- Config store schema v1: user-owned configuration data.

CREATE TABLE projects (
    Id          TEXT PRIMARY KEY,
    Name        TEXT NOT NULL,
    Description TEXT NULL,
    Notes       TEXT NULL,
    IsActive    INTEGER NOT NULL DEFAULT 1,
    CreatedUtc  TEXT NOT NULL
);

CREATE TABLE wallets (
    Id            TEXT PRIMARY KEY,
    ProjectId     TEXT NOT NULL,
    BlockchainKey TEXT NOT NULL,
    Address       TEXT NOT NULL,
    Label         TEXT NULL,
    Notes         TEXT NULL,
    IsActive      INTEGER NOT NULL DEFAULT 1,
    CreatedUtc    TEXT NOT NULL,
    FOREIGN KEY (ProjectId) REFERENCES projects (Id) ON DELETE CASCADE
);

CREATE TABLE tags (
    Id   TEXT PRIMARY KEY,
    Name TEXT NOT NULL UNIQUE
);

CREATE TABLE project_tags (
    ProjectId TEXT NOT NULL,
    TagId     TEXT NOT NULL,
    PRIMARY KEY (ProjectId, TagId),
    FOREIGN KEY (ProjectId) REFERENCES projects (Id) ON DELETE CASCADE,
    FOREIGN KEY (TagId) REFERENCES tags (Id) ON DELETE CASCADE
);

CREATE TABLE wallet_tags (
    WalletId TEXT NOT NULL,
    TagId    TEXT NOT NULL,
    PRIMARY KEY (WalletId, TagId),
    FOREIGN KEY (WalletId) REFERENCES wallets (Id) ON DELETE CASCADE,
    FOREIGN KEY (TagId) REFERENCES tags (Id) ON DELETE CASCADE
);

CREATE TABLE settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE INDEX ix_wallets_project ON wallets (ProjectId);
