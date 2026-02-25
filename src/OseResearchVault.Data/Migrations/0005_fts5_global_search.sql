DROP TABLE IF EXISTS note_fts;
DROP TABLE IF EXISTS document_text_fts;
DROP TABLE IF EXISTS snippet_fts;
DROP TABLE IF EXISTS artifact_fts;

CREATE VIRTUAL TABLE IF NOT EXISTS note_fts USING fts5(
    id UNINDEXED,
    title,
    body
);

CREATE VIRTUAL TABLE IF NOT EXISTS document_text_fts USING fts5(
    id UNINDEXED,
    title,
    content
);

CREATE VIRTUAL TABLE IF NOT EXISTS snippet_fts USING fts5(
    id UNINDEXED,
    text
);

CREATE VIRTUAL TABLE IF NOT EXISTS artifact_fts USING fts5(
    id UNINDEXED,
    content
);

INSERT INTO note_fts(id, title, body)
SELECT id, title, content
  FROM note;

INSERT INTO document_text_fts(id, title, content)
SELECT d.id,
       d.title,
       COALESCE((SELECT group_concat(dt.content, char(10) || char(10))
                   FROM document_text dt
                  WHERE dt.document_id = d.id
               ORDER BY dt.chunk_index), '')
  FROM document d;

INSERT INTO snippet_fts(id, text)
SELECT id,
       trim(COALESCE(quote_text, '') || ' ' || COALESCE(context, ''))
  FROM snippet;

INSERT INTO artifact_fts(id, content)
SELECT id,
       COALESCE(content, '')
  FROM artifact;
