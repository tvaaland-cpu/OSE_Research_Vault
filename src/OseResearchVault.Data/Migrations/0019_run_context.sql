CREATE TABLE IF NOT EXISTS run_context (
    run_context_id TEXT PRIMARY KEY,
    workspace_id TEXT,
    run_id TEXT NOT NULL,
    context_json TEXT NOT NULL,
    prompt_text TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE SET NULL,
    FOREIGN KEY (run_id) REFERENCES agent_run(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_run_context_run_id ON run_context(run_id);
