#!/bin/bash
# Runs one static editor method headlessly: Tools/run_method.sh Namespace.Class.Method [logname]
set -u
source "$(dirname "$0")/unity_common.sh"
LOG="$LOGS/${2:-method}.log"
run_unity "$LOG" -quit -executeMethod "$1"
code=$?
echo "== $1 exit $code"
grep -E "error CS|Exception|\[Arena" "$LOG" | sort -u | head -30
exit $code
