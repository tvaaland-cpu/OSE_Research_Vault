CREATE TABLE IF NOT EXISTS automation (
    automation_id TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    name TEXT NOT NULL,
    is_enabled INTEGER NOT NULL DEFAULT 1,
    schedule_type TEXT NOT NULL,
    interval_minutes INTEGER,
    daily_time TEXT,
    last_run_at TEXT,
    next_run_at TEXT,
    payload_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS automation_run (
    automation_run_id TEXT PRIMARY KEY,
    automation_id TEXT NOT NULL,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    status TEXT NOT NULL,
    error TEXT,
    created_run_id TEXT,
    FOREIGN KEY (automation_id) REFERENCES automation(automation_id) ON DELETE CASCADE,
    FOREIGN KEY (created_run_id) REFERENCES agent_run(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_automation_enabled_next_run
    ON automation(is_enabled, next_run_at);

CREATE INDEX IF NOT EXISTS ix_automation_run_automation
    ON automation_run(automation_id, started_at);
