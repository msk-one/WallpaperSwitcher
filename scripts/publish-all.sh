#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"
RUNTIMES=(
  "win-x64"
  "osx-arm64"
  "osx-x64"
  "linux-x64"
  "linux-arm64"
)

for runtime in "${RUNTIMES[@]}"; do
  output="$ROOT_DIR/artifacts/publish/$runtime"
  echo "Publishing $runtime -> $output"
  dotnet publish "$PROJECT" \
    -c "$CONFIGURATION" \
    -r "$runtime" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -o "$output"
done

if command -v hdiutil >/dev/null 2>&1; then
  "$ROOT_DIR/scripts/package-macos-dmg.sh" osx-arm64
  "$ROOT_DIR/scripts/package-macos-dmg.sh" osx-x64
else
  echo "Skipping macOS DMG packaging because hdiutil is not available."
fi
