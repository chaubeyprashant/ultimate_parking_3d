#!/bin/bash
# usage: perf_case.sh name [tab_x tab_y] flag_x flag_y   (device pixels)
D=${ANDROID_SERIAL:-RZCT929739K}; P=om.ultimateparking.carparking3d; S=${OUT:-/tmp}
name=$1; shift
adb -s $D shell am force-stop $P; adb -s $D logcat -c
adb -s $D shell am start -n $P/com.unity3d.player.UnityPlayerGameActivity >/dev/null; sleep 22
adb -s $D shell input tap 232 829; sleep 3
if [ $# -eq 4 ]; then adb -s $D shell input tap $1 $2; sleep 1; shift 2; fi
adb -s $D shell input tap $1 $2; sleep 1.5
adb -s $D shell input tap 2040 970
sleep 24
adb -s $D shell input swipe 2160 876 2160 876 9000 &
sleep 2; adb -s $D shell input swipe 490 910 490 910 3000
wait
sleep 3
adb -s $D exec-out screencap -p > $S/case_$name.png
adb -s $D logcat -d -s Unity | grep -E "\[PerfMon\] (level|scene 'Gameplay|Gameplay:|LOW)" | sed 's/^.*\[PerfMon\]/[PerfMon]/' | cut -c1-400 > $S/case_$name.txt
echo "== $name"; cat $S/case_$name.txt
