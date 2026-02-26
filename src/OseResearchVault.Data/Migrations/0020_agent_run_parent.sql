ALTER TABLE agent_run ADD COLUMN parent_run_id TEXT;
CREATE INDEX IF NOT EXISTS idx_agent_run_parent_run_id ON agent_run(parent_run_id);
