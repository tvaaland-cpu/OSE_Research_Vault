# Database schema

## Tables

- `schema_migrations`
  - `id` TEXT PRIMARY KEY
  - `applied_utc` TEXT

- `companies`
  - `id` INTEGER PRIMARY KEY AUTOINCREMENT
  - `name` TEXT NOT NULL
  - `ticker` TEXT NULL
  - `created_utc` TEXT NOT NULL

- `notes`
  - `id` INTEGER PRIMARY KEY AUTOINCREMENT
  - `company_id` INTEGER NULL
  - `title` TEXT NOT NULL
  - `content` TEXT NOT NULL
  - `created_utc` TEXT NOT NULL
  - `updated_utc` TEXT NOT NULL

- `documents`
  - `id` INTEGER PRIMARY KEY AUTOINCREMENT
  - `company_id` INTEGER NULL
  - `file_name` TEXT NOT NULL
  - `relative_path` TEXT NOT NULL
  - `created_utc` TEXT NOT NULL

- `watchlist`
  - `id` INTEGER PRIMARY KEY AUTOINCREMENT
  - `company_name` TEXT NOT NULL
  - `ticker` TEXT NULL
  - `created_utc` TEXT NOT NULL

## Full-text search

- Virtual table: `notes_fts` using `fts5(title, content)`.
- Triggers keep `notes_fts` synchronized on insert/update/delete of `notes`.
