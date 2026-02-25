# OSE Research Vault

OSE Research Vault is a local-first Windows desktop app for equity and company research. The MVP uses WPF + MVVM, SQLite, Dapper, and SQLite FTS5.

## Repository layout

- `src/OseResearchVault.App` - WPF shell application
- `src/OseResearchVault.Core` - Domain contracts and shared models
- `src/OseResearchVault.Data` - SQLite persistence, migrations, and repositories
- `src/OseResearchVault.Tests` - Unit tests
- `docs` - Architecture and schema docs

## Prerequisites

- Windows 10/11
- .NET SDK 8.0+

## Build and run

```bash
dotnet restore OseResearchVault.sln
dotnet build OseResearchVault.sln -c Debug
```

Run the app:

```bash
dotnet run --project src/OseResearchVault.App/OseResearchVault.App.csproj
```

## Runtime behavior

- On first run, the app creates a settings file in `%APPDATA%/OSE Research Vault/settings.json`.
- If no custom paths are set, it uses defaults under `%APPDATA%/OSE Research Vault/`:
  - `data/ose-research-vault.db`
  - `vault/`
- Database schema migrations run automatically at startup.
- Logs are written to `%LOCALAPPDATA%/OSE Research Vault/logs/`.

## Next steps

- Add real pages for each navigation area
- Add settings UI for selecting custom data folder
- Add CRUD workflows for companies/documents/notes
