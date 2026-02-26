CREATE TABLE IF NOT EXISTS thesis_version (
    thesis_version_id TEXT PRIMARY KEY,
    workspace_id TEXT,
    company_id TEXT NOT NULL,
    position_id TEXT,
    title TEXT NOT NULL DEFAULT 'Thesis',
    body TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    created_by TEXT NOT NULL DEFAULT 'user',
    source_note_id TEXT,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE,
    FOREIGN KEY (position_id) REFERENCES position(id) ON DELETE SET NULL,
    FOREIGN KEY (source_note_id) REFERENCES note(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_thesis_version_company_created_at
    ON thesis_version(company_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_thesis_version_position_id
    ON thesis_version(position_id);
