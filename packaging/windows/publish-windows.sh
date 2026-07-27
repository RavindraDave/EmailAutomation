#!/usr/bin/env bash
set -euo pipefail

# Publishes a self-contained, single-file win-x64 build. This is a plain `dotnet publish` with
# no OS-specific packaging step (unlike macOS, Windows needs no .app bundle or icon conversion -
# the .ico referenced by ApplicationIcon in the csproj is embedded directly into the .exe).
# Can be run from macOS/Linux/Windows alike; .NET's publish supports cross-targeting another RID.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/publish/win-x64"

dotnet publish "$REPO_ROOT/EmailAutomation.UI/EmailAutomation.UI.csproj" \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o "$PUBLISH_DIR"

# .pdb files (ours and the ones SkiaSharp/HarfBuzzSharp ship as native-library content) add tens
# of MB and are never needed at runtime - only for attaching a debugger to a native crash.
find "$PUBLISH_DIR" -name "*.pdb" -delete

echo "==> Done: $PUBLISH_DIR/EmailAutomation.UI.exe"
