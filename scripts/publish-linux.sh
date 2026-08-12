#!/usr/bin/env bash
#
# Publishes the Linux build and packages it as a tarball. Mirrors what the
# release workflow does on ubuntu-latest, so a local build and a released build
# are produced the same way.
#
# Produces:
#   artifacts/publish/<rid>/WallpaperSwitcher                 self-contained, single file
#   artifacts/tar/WallpaperSwitcher-<version>-<rid>.tar.gz
#
# Usage: ./scripts/publish-linux.sh [linux-x64|linux-arm64]
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj"
APP_NAME="WallpaperSwitcher"
RUNTIME="${1:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"

# Read the default from Directory.Build.props so a local run matches a release
# build. Override with VERSION=1.2.3 (the release workflow passes the tag).
VERSION="${VERSION:-$(sed -n 's/.*<VersionPrefix>\(.*\)<\/VersionPrefix>.*/\1/p' "$ROOT_DIR/Directory.Build.props" | head -n 1)}"

if [[ "$RUNTIME" != linux-* ]]; then
  echo "Runtime must be a Linux runtime such as linux-x64 or linux-arm64." >&2
  exit 1
fi

export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"

PUBLISH_DIR="$ROOT_DIR/artifacts/publish/$RUNTIME"
TAR_DIR="$ROOT_DIR/artifacts/tar"
TARBALL="$TAR_DIR/$APP_NAME-$VERSION-$RUNTIME.tar.gz"

rm -rf "$PUBLISH_DIR"

# PublishReadyToRun is off deliberately: it roughly doubles the download for a
# startup saving that does not matter for an app that lives in the tray.
dotnet publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=false \
  -p:DebugType=none \
  -p:Version="$VERSION" \
  -o "$PUBLISH_DIR"

BINARY="$PUBLISH_DIR/$APP_NAME"
if [[ ! -f "$BINARY" ]]; then
  echo "Publish did not produce $BINARY" >&2
  exit 1
fi

# The Skia and HarfBuzz native libraries are embedded in the single file rather
# than sitting beside it, so a suspiciously small binary means the publish
# silently produced something that will not run on a machine without them.
SIZE_MB=$(( $(wc -c < "$BINARY") / 1024 / 1024 ))
echo "  $APP_NAME  ${SIZE_MB} MB"
if (( SIZE_MB < 40 )); then
  echo "Binary is only ${SIZE_MB} MB; native libraries are probably not embedded." >&2
  exit 1
fi

chmod +x "$BINARY"
mkdir -p "$TAR_DIR"
rm -f "$TARBALL"
tar -czf "$TARBALL" -C "$PUBLISH_DIR" "$APP_NAME"
echo "Created $TARBALL"
