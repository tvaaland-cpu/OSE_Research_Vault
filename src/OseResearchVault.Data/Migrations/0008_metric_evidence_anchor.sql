PRAGMA foreign_keys = OFF;

ALTER TABLE metric RENAME TO metric_legacy;

CREATE TABLE IF NOT EXISTS metric (
    metric_id TEXT PRIMARY KEY NOT NULL,
    workspace_id TEXT NOT NULL,
    company_id TEXT NOT NULL,
    metric_name TEXT NOT NULL,
    period TEXT NOT NULL,
    value REAL NOT NULL,
    unit TEXT,
    currency TEXT,
    snippet_id TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE,
    FOREIGN KEY (snippet_id) REFERENCES snippet(id) ON DELETE RESTRICT
);

DROP TABLE metric_legacy;

CREATE INDEX IF NOT EXISTS idx_metric_workspace_id ON metric(workspace_id);
CREATE INDEX IF NOT EXISTS idx_metric_company_id ON metric(company_id);
CREATE INDEX IF NOT EXISTS idx_metric_company_name ON metric(company_id, metric_name);
CREATE INDEX IF NOT EXISTS idx_metric_snippet_id ON metric(snippet_id);

PRAGMA foreign_keys = ON;
