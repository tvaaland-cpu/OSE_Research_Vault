CREATE TABLE IF NOT EXISTS trade (
    trade_id TEXT PRIMARY KEY NOT NULL,
    workspace_id TEXT,
    company_id TEXT NOT NULL,
    position_id TEXT,
    trade_date TEXT NOT NULL,
    side TEXT NOT NULL CHECK(side IN ('buy','sell')),
    quantity REAL NOT NULL,
    price REAL NOT NULL,
    fee REAL DEFAULT 0,
    currency TEXT NOT NULL DEFAULT 'NOK',
    note TEXT,
    source_id TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE,
    FOREIGN KEY (company_id) REFERENCES company(id) ON DELETE CASCADE,
    FOREIGN KEY (position_id) REFERENCES position(id) ON DELETE SET NULL,
    FOREIGN KEY (source_id) REFERENCES source(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_trade_workspace_company ON trade(workspace_id, company_id);
CREATE INDEX IF NOT EXISTS idx_trade_workspace_trade_date ON trade(workspace_id, trade_date);
