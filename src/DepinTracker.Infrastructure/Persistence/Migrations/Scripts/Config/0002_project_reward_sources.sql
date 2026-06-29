-- Per-project allow-list of on-chain source addresses that count as rewards.
-- Empty list (no rows for a project) means "import everything" (back-compat).
-- Addresses are stored lowercased so EIP-55 mixed-case copies compare equal.

CREATE TABLE project_reward_sources (
    ProjectId TEXT NOT NULL,
    Address   TEXT NOT NULL,
    PRIMARY KEY (ProjectId, Address),
    FOREIGN KEY (ProjectId) REFERENCES projects (Id) ON DELETE CASCADE
);

CREATE INDEX ix_project_reward_sources_address ON project_reward_sources (Address);
