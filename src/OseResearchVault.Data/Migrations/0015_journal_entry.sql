CREATE TABLE IF NOT EXISTS journal_entry (
    journal_entry_id TEXT PRIMARY KEY NOT NULL,
    workspace_id TEXT,
    company_id TEXT NOT NULL,
    position_id TEXT,
    action TEXT NOT NULL CHECK(action IN ('buy','sell','hold','add','reduce')),
    entry_date TEXT NOT NULL,
    rationale TEXT NOT NULL,
    expected_outcome TEXT,
    review_date TEXT,
    review_notes TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE,
    FOREIGN KEY (position_id) REFERENCES position(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_journal_entry_company_date ON journal_entry(company_id, entry_date DESC);
CREATE INDEX IF NOT EXISTS idx_journal_entry_review_due ON journal_entry(review_date);

CREATE TABLE IF NOT EXISTS journal_trade (
    workspace_id TEXT,
    journal_entry_id TEXT NOT NULL,
    trade_id TEXT NOT NULL,
    PRIMARY KEY (journal_entry_id, trade_id),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (journal_entry_id) REFERENCES journal_entry(journal_entry_id) ON DELETE CASCADE,
    FOREIGN KEY (trade_id) REFERENCES trade(trade_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS journal_snippet (
    workspace_id TEXT,
    journal_entry_id TEXT NOT NULL,
    snippet_id TEXT NOT NULL,
    PRIMARY KEY (journal_entry_id, snippet_id),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (journal_entry_id) REFERENCES journal_entry(journal_entry_id) ON DELETE CASCADE,
    FOREIGN KEY (snippet_id) REFERENCES snippet(id) ON DELETE CASCADE
);
