-- Persist the on-chain 'from' address of each reward for audit and so future
-- re-imports can apply (or revoke) per-project source-address filters.

ALTER TABLE reward_transactions ADD COLUMN FromAddress TEXT NULL;

CREATE INDEX ix_rewards_from_address ON reward_transactions (FromAddress);
