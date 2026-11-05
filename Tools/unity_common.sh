# Shared by the Tools/*.sh scripts. Runs Unity headlessly with a stall watchdog
# (Unity 6000.5 batch mode has been seen to hang silently; kill it if the log goes quiet).
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY=/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity
LOGS="$PROJECT/../UltimateCarParking3D_logs"; mkdir -p "$LOGS"
STALL_SECONDS=${STALL_SECONDS:-480}

# run_unity <logfile> <unity args...>  — no -nographics (it has deadlocked 6000.5 during project load)
run_unity() {
  local log=$1; shift
  rm -f "$log"
  "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$log" "$@" &
  local pid=$!
  while kill -0 $pid 2>/dev/null; do
    sleep 15
    if [[ -f "$log" ]]; then
      local age=$(( $(date +%s) - $(stat -f %m "$log") ))
      if (( age > STALL_SECONDS )); then
        echo "!! Unity log silent for ${age}s — killing $pid"; kill $pid; sleep 5; kill -9 $pid 2>/dev/null
      fi
    fi
  done
  wait $pid
}
