#!/bin/zsh
# Creates a Play Console upload keystore (keep safe — losing it blocks updates).
set -euo pipefail
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="$PROJECT/Builds"
KEYSTORE="$BUILD_DIR/ultimate-parking-upload.keystore"
ENV_FILE="$BUILD_DIR/signing.local.env"
ALIAS="${ANDROID_KEY_ALIAS:-upload}"

mkdir -p "$BUILD_DIR"
if [[ -f "$KEYSTORE" ]]; then
  echo "Keystore already exists: $KEYSTORE"
  exit 0
fi

if [[ -z "${ANDROID_KEYSTORE_PASS:-}" ]]; then
  ANDROID_KEYSTORE_PASS="$(openssl rand -base64 18 | tr -d '/+=' | head -c 20)"
  echo "Generated random keystore password (saved to signing.local.env)."
fi
ANDROID_KEY_PASS="${ANDROID_KEY_PASS:-$ANDROID_KEYSTORE_PASS}"

KEYTOOL="${JAVA_HOME:-/Applications/Unity/Hub/Editor/6000.5.9f1/PlaybackEngines/AndroidPlayer/OpenJDK}/bin/keytool"
"$KEYTOOL" -genkeypair -v \
  -keystore "$KEYSTORE" \
  -alias "$ALIAS" \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -storepass "$ANDROID_KEYSTORE_PASS" \
  -keypass "$ANDROID_KEY_PASS" \
  -dname "CN=Ultimate Parking, OU=Android, O=Ultimate Parking, L=Unknown, ST=Unknown, C=IN"

cat > "$ENV_FILE" <<EOF
# Local only — Builds/ is gitignored. Back up the keystore and passwords offline.
ANDROID_KEYSTORE_PATH=$KEYSTORE
ANDROID_KEY_ALIAS=$ALIAS
ANDROID_KEYSTORE_PASS=$ANDROID_KEYSTORE_PASS
ANDROID_KEY_PASS=$ANDROID_KEY_PASS
EOF
chmod 600 "$ENV_FILE" 2>/dev/null || true

echo "Created upload keystore:"
echo "  $KEYSTORE"
echo "  alias: $ALIAS"
echo "  credentials: $ENV_FILE"
echo "Back up both files before Play Store upload."
