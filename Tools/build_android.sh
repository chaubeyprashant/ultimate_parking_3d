#!/bin/zsh
# Android build.
#   Tools/build_android.sh            Android App Bundle (country arenas as Play Asset Delivery packs) -> Builds/UltimateCarParking3D.aab
#   Tools/build_android.sh install    same, then install on the device with bundletool --local-testing and launch
#   Tools/build_android.sh apk        quick single APK instead (arenas inside the APK) -> Builds/UltimateCarParking3D.apk
set -u
source "$(dirname "$0")/unity_common.sh"
METHOD=BuildAndroid; OUT="$PROJECT/Builds/UltimateCarParking3D.aab"
if [[ "${1:-}" == "apk" ]]; then METHOD=BuildAndroidApk; OUT="$PROJECT/Builds/UltimateCarParking3D.apk"; fi
if [[ "${SKIP_UNITY:-0}" != "1" ]]; then
  run_unity "$LOGS/build.log" -executeMethod UltimateCarParking.EditorTools.BuildScript.$METHOD
  code=$?
  echo "== build exit $code"
  grep -E "error CS|invalid dependencies|\[Build\]|\[Delivery\]|BUILD FAILED" "$LOGS/build.log" | sort -u | head -20
  [[ $code -ne 0 ]] && exit $code
fi
ls -la "$OUT"
if [[ "${1:-}" == "install" ]]; then
  ANDROID_PLAYER=/Applications/Unity/Hub/Editor/6000.5.9f1/PlaybackEngines/AndroidPlayer
  JAVA="$ANDROID_PLAYER/OpenJDK/bin/java"
  BT="$(ls $ANDROID_PLAYER/Tools/bundletool-all-*.jar | head -1)"
  SERIAL="${ANDROID_SERIAL:-$(adb devices | awk 'NR>1 && $2=="device"{print $1; exit}')}"
  APKS="$PROJECT/Builds/UltimateCarParking3D.apks"; rm -f "$APKS"
  "$JAVA" -jar "$BT" build-apks --bundle="$OUT" --output="$APKS" --local-testing --connected-device \
      --device-id="$SERIAL" --adb="$(command -v adb)" || exit 1
  "$JAVA" -jar "$BT" install-apks --apks="$APKS" --device-id="$SERIAL" --adb="$(command -v adb)" || exit 1
  adb -s "$SERIAL" shell am start -n om.ultimateparking.carparking3d/com.unity3d.player.UnityPlayerGameActivity
fi
