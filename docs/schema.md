# Database schema

## Migration system

- `schema_migrations(version TEXT PRIMARY KEY, applied_at TEXT)` tracks applied SQL migrations.
- Migration files live in `src/OseResearchVault.Data/Migrations` and are applied in filename order.
- SQLite connections are configured with `foreign_keys=ON`.

## Core entities

All primary tables use `id TEXT PRIMARY KEY` with a SQLite-generated GUID-style default value.

- `workspace`: top-level container for research data.
- `company`: belongs to a `workspace`.
- `position`: belongs to a `workspace`; optional link to `company`.
- `watchlist_item`: belongs to a `workspace`; optional link to `company`.
- `source`: belongs to a `workspace`; optional link to `company`.
- `document`: belongs to a `workspace`; optional links to `source` and `company`.
- `document_text`: belongs to a `document`; stores extracted text chunks.
- `note`: belongs to a `workspace`; optional links to `company` and `position`.
- `snippet`: belongs to a `workspace`; optional links to `document`, `note`, and `source`.
- `agent`: belongs to a `workspace`.
- `agent_run`: belongs to an `agent` and `workspace`.
- `tool_call`: belongs to an `agent_run`.
- `artifact`: belongs to a `workspace`; optional link to `agent_run`.
- `evidence_link`: polymorphic linkage between entities inside a `workspace`.
- `tag`: belongs to a `workspace`, unique by `(workspace_id, name)`.
- `event`: belongs to a `workspace`; optional links to `company` and `position`.
- `metric`: belongs to a `workspace`; optional links to `company` and `position`.
- `price_daily`: belongs to a `workspace`; required `company` and daily close values unique on `(workspace_id, company_id, price_date)`.

## Tag join tables

- `note_tag(note_id, tag_id)`
- `snippet_tag(snippet_id, tag_id)`
- `artifact_tag(artifact_id, tag_id)`
- `document_tag(document_id, tag_id)`
- `company_tag(company_id, tag_id)`

All join tables use composite primary keys and cascade deletes from both sides.

## Full-text search (FTS5)

The schema defines four virtual FTS5 tables:

- `note_fts(id UNINDEXED, title, content)`
- `snippet_fts(id UNINDEXED, quote_text, context)`
- `artifact_fts(id UNINDEXED, title, content)`
- `document_text_fts(id UNINDEXED, content)`

Synchronization is handled in application code (not SQL triggers) for create/update/delete operations.
