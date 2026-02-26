ALTER TABLE metric ADD COLUMN snippet_id TEXT;
ALTER TABLE metric ADD COLUMN period_label TEXT;
ALTER TABLE metric ADD COLUMN currency TEXT;

CREATE INDEX IF NOT EXISTS idx_metric_snippet_id ON metric(snippet_id);
