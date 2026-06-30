#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/WallpaperSwitcher.Desktop/WallpaperSwitcher.Desktop.csproj"
APP_NAME="WallpaperSwitcher"
ICON_SOURCE="$ROOT_DIR/WallpaperSwitcher.Desktop/Assets/AppIcon.png"
RUNTIME="${1:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
ICON_BUNDLE_FILE="AppIcon"

if [[ "$RUNTIME" != osx-* ]]; then
  echo "Runtime must be a macOS runtime such as osx-arm64 or osx-x64." >&2
  exit 1
fi

if ! command -v hdiutil >/dev/null 2>&1; then
  echo "hdiutil is required to create a DMG. Run this script on macOS." >&2
  exit 1
fi

export AVALONIA_TELEMETRY_OPTOUT="${AVALONIA_TELEMETRY_OPTOUT:-1}"

PUBLISH_DIR="$ROOT_DIR/artifacts/publish/$RUNTIME"
APP_ARTIFACT_ROOT="$ROOT_DIR/artifacts/macos/$RUNTIME"
BUILD_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/wallpaper-switcher-dmg.XXXXXX")"
APP_DIR="$BUILD_ROOT/$APP_NAME.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
STAGING_DIR="$BUILD_ROOT/dmg-stage"
DMG_DIR="$ROOT_DIR/artifacts/dmg"
DMG_PATH="$DMG_DIR/$APP_NAME-$RUNTIME.dmg"
trap 'rm -rf "$BUILD_ROOT"' EXIT

dotnet publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=false \
  -o "$PUBLISH_DIR"

rm -rf "$APP_ARTIFACT_ROOT" "$DMG_PATH"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR" "$STAGING_DIR" "$DMG_DIR" "$APP_ARTIFACT_ROOT"

cp -R "$PUBLISH_DIR/." "$MACOS_DIR/"
find "$MACOS_DIR" -name "*.pdb" -delete
chmod +x "$MACOS_DIR/$APP_NAME"

if [[ -f "$ICON_SOURCE" ]]; then
  ICONSET_DIR="$BUILD_ROOT/AppIcon.iconset"
  mkdir -p "$ICONSET_DIR"
  sips -z 16 16 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
  sips -z 32 32 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
  sips -z 64 64 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
  sips -z 256 256 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
  sips -z 512 512 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "$ICON_SOURCE" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
  cp "$ICON_SOURCE" "$ICONSET_DIR/icon_512x512@2x.png"
  xattr -cr "$ICONSET_DIR" 2>/dev/null || true
  if iconutil -c icns "$ICONSET_DIR" -o "$RESOURCES_DIR/AppIcon.icns"; then
    ICON_BUNDLE_FILE="AppIcon"
  else
    echo "warning: iconutil could not create AppIcon.icns; falling back to AppIcon.png for local testing." >&2
    cp "$ICON_SOURCE" "$RESOURCES_DIR/AppIcon.png"
    xattr -cr "$RESOURCES_DIR/AppIcon.png" 2>/dev/null || true
    ICON_BUNDLE_FILE="AppIcon.png"
  fi
fi

cat > "$CONTENTS_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>Wallpaper Switcher</string>
  <key>CFBundleExecutable</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>com.wallpaperswitcher.app</string>
  <key>CFBundleIconFile</key>
  <string>$ICON_BUNDLE_FILE</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Wallpaper Switcher</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSHumanReadableCopyright</key>
  <string>Copyright © 2026</string>
</dict>
</plist>
PLIST

if command -v codesign >/dev/null 2>&1; then
  xattr -cr "$APP_DIR" 2>/dev/null || true
  codesign --force --deep --sign - "$APP_DIR"
  codesign --verify --deep --strict "$APP_DIR"
fi

cp -R "$APP_DIR" "$APP_ARTIFACT_ROOT/"
cp -R "$APP_DIR" "$STAGING_DIR/"
xattr -cr "$APP_ARTIFACT_ROOT/$APP_NAME.app" 2>/dev/null || true
xattr -cr "$STAGING_DIR/$APP_NAME.app" 2>/dev/null || true
xattr -d com.apple.FinderInfo "$APP_ARTIFACT_ROOT/$APP_NAME.app" 2>/dev/null || true
xattr -d com.apple.FinderInfo "$STAGING_DIR/$APP_NAME.app" 2>/dev/null || true
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create \
  -volname "Wallpaper Switcher" \
  -srcfolder "$STAGING_DIR" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "Created $DMG_PATH"
