# Ultimate Car Parking 3D — Phase 2: parking mechanics and levels

Builds on Phase 1 without changing the vehicle, input, wheel or camera code. Phase 1 setup: `PHASE1_SETUP.md`.

## What was added

| Script | Folder | Role |
|---|---|---|
| `ParkingZone` | Runtime/Parking | Target bay: trigger box tracking the player car + 4 marker strips that change colour with status |
| `ParkingValidator` | Runtime/Parking | Inside / aligned / slow / held checks; raises `Parked` |
| `ParkingRules` | Runtime/Parking | Threshold set (edge margin, angle tolerance, either direction, max speed, hold time) |
| `ParkingGeometry` | Runtime/Parking | Pure maths: corners-inside-bay count, heading error |
| `ParkingStatus` | Runtime/Parking | Inactive, Outside, PartiallyInside, Misaligned, TooFast, Holding, Parked |
| `CollisionHandler` | Runtime/Vehicle | On the car; counts penalties (ignores ground, soft touches, repeat scrapes) |
| `LevelData` | Runtime/Levels | ScriptableObject: id, name, environment, spawn, target, limits, rules override, stars, rewards |
| `LevelCatalog` | Runtime/Levels | Ordered list of LevelData (defines "next level") |
| `LevelSelection` | Runtime/Levels | Carries the chosen level across a scene load |
| `LevelLoader` | Runtime/Levels | Spawns environment, car, bay; points camera; hands off to LevelManager |
| `LevelManager` | Runtime/Levels | Start, timer, collision limit, complete, fail, restart, next; events for UI |
| `StarRating`, `StarThresholds` | Runtime/Levels | 3 / 2 / 1 star rules |
| `LevelResult`, `LevelState`, `FailReason` | Runtime/Levels | Outcome types |
| `LevelDebugOverlay` | Runtime/DebugTools | Editor/dev-build overlay: state, timer, checks, Restart/Next (R / N) |

Generated assets: `Prefabs/Environments/Env_TestLot.prefab`, `Levels/Level_01.asset`, `Levels/Level_02.asset`,
`Levels/LevelCatalog.asset`. `PlayerCar.prefab` gained a `CollisionHandler`. `Gameplay.unity` is now a shell
(sun, camera, `GameSystems`); the loader spawns the world and the car.

## How the pieces connect

```
Gameplay.unity
├─ Directional Light
├─ Main Camera          VehicleCamera (target set by the loader)
└─ GameSystems          ParkingValidator, LevelManager, LevelLoader, LevelDebugOverlay

LevelLoader.Start
  level = LevelSelection.Consume() ?? catalog[defaultLevelIndex]
  → Instantiate(level.EnvironmentPrefab)
  → Instantiate(vehiclePrefab) at spawn (+ spawnLift)
  → new ParkingZone at target (size from LevelData)
  → camera.SetTarget(car)
  → LevelManager.BeginLevel(level, car, collisionHandler, zone)
        → validator.Bind(zone, car, level rules or defaults)
        → Starting (controls held for startDelay) → Playing (timer runs, collisions count, validator armed)
        → validator.Parked → Completed (stars + coins)   |   time up / collisions > limit → Failed
RestartLevel / NextLevel → LevelSelection.Request(level) → reload Gameplay (resets knocked-over cones)
```

No `Find*` calls during gameplay: every reference is Inspector-wired by the bootstrapper. Lookups happen once:
`GetComponent` on the spawned car, and the validator measures the car's footprint once when bound.
`LevelManager` and `LevelLoader` each keep a static `Instance`; a second copy logs a warning and removes itself.

## Setup steps

1. Open the project, then run **Tools ▸ Ultimate Car Parking ▸ Bootstrap Project**. The Console should end with `[Bootstrap] VERIFY OK`.
   This regenerates the car prefab, environment prefab, the two levels, the catalog and all scenes.
   It overwrites edits to generated assets, so for hand-tuned levels make new LevelData assets (step 4).
2. Open `Assets/_Project/Scenes/Gameplay.unity` and press Play. Level 1 loads; the debug overlay is top-left.
3. Drive into the yellow bay. The markers turn orange while you are partly in, angled or moving, light green
   while the hold timer runs, and green once parked.
4. To add a level: **Assets ▸ Create ▸ Ultimate Car Parking ▸ Level Data**, fill it in (below), then add it to
   `Levels/LevelCatalog.asset`. Level ids must be unique (the catalog warns about duplicates).
   To place a bay, put an empty GameObject on the bay centre in a scene with the environment prefab and
   copy its position and Y rotation into Target Position / Target Yaw.
5. To start on a level other than the first, set **Default Level Index** on `GameSystems ▸ LevelLoader`.
   (The Level Select screen in a later phase calls `LevelSelection.Request(level)` and loads Gameplay.)

## Inspector values

### ParkingValidator (GameSystems) — default rules
| Field | Default | Meaning |
|---|---|---|
| Edge Margin | 0.05 m | every footprint corner this far inside the bay lines |
| Angle Tolerance | 10° | max angle between car and bay axis |
| Allow Either Direction | on | nose-in and reversed-in both count |
| Max Speed Kmh | 2 | counts as stopped below this |
| Hold Duration | 1.5 s | must stay valid this long |
| Fallback Footprint | 1.8 × 4.3 m | only used if the car has no BoxColliders |

The footprint is measured from the car's non-trigger BoxColliders (PlayerCar: 1.8 × 4.3 m).

### LevelManager
Validator (wired), Loader (wired, needed for Next), **Start Delay 0.5 s** (controls held while the car settles).

### LevelLoader
Catalog, Default Level Index **0**, Load On Start **on**, Vehicle Prefab `PlayerCar`, **Spawn Lift 0.2 m**,
Zone Marker Material `LineTarget`, Level Manager, Vehicle Camera.

