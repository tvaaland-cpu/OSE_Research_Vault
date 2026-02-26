CREATE TABLE IF NOT EXISTS share_log (
  share_log_id TEXT PRIMARY KEY,
  workspace_id TEXT,
  action TEXT NOT NULL,
  target_company_id TEXT,
  profile_id TEXT,
  output_path TEXT NOT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  summary TEXT,
  FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE SET NULL,
  FOREIGN KEY (target_company_id) REFERENCES company(id) ON DELETE SET NULL,
  FOREIGN KEY (profile_id) REFERENCES export_profile(profile_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_share_log_created_at ON share_log(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_share_log_workspace ON share_log(workspace_id);
