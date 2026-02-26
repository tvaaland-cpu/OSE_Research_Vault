CREATE TABLE IF NOT EXISTS automation (
    id TEXT PRIMARY KEY NOT NULL,
    workspace_id TEXT NOT NULL,
    name TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    schedule_type TEXT NOT NULL,
    interval_minutes INTEGER,
    daily_time TEXT,
    payload_type TEXT NOT NULL,
    agent_id TEXT,
    company_scope_mode TEXT NOT NULL DEFAULT 'global',
    company_scope_ids_json TEXT,
    query_text TEXT,
    next_run_at TEXT,
    last_run_at TEXT,
    last_status TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (agent_id) REFERENCES agent(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS automation_run (
    id TEXT PRIMARY KEY NOT NULL,
    automation_id TEXT NOT NULL,
    trigger_type TEXT NOT NULL,
    status TEXT NOT NULL,
    started_at TEXT NOT NULL,
    finished_at TEXT,
    error TEXT,
    FOREIGN KEY (automation_id) REFERENCES automation(id) ON DELETE CASCADE
);
