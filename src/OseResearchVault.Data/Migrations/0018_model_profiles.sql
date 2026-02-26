CREATE TABLE IF NOT EXISTS model_profile (
    model_profile_id TEXT PRIMARY KEY,
    workspace_id TEXT,
    name TEXT NOT NULL,
    provider TEXT NOT NULL,
    model TEXT NOT NULL,
    parameters_json TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    is_default INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE
);

ALTER TABLE agent ADD COLUMN model_profile_id TEXT;
ALTER TABLE agent_run ADD COLUMN model_profile_id TEXT;

CREATE INDEX IF NOT EXISTS idx_model_profile_workspace_id ON model_profile(workspace_id);
CREATE INDEX IF NOT EXISTS idx_model_profile_workspace_default ON model_profile(workspace_id, is_default);
CREATE INDEX IF NOT EXISTS idx_agent_model_profile_id ON agent(model_profile_id);
CREATE INDEX IF NOT EXISTS idx_agent_run_model_profile_id ON agent_run(model_profile_id);
