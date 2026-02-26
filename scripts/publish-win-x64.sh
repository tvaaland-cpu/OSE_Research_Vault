#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-win-x64}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/dist/$RUNTIME"

echo "Publishing OSE Research Vault for $RUNTIME ($CONFIGURATION)..."
dotnet publish "$REPO_ROOT/src/OseResearchVault.App/OseResearchVault.App.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -o "$OUTPUT_DIR"

echo "Publish output written to $OUTPUT_DIR"
