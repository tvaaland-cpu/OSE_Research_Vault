param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot "dist/$Runtime"

Write-Host "Publishing OSE Research Vault for $Runtime ($Configuration)..."
dotnet publish "$repoRoot/src/OseResearchVault.App/OseResearchVault.App.csproj" `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -o $outputDir

Write-Host "Publish output written to $outputDir"
