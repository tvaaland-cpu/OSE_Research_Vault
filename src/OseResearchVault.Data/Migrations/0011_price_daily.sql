CREATE TABLE IF NOT EXISTS price_daily (
  price_id TEXT PRIMARY KEY,
  workspace_id TEXT,
  company_id TEXT NOT NULL,
  price_date TEXT NOT NULL,
  close REAL NOT NULL,
  currency TEXT NOT NULL DEFAULT 'NOK',
  source_id TEXT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE(workspace_id, company_id, price_date)
);

CREATE INDEX IF NOT EXISTS idx_price_daily_company_date ON price_daily(company_id, price_date DESC);
