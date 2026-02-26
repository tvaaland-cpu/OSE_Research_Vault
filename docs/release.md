# Release and Windows Packaging

This repository currently uses a **self-contained publish workflow** as the Windows packaging path.
It produces a folder that can be distributed directly or zipped into an installer artifact.

## Prerequisites

- .NET 8 SDK
- Windows machine for final packaging validation (`win-x64` runtime)

## 1) Build the solution in Release

From the repository root:

```bash
dotnet restore OseResearchVault.sln -p:EnableWindowsTargeting=true
dotnet build OseResearchVault.sln -c Release --no-restore -p:EnableWindowsTargeting=true
dotnet test src/OseResearchVault.Tests/OseResearchVault.Tests.csproj -c Release --no-build -p:EnableWindowsTargeting=true
```

## 2) Generate a Windows distributable artifact

### Option A (recommended): helper script

```bash
./scripts/publish-win-x64.sh Release win-x64
```

### Option B: direct `dotnet publish`

```bash
dotnet publish src/OseResearchVault.App/OseResearchVault.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./dist/win-x64
```

The output in `dist/win-x64` is the installable/distributable artifact set.

## 3) Optional signing step

If your release process requires signing, sign `OseResearchVault.App.exe` (and optionally all binaries) in `dist/win-x64` using your organization certificate tooling (for example, `signtool.exe`).

## Note on MSIX

MSIX packaging can be added later via a dedicated Windows Application Packaging project (`.wapproj`) when a Windows packaging/signing pipeline is available. This step intentionally keeps packaging lightweight and repeatable in the current repository setup.
