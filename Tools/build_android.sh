#!/bin/zsh
# Builds the APK headlessly; "install" also installs and launches it on the attached device.
# Usage: Tools/build_android.sh [install]
set -u
source "$(dirname "$0")/unity_common.sh"
run_unity "$LOGS/build.log" -executeMethod UltimateCarParking.EditorTools.BuildScript.BuildAndroid
code=$?
echo "== build exit $code"
grep -E "error CS|invalid dependencies|\[Build\]|BUILD FAILED" "$LOGS/build.log" | sort -u | head -20
[[ $code -ne 0 ]] && exit $code
ls -la "$PROJECT/Builds/UltimateCarParking3D.apk"
if [[ "${1:-}" == "install" ]]; then
  SERIAL="${ANDROID_SERIAL:-$(adb devices | awk 'NR>1 && $2=="device"{print $1; exit}')}"
  adb -s "$SERIAL" install -r "$PROJECT/Builds/UltimateCarParking3D.apk" && \
  adb -s "$SERIAL" shell am start -n com.ergebins.ultimatecarparking3d/com.unity3d.player.UnityPlayerGameActivity
fi
