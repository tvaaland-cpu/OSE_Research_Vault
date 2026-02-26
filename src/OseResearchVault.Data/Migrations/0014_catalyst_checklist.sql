CREATE TABLE IF NOT EXISTS catalyst (
    catalyst_id TEXT PRIMARY KEY NOT NULL,
    workspace_id TEXT,
    company_id TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    expected_start TEXT,
    expected_end TEXT,
    status TEXT NOT NULL CHECK(status IN ('open','done','invalidated')),
    impact TEXT NOT NULL DEFAULT 'med' CHECK(impact IN ('low','med','high')),
    notes TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE SET NULL,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_catalyst_company_expected_start ON catalyst(company_id, expected_start);
CREATE INDEX IF NOT EXISTS idx_catalyst_company_status_impact ON catalyst(company_id, status, impact);

CREATE TABLE IF NOT EXISTS catalyst_snippet (
    workspace_id TEXT,
    catalyst_id TEXT NOT NULL,
    snippet_id TEXT NOT NULL,
    PRIMARY KEY (catalyst_id, snippet_id),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE SET NULL,
    FOREIGN KEY (catalyst_id) REFERENCES catalyst(catalyst_id) ON DELETE CASCADE,
    FOREIGN KEY (snippet_id) REFERENCES snippet(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_catalyst_snippet_snippet_id ON catalyst_snippet(snippet_id);
