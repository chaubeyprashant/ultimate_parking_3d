# Phase 4: memory and loading (measured on Xiaomi M2007J3SP, release build)

Date 2026-11-17. Unattended benchmark: World Tour missions IN, ID, AE, US, BR, JP, TH played back to back (`bench:` flag, see PerfBenchmark).

## Found
**Arena memory leak.** `LevelLoader` never released its Addressables handle when the level scene was left, so each new country's pack (about 25 MB) stayed resident for the whole session. Unity heap on first visits, before the fix: 136, 148, 172, 199, 209, 226, 236 MB (about +17 MB per new country; about 600 MB projected over 30 countries). Revisits stayed flat, which is why it was easy to miss.

## Fixed
- `LevelLoader.OnDestroy` now releases the arena. Heap after the fix, first visits: 136, 137, 150, 153, 140, 145, 144 MB; across 14 loads it stays within 136-158 MB, reserved memory flat at 235 MB (was 295-315 MB).
- Leak guard: `LevelLoader.OpenArenaHandles` must be 0 after leaving a level. A PlayMode test fails without the fix (checked by removing it).
- `ArenaPrefetcher`: selecting a country on the map starts downloading its asset pack (download only, no memory), once per pack. Hides on-demand download time behind the player choosing; not testable here beyond the request itself (real Play downloads untested).
- `MemoryPolicy` / `MemoryGuard`: on Android's low-memory warning, free unused assets at a safe moment (never mid-drive); two warnings within five minutes switch to half-size textures (quarter of the texture memory) until restart. Low quality tier now starts with half-size textures.

## Load times (cold, Xiaomi, from the World Tour map)
Fade out about 300 ms, pack check 18-270 ms, arena asset load 230-450 ms, instantiate 2-15 ms, car and mission setup about 5 ms, then the 0.5 s car-settle delay before controls. Request to playable: 1.3-1.7 s cold, about 1.0 s warm. The Galaxy A33 (Phase 1) took 1.5-2.3 s. The biggest single item is the asset load, then the screen fades.

## Not done / not measured
- Real Play on-demand downloads (network time) and prefetch effect.
- Low-memory behaviour on a 2-3 GB phone; the Xiaomi (5.6 GB) raised 2 warnings during a long benchmark, handled by the new policy but the resulting texture switch was not observed on device.
- GC bursts at level start (13 collections in the first window) not traced: draw calls and allocations need a development build with the Profiler.
- Galaxy A33 not connected this phase.
