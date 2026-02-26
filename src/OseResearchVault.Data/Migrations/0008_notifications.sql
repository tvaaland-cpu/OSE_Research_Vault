CREATE TABLE IF NOT EXISTS notification (
    notification_id TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    level TEXT NOT NULL,
    title TEXT NOT NULL,
    body TEXT NOT NULL,
    created_at TEXT NOT NULL,
    is_read INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (workspace_id) REFERENCES workspace(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_notification_workspace_created
    ON notification(workspace_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_notification_workspace_unread
    ON notification(workspace_id, is_read);