### CollisionHandler (PlayerCar)
| Field | Default | Meaning |
|---|---|---|
| Min Impact Speed | 0.3 m/s | softer contacts are ignored |
| Ground Normal Threshold | 0.7 | contacts facing up (ground, kerb tops) never count |
| Same Object Cooldown | 1 s | a scrape along one wall counts once |
| Ignored Layers | none | layers that never count |

### ParkingZone (created per level)
Size from LevelData, Trigger Height 3 m, Marker Width 0.15 m; colours idle yellow, occupied orange, holding
light green, parked green.

### LevelData
| Field | Level 1 | Level 2 |
|---|---|---|
| Level Id / Display Name | L01 / Level 1 — First Bay | L02 / Level 2 — Chicane |
| Environment Prefab | Env_TestLot | Env_TestLot |
| Spawn Position / Yaw | (−20, 0, 0) / 90° | (31, 0, 0) / 270° |
| Target Position / Yaw | (4.2, 0, 9.25) / 0° | (−4.2, 0, −9.25) / 180° |
| Target Size | 2.8 × 5.5 | 2.8 × 5.5 |
| Time Limit (0 = none) | 90 s | 75 s |
| Collision Limit (−1 = unlimited; fail when **above**) | 3 | 2 |
| Override Parking Rules | off | on: margin 0.05, angle 7°, either way, 1.5 km/h, hold 2 s |
| 3★ time / max collisions | 40 s / 0 | 45 s / 0 |
| 2★ time / max collisions | 65 s / 1 | 60 s / 1 |
| Base Coins / Coins Per Star | 100 / 50 | 150 / 60 |

Stars: 3 if time ≤ 3★ time **and** collisions ≤ 3★ max; else 2 if within the 2★ ceilings; else 1.
Coins = base + per-star × stars (Level 1: 150 / 200 / 250). Failure gives 0 stars and 0 coins.

## Events for the UI phase

`LevelManager`: `StateChanged(LevelState)`, `LevelStarted(LevelData)`, `LevelCompleted(LevelResult)`,
`LevelFailed(LevelResult)`, `CollisionCountChanged(int)`. Properties: `State`, `Level`, `ElapsedSeconds`,
`RemainingSeconds`, `Collisions`, `LastResult`, `HasNextLevel`, `Validator`.
Methods: `RestartLevel()`, `NextLevel()`, `CompleteLevel()`, `FailLevel(reason)`.
`ParkingValidator`: `StatusChanged`, `Parked`, `Status`, `HoldProgress`, `CornersInside`, `HeadingErrorDegrees`, `SpeedKmh`.

## Test checklist

Automated (`Tools/run_tests.sh`, last run 2026-11-05: **EditMode 23/23, PlayMode 37/37**):

- [x] Star rules at and around every boundary; two-star ceilings never below three-star (EditMode)
- [x] Bay geometry: centred / overhanging / margin / rotated bay / height ignored; heading error incl. reversed-in (EditMode)
- [x] Catalog order, next level, lookups by id; generated levels fully configured, two-star time reachable (EditMode)
- [x] Gameplay loads catalog[0]: environment, car at spawn, bay at target, camera on car, one manager, one car
- [x] Centred and stopped → Completed after the full 1.5 s hold, 3★, 250 coins, controls frozen
- [x] Reversed in → Completed; angled 7° → Completed
- [x] Nose 1 m past the line → PartiallyInside (2/4 corners), never completes
- [x] 0.1 m over the side line → PartiallyInside
- [x] Angled 11° (inside the lines, tolerance 10°) → Misaligned
- [x] Rolling at 5.4 km/h in the bay → TooFast, hold 0 %
- [x] Leaving at 52 % hold resets it; a full second hold is needed
- [x] 1.5 s time limit → Failed (TimeUp), 0 stars, controls frozen
- [x] Collision limit 0, drive into a wall → Failed (TooManyCollisions) with 1 collision
- [x] Pushing into a wall for 1.5 s counts once; then parking → Completed with 2★
- [x] Reversing over the kerb strip → 0 collisions
- [x] Next → Level 2 with its own rules (7°); Restart → timer and car reset; last level has no next
- [x] Duplicate LevelManager / LevelLoader remove themselves
- [x] Null level / vehicle / zone → logged errors, no exceptions, running level unaffected
- [x] All Phase 1 driving tests still pass

Manual, in the Editor (Gameplay scene, Play):

- [ ] Overlay shows "Level 1 — First Bay [Playing]", timer counting down from 90
- [ ] Drive to the yellow bay (drive along +X, it is on the left after the second pillar); markers go orange as you enter
- [ ] Stop straight inside: markers go light green, hold reaches 100 %, overlay shows PARKED ★★★ +250 coins, car is held
- [ ] Press N: Level 2 loads at the east end; drive through the barriers to the south bay
- [ ] Hit three things in Level 2 (limit 2): the third fails the level with TooManyCollisions
- [ ] Press R: cones and car reset, timer back to 75
- [ ] Wait out the timer: FAILED: TimeUp
- [ ] Scrape along a wall slowly: counts once, not once per frame

On device (2026-11-05, Galaxy A33): release APK installs, Level 1 loads (`[Level] Started L01` in logcat),
bay markers visible, no script errors. Driving on the phone needs the touch controls from the UI phase;
the debug overlay is hidden in release builds (build with **Build Android APK (Development)** to see it).

## Not in this phase

- No HUD, results screen or touch controls (UI phase). The debug overlay is a development tool only.
- Coins and stars are computed but **not saved or granted**; there is no save system yet.
- Level Select / Main Menu still empty; Bootstrap goes straight to Gameplay.
- Only one environment (the test lot); Level 2 reuses it.
