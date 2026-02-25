# Architecture (MVP)

## Layers

1. **App (`OseResearchVault.App`)**
   - WPF shell and MVVM presentation models
   - Dependency injection composition root
   - Startup orchestration (settings + migrations)
   - Logging providers

2. **Core (`OseResearchVault.Core`)**
   - Domain-neutral models (`AppSettings`, navigation contracts)
   - Interfaces (`IAppSettingsService`, `IDatabaseInitializer`, repositories)

3. **Data (`OseResearchVault.Data`)**
   - JSON settings persistence
   - SQLite migrations (`schema_migrations` table)
   - Dapper repositories

4. **Tests (`OseResearchVault.Tests`)**
   - Unit tests for migration metadata and settings initialization

## Startup sequence

1. Build DI container and logging.
2. Load/create settings file.
3. Resolve database path and initialize SQLite DB.
4. Apply pending migrations.
5. Launch WPF shell.
