#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"
export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-${TMPDIR:-/tmp}/dotnet-home}"
mkdir -p "$DOTNET_CLI_HOME"
RUNTIMES=(
  "win-x64"
  "osx-arm64"
  "osx-x64"
  "linux-x64"
  "linux-arm64"
)

for runtime in "${RUNTIMES[@]}"; do
  output="$ROOT_DIR/artifacts/publish/$runtime"
  runtime_build_dir="$ROOT_DIR/WallpaperSwitcher.Desktop/bin/$CONFIGURATION/net9.0/$runtime"
  echo "Publishing $runtime -> $output"
  dotnet publish "$PROJECT" \
    -c "$CONFIGURATION" \
    -r "$runtime" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishReadyToRun=false \
    -o "$output"

  case "$runtime" in
    osx-*)
      native_libraries=(libSkiaSharp.dylib libHarfBuzzSharp.dylib libAvaloniaNative.dylib)
      ;;
    linux-*)
      native_libraries=(libSkiaSharp.so libHarfBuzzSharp.so)
      ;;
    win-*)
      native_libraries=(libSkiaSharp.dll libHarfBuzzSharp.dll av_libglesv2.dll)
      ;;
    *)
      native_libraries=()
      ;;
  esac

  for native_library in "${native_libraries[@]}"; do
    if [[ ! -f "$runtime_build_dir/$native_library" ]]; then
      echo "Missing required native library: $runtime_build_dir/$native_library" >&2
      exit 1
    fi

    cp "$runtime_build_dir/$native_library" "$output/"
  done
done

if command -v hdiutil >/dev/null 2>&1; then
  "$ROOT_DIR/scripts/package-macos-dmg.sh" osx-arm64
  "$ROOT_DIR/scripts/package-macos-dmg.sh" osx-x64
else
  echo "Skipping macOS DMG packaging because hdiutil is not available."
fi
