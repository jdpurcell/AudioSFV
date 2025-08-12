#!/usr/bin/env bash
# Deploy an Avalonia .NET app as a macOS .app bundle.
# References: https://docs.avaloniaui.net/docs/deployment/macOS

set -euo pipefail

# ---- Configurable defaults ----
PROJECT_FILE=${PROJECT_FILE:-"AudioSFV.csproj"}
APP_NAME=${APP_NAME:-"AudioSFV"}
BUNDLE_ID=${BUNDLE_ID:-"com.jdp.${APP_NAME}"}
APP_VERSION=${APP_VERSION:-"1.0.0"}
ICON_PATH=${ICON_PATH:-"macos/AppIcon.icns"}
ENTITLEMENTS_PATH=${ENTITLEMENTS_PATH:-"macos/entitlements.plist"}
OUTPUT_DIR=${OUTPUT_DIR:-"dist"}
SIGN_AND_NOTARIZE="${SIGN_AND_NOTARIZE:-}"
CODESIGN_CERT_NAME="${CODESIGN_CERT_NAME:-}"
APPLE_ID_USER="${APPLE_ID_USER:-}"
APPLE_ID_PASS="${APPLE_ID_PASS:-}"

# File association defaults
FILE_EXT=${FILE_EXT:-"sfva"}
DOC_NAME=${DOC_NAME:-"Audio SFV"}
DOC_UTI=${DOC_UTI:-"com.audiosfv.sfva"}

# ---- Helpers ----
fail() { echo "Error: $*" >&2; exit 1; }

# Resolve/set working directory
ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$ROOT_DIR"

# Determine RID from host arch unless provided. Accepts osx-x64/osx-arm64
RID=${RID:-""}
if [[ -z "${RID}" ]]; then
  ARCH=$(uname -m)
  case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x86_64) RID="osx-x64" ;;
    *) fail "Unsupported arch: $ARCH. Set RID=osx-arm64 or RID=osx-x64" ;;
  esac
fi

FRAMEWORK=${FRAMEWORK:-"net8.0"}
CONFIG=${CONFIG:-"Release"}

PUBLISH_DIR="bin/${CONFIG}/${FRAMEWORK}/${RID}/publish"
APP_DIR="${OUTPUT_DIR}/${APP_NAME}.app"

echo "Building ${APP_NAME} for ${RID} (${CONFIG})..."
dotnet publish "$PROJECT_FILE" -c "$CONFIG" -r "$RID" \
  -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false \
  -p:DebugType=None -p:DebugSymbols=false

[[ -d "$PUBLISH_DIR" ]] || fail "Publish directory not found: $PUBLISH_DIR"

echo "Assembling .app bundle at ${APP_DIR}..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

# Copy published payload into Contents/MacOS
cp -a "$PUBLISH_DIR"/. "$APP_DIR/Contents/MacOS/"

# Create Info.plist from template with variables
PLIST_TEMPLATE="macos/Info.plist"
PLIST_OUT="$APP_DIR/Contents/Info.plist"
sed -e "s/__APP_NAME__/${APP_NAME}/g" \
    -e "s/__BUNDLE_ID__/${BUNDLE_ID}/g" \
    -e "s/__APP_VERSION__/${APP_VERSION}/g" \
    -e "s/__FILE_EXT__/${FILE_EXT}/g" \
    -e "s/__DOC_NAME__/${DOC_NAME}/g" \
    -e "s/__DOC_UTI__/${DOC_UTI}/g" \
    "$PLIST_TEMPLATE" > "$PLIST_OUT"

# Copy icon 
cp "$ICON_PATH" "$APP_DIR/Contents/Resources/AppIcon.icns"

# Sign .app
if [[ "$SIGN_AND_NOTARIZE" == "true" ]]; then
    echo "Running codesign"
    codesign --sign "$CODESIGN_CERT_NAME" --deep --force --options runtime --entitlements "$ENTITLEMENTS_PATH" --timestamp "$APP_DIR"
fi

# Create .dmg
echo "Creating disk image"
DMG_FILE=$OUTPUT_DIR/$APP_NAME.dmg
rm -f "$DMG_FILE"
hdiutil create -srcfolder "$APP_DIR" -volname "$APP_NAME" -format UDSB "$OUTPUT_DIR/temp.sparsebundle"
hdiutil convert "$OUTPUT_DIR/temp.sparsebundle" -format ULFO -o "$DMG_FILE"
rm -r "$OUTPUT_DIR/temp.sparsebundle"

# Sign/notarize .dmg
if [[ "$SIGN_AND_NOTARIZE" == "true" ]]; then
    codesign --sign "$CODESIGN_CERT_NAME" --timestamp --identifier "$BUNDLE_ID.dmg" "$DMG_FILE"
    xcrun notarytool submit "$DMG_FILE" --apple-id "$APPLE_ID_USER" --password "$APPLE_ID_PASS" --team-id "${CODESIGN_CERT_NAME: -11:10}" --wait
    xcrun stapler staple "$DMG_FILE"
    xcrun stapler validate "$DMG_FILE"
    rm -r "$APP_DIR"
fi

exit 0
