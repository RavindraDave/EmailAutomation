#!/usr/bin/env bash
set -euo pipefail

# Builds a double-clickable EmailAutomation.app bundle for macOS (Apple Silicon).
# Run from anywhere; paths are resolved relative to the repository root.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/publish/osx-arm64"
APP_DIR="$REPO_ROOT/publish/EmailAutomation.app"
ICON_SRC="$REPO_ROOT/EmailAutomation.UI/Assets/avalonia-logo.ico"

echo "==> Publishing self-contained osx-arm64 build..."
dotnet publish "$REPO_ROOT/EmailAutomation.UI/EmailAutomation.UI.csproj" \
  -c Release -r osx-arm64 --self-contained true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o "$PUBLISH_DIR"

# .pdb files (ours and the ones SkiaSharp/HarfBuzzSharp ship as native-library content) add tens
# of MB and are never needed at runtime - only for attaching a debugger to a native crash.
find "$PUBLISH_DIR" -name "*.pdb" -delete

echo "==> Assembling .app bundle at $APP_DIR"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
cp "$REPO_ROOT/packaging/macos/Info.plist" "$APP_DIR/Contents/Info.plist"
chmod +x "$APP_DIR/Contents/MacOS/EmailAutomation.UI"

echo "==> Generating AppIcon.icns from $ICON_SRC"
WORKDIR="$(mktemp -d)"
ICONSET="$WORKDIR/AppIcon.iconset"
mkdir -p "$ICONSET"
BASE_PNG="$WORKDIR/icon_base.png"
sips -s format png "$ICON_SRC" --out "$BASE_PNG" -Z 256 > /dev/null
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$BASE_PNG" --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$BASE_PNG" --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$ICONSET" -o "$APP_DIR/Contents/Resources/AppIcon.icns"
rm -rf "$WORKDIR"

echo "==> Done: $APP_DIR"
echo ""
echo "This bundle is unsigned. On any machine other than the one that built it, Gatekeeper"
echo "will refuse to open it on first launch. Recipients should either:"
echo "  - Right-click the .app in Finder and choose Open (then confirm), or"
echo "  - Run: xattr -dr com.apple.quarantine \"$APP_DIR\""
