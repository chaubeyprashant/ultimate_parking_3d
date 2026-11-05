#!/bin/zsh
# Runs the test suites headlessly. Usage: [FILTER=regex] Tools/run_tests.sh [EditMode|PlayMode|both]
set -u
source "$(dirname "$0")/unity_common.sh"
MODE="${1:-both}"
filter=(); [[ -n "${FILTER:-}" ]] && filter=(-testFilter "$FILTER")
run() {
  local platform=$1
  rm -f "$LOGS/$platform.xml"
  run_unity "$LOGS/$platform.log" -runTests -testPlatform $platform -testResults "$LOGS/$platform.xml" "${filter[@]}"
  local code=$?
  echo "== $platform exit $code"
  grep -E "error CS|invalid dependencies" "$LOGS/$platform.log" | sort -u | head -20
  grep -E "^\[Test\]" "$LOGS/$platform.log" | head -60
  if [[ -f "$LOGS/$platform.xml" ]]; then
    python3 - "$LOGS/$platform.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')} skipped={r.get('skipped')} duration={float(r.get('duration')):.1f}s")
for tc in r.iter('test-case'):
    if tc.get('result') != 'Passed':
        msg = tc.find('failure/message'); print(f"  {tc.get('result')}: {tc.get('fullname')}\n    {(msg.text or '').strip()[:500] if msg is not None else ''}")
PY
  else
    echo "no results file"
  fi
  return $code
}
code=0
case $MODE in
  EditMode) run EditMode || code=$? ;;
  PlayMode) run PlayMode || code=$? ;;
  both) run EditMode || code=$?; run PlayMode || code=$? ;;
esac
exit $code
