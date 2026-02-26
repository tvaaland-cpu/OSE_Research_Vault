CREATE TABLE IF NOT EXISTS scenario (
    scenario_id TEXT PRIMARY KEY,
    workspace_id TEXT,
    company_id TEXT NOT NULL,
    name TEXT NOT NULL,
    probability REAL NOT NULL CHECK(probability >= 0 AND probability <= 1),
    assumptions TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_scenario_company ON scenario(company_id);

CREATE TABLE IF NOT EXISTS scenario_kpi (
    scenario_kpi_id TEXT PRIMARY KEY,
    workspace_id TEXT,
    scenario_id TEXT NOT NULL,
    kpi_name TEXT NOT NULL,
    period TEXT NOT NULL,
    value REAL NOT NULL,
    unit TEXT,
    currency TEXT,
    snippet_id TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (scenario_id) REFERENCES scenario(scenario_id) ON DELETE CASCADE,
    FOREIGN KEY (snippet_id) REFERENCES snippet(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_scenario_kpi_scenario ON scenario_kpi(scenario_id);
CREATE INDEX IF NOT EXISTS idx_scenario_kpi_snippet ON scenario_kpi(snippet_id);
