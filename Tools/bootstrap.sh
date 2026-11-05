#!/bin/zsh
# Generates settings, materials, the car prefab and all scenes. Usage: Tools/bootstrap.sh
set -u
source "$(dirname "$0")/unity_common.sh"
# TMP Essential Resources (fonts/shaders/settings TextMeshPro needs): unpacked directly, once.
if [[ ! -f "$PROJECT/Assets/TextMesh Pro/Resources/TMP Settings.asset" ]]; then
  TMP_PKG="$(ls -d "$PROJECT"/Library/PackageCache/com.unity.ugui@*/"Package Resources/TMP Essential Resources.unitypackage" 2>/dev/null | head -1)"
  [[ -z "$TMP_PKG" ]] && TMP_PKG="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage"
  python3 "$(dirname "$0")/extract_unitypackage.py" "$TMP_PKG" "$PROJECT"
fi
run_unity "$LOGS/bootstrap.log" -quit -executeMethod UltimateCarParking.EditorTools.ProjectBootstrapper.BootstrapAll
code=$?
echo "== bootstrap exit $code"
grep -E "error CS|invalid dependencies|\[Bootstrap\]|Exception" "$LOGS/bootstrap.log" | sort -u | head -30
exit $code
