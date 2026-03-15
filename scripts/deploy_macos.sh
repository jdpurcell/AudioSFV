#!/usr/bin/env bash

# Deploy an Avalonia .NET app as a macOS .app bundle.
# References: https://docs.avaloniaui.net/docs/deployment/macOS

set -euo pipefail

# Configurable defaults
PROJECT_FILE=${PROJECT_FILE:-"AudioSFV.csproj"}
APP_NAME=${APP_NAME:-"AudioSFV"}
APP_VERSION=${APP_VERSION:-"1.3.0"}
FRAMEWORK=${FRAMEWORK:-"net10.0"}
CONFIG=${CONFIG:-"Release"}
RID=${RID:-""}
OUTPUT_DIR=${OUTPUT_DIR:-"dist"}

# Optional env vars
APPLE_DEVID_APP_CERT_NAME="${APPLE_DEVID_APP_CERT_NAME:-}"
APPLE_ID_USER="${APPLE_ID_USER:-}"
APPLE_ID_PASS="${APPLE_ID_PASS:-}"

# Other vars
BUNDLE_ID="com.jdp.${APP_NAME}"
ICON_PATH="assets/macos/AppIcon.icns"
PLIST_TEMPLATE_PATH="assets/macos/Info.plist"
ENTITLEMENTS_PATH="assets/macos/Entitlements.plist"
FILE_EXT="sfva"
DOC_NAME="Audio SFV"
DOC_UTI="com.audiosfv.sfva"

# ---- Helpers ----
fail() { echo "Error: $*" >&2; exit 1; }

# Resolve/set working directory
ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$ROOT_DIR"

# Determine RID from host arch unless provided. Accepts osx-x64/osx-arm64
if [[ -z "${RID}" ]]; then
  ARCH=$(uname -m)
  case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) fail "Unsupported arch: $ARCH. Set RID=osx-arm64 or RID=osx-x64" ;;
  esac
fi

PUBLISH_DIR="bin/${CONFIG}/${FRAMEWORK}/${RID}/publish"
APP_DIR="${OUTPUT_DIR}/${APP_NAME}.app"

echo "Building ${APP_NAME} for ${RID} (${CONFIG})..."
dotnet publish "$PROJECT_FILE" -c "$CONFIG" -r "$RID" \
  --self-contained -p:DebugSymbols=false

[[ -d "$PUBLISH_DIR" ]] || fail "Publish directory not found: $PUBLISH_DIR"

echo "Assembling .app bundle at ${APP_DIR}..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

# Copy published payload into Contents/MacOS
cp -a "$PUBLISH_DIR"/. "$APP_DIR/Contents/MacOS/"

# Create Info.plist from template with variables
PLIST_OUT="$APP_DIR/Contents/Info.plist"
sed -e "s/__APP_NAME__/${APP_NAME}/g" \
    -e "s/__BUNDLE_ID__/${BUNDLE_ID}/g" \
    -e "s/__APP_VERSION__/${APP_VERSION}/g" \
    -e "s/__FILE_EXT__/${FILE_EXT}/g" \
    -e "s/__DOC_NAME__/${DOC_NAME}/g" \
    -e "s/__DOC_UTI__/${DOC_UTI}/g" \
    "$PLIST_TEMPLATE_PATH" > "$PLIST_OUT"

# Copy icon 
cp "$ICON_PATH" "$APP_DIR/Contents/Resources/AppIcon.icns"

# Sign .app
if [[ -n "$APPLE_DEVID_APP_CERT_NAME" ]]; then
    echo "Signing app bundle"
    codesign --sign "$APPLE_DEVID_APP_CERT_NAME" --deep --force --options runtime --entitlements "$ENTITLEMENTS_PATH" --timestamp "$APP_DIR"
fi

# Create .dmg
echo "Creating disk image"
ARCH_SUFFIX="${RID##*-}"
DMG_FILE="$OUTPUT_DIR/$APP_NAME-$APP_VERSION-macOS-$ARCH_SUFFIX.dmg"
rm -f "$DMG_FILE"
hdiutil create -srcfolder "$APP_DIR" -volname "$APP_NAME" -format UDSB "$OUTPUT_DIR/temp.sparsebundle"
hdiutil convert "$OUTPUT_DIR/temp.sparsebundle" -format ULFO -o "$DMG_FILE"
rm -r "$OUTPUT_DIR/temp.sparsebundle"
rm -r "$APP_DIR"

# Sign/notarize .dmg
if [[ -n "$APPLE_DEVID_APP_CERT_NAME" ]]; then
    echo "Signing dmg file"
    codesign --sign "$APPLE_DEVID_APP_CERT_NAME" --timestamp --identifier "$BUNDLE_ID.dmg" "$DMG_FILE"

    echo "Notarizing dmg file"
    xcrun notarytool submit "$DMG_FILE" --apple-id "$APPLE_ID_USER" --password "$APPLE_ID_PASS" --team-id "${APPLE_DEVID_APP_CERT_NAME: -11:10}" --wait
    xcrun stapler staple "$DMG_FILE"
    xcrun stapler validate "$DMG_FILE"
fi

exit 0
