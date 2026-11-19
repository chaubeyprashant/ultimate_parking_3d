# Phase 1: performance baseline (Galaxy A33, release build)

Date 2026-11-16. Device: Samsung SM-A336E, 5.5 GB RAM, 2400x1080, Android, Mali GPU, 60 Hz display. Build: release AAB with on-demand packs, local-testing install, **before any driving-feel or quality changes**. Quality: project default, URP asset `URP_Mobile` (MSAA 2x, render scale 1.0, 1024 shadow map, 45 m, no HDR). All sessions were about 1-2 minutes at room temperature, battery 55-58%, so thermal status stayed 0 (none): **long-session thermal and battery drain are not measured yet**.

## How to reproduce
```
adb shell touch /sdcard/Android/data/om.ultimateparking.carparking3d/files/perfmon     # turns the recorder on
OUT=/tmp Tools/perf_case.sh <name> [tab_x tab_y] <flag_x> <flag_y>                     # cold start -> World Tour -> mission -> drive 9 s
adb pull /sdcard/Android/data/om.ultimateparking.carparking3d/files/perf_log.csv        # per-10-second rows
```
`PerfMonitor` writes one logcat line (tag `PerfMon`) and one CSV row every 10 s; it is off in normal play (no flag file = nothing runs).

## Results (10-second windows; first row of each level includes the load)
| Scene | FPS avg (steady) | 1% low | Worst frame | Frames >33 ms (per 10 s) | CPU main | GPU | Set-pass | Triangles | System mem |
|---|---|---|---|---|---|---|---|---|---|
| Main menu | 59.8 | 59.6 | 17 ms | 0 | 4.8-5.6 ms | 12-17 ms | 12 | 13 k | 415 MB |
| Garage | 59.7 | 59.7 | 17-33 ms | 0-1 | 7.1 ms | 15-17 ms | 11 | 15 k | 384 MB |
| Campaign L21 "The Yard" | 59.5-59.8 | 59.6 | 33 ms | 0-3 | 7.5-8.4 ms | 13-16 ms | 26-44 | 172-392 k | 430 MB |
| Indonesia ID1 | 59.0-59.8 | 29.9 | 33 ms | 0-7 | 7.2-7.7 ms | 13-16 ms | 45 | 202-293 k | 495-508 MB |
| Thailand TH1 | 59.8 | 59.7 | 17 ms | 0 | 6.7-7.7 ms | 15-16 ms | 14-46 | 17-147 k | 497 MB |
| Japan JP1 | 59.5-59.8 | 59.7 | 17-33 ms | 0-3 | 7.6-7.9 ms | 14-17 ms | 50 | 337-359 k | 493-506 MB |
| Brazil BR1 | 59.8 | 59.7 | 17 ms | 0 | 6.7-7.5 ms | 15-16 ms | 15-31 | 22-123 k | 508 MB |
| UAE AE1 | 58.8-59.0 | 29.9 | 50-67 ms | 6-7 (1-2 >50 ms) | 7.4-7.5 ms | 14 ms | 55-60 | 410-661 k | 517-538 MB |
| **US US1 (heaviest)** | 57.7-58.3 | 29.9 | 50 ms | 9-19 (2-5 >50 ms) | 8.9-10.0 ms | 14-15 ms | 79-98 | 0.73-1.06 M | 538-561 MB |

Level load (scene loaded to playable): 1.5-2.3 s for World Tour arenas (US 2.2 s). That is from local packs: a real on-demand download adds network time that is not measured. Cold start to main menu scene: about 2.8 s. Arena build causes one hitch of 335-652 ms and about 13 GC collections in the first 10 s of every level. Low-memory warnings: 0.

## Findings
1. **60 FPS holds on this phone** for light and mid arenas. Heavy arenas (US, UAE) drop frames: 9-29 frames per 10 s slower than 33 ms, 1% low about 20-30 FPS.
2. **The GPU is the tight resource, not the CPU.** GPU frame time is 12-17 ms (a 16.7 ms budget at 60 FPS) even in the menu with 13 k triangles, while the main thread uses only 5-10 ms. That points to fill-rate and bandwidth cost (2400x1080 at render scale 1.0 with 2x MSAA), not geometry. This is a hypothesis; the quality phase will confirm it by changing render scale and MSAA and re-measuring.
3. **Level start is a hitch.** About 0.3-0.65 s freeze plus GC burst while the arena instantiates; masked by the loading screen only partly.
4. **Memory** is 500-560 MB in arenas on a 5.5 GB phone. Nothing was tested on a 2-3 GB device; those are untested and flagged as a risk.
5. **Not measurable in release builds:** draw calls and per-frame GC bytes (reported as -1). Set-pass calls, triangle counts and GC collection counts are real. For draw calls and allocation traces a development build with the Profiler is needed (Phase 3/4).

## Known limits of this baseline
- One device, short sessions: no thermal or battery-drain data, no low-end device, no 90/120 Hz display.
- Driving input was scripted (hold Gas 9 s, steer right 3 s), so scenes are measured while moving but not during real parking.
