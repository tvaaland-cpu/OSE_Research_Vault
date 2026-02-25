ALTER TABLE note ADD COLUMN note_type TEXT NOT NULL DEFAULT 'manual';
ALTER TABLE artifact ADD COLUMN content_format TEXT NOT NULL DEFAULT 'text';
ALTER TABLE artifact ADD COLUMN metadata_json TEXT;
