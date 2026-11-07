# Ultimate Car Parking 3D — implementation report

Date: 2026-11-06. Unity 6000.5.9f1, URP, Android IL2CPP ARM64, package `om.ultimateparking.carparking3d`.
Device used: Samsung Galaxy A33 (SM-A336E, Vulkan, 8 cores, 5.4 GB RAM, 2400×1080).

**Rule used in this report:** a feature is "done" only if a test or an on-device check below exercised it. Everything else is listed as partial or not done.

## 1. Headline status

| Area | Status |
|---|---|
| World Tour UI (30 countries, map, cards, mission picker) | ✅ done, tested in PlayMode and on the device |
| Country arenas | ⚠️ **10 of 30 built** (India, USA, UK, Japan, UAE, China, South Korea, Thailand, Singapore, Malaysia — 3 missions each). The other 20 have no assets yet |
| Missions | ⚠️ **30 of 90 built** (the first 10 countries). The other 60 are planned in [MISSION_PLAN_90.md](MISSION_PLAN_90.md) |
| Reusable mechanics | ✅ 11 of 20 implemented and tested (see MISSION_PLAN_90.md); 3 partial; 6 not started |
| Country progression, rewards | ✅ done, tested |
| Mission progression, retry, next, stars, rewards | ✅ done, tested |
| Garage: selection, unlocks, upgrades | ✅ done, tested (upgrades new) |
| Android build (AAB with Play Asset Delivery) | ✅ builds, installs and runs on the device |

## 2. What was implemented in this pass

### Mechanics batch 1 (new C#, all tested)
- `PrecisionParkObjective`: accuracy grading (centring, depth, heading) with a minimum to count as parked. Used by IN3.
- `TimedParkObjective`: its own countdown; hits cost seconds; running out fails; scored on time left.
- `MovingBarrier` + `BarrierGateObjective`: imported barrier models animated by C# (slide / sink / swing), with a lane safety sensor. Barrier hits fail the mission.
- `NarrowAlleyObjective`: side-gap sensors, wall-scrape limit, a reverse-through variant; scored on the tightest gap.
- `ValetParkObjective`: valet-stand handover, then park; guest rating drops for speeding, harsh driving and hits.
- Quality scoring: `IScoredObjective` → `MissionRunner.Quality / ScoreLabel` → `LevelData.scoreMode` → `StarRating` → results screen label (e.g. "ACCURACY 93%").

### Progression and garage
- **Country rewards:**
  - Completion bonus (+500) and all-three-stars bonus (+750), each paid once.
  - Stored in save v4 (`countryRewardIds`).
  - Shown on the World Tour card ("COUNTRY BONUS" chip) and on the results screen ("INDIA COMPLETE +500" / "INDIA ALL STARS +1250").
- **Completion percentage** on the country card ("33% DONE").
- **Car upgrades:**
  - Engine, Brakes and Steering, 3 levels each (250 / 500 / 900 coins).
  - Saved per car; applied when the car spawns.
  - Steering also tightens the turning circle.
  - Garage row with LV x/3 and price; the stat bars include upgrades.
- **Save v3 → v4 migration**, tested.

### Android
- **AAB with Play Asset Delivery:** the arenas ship in an asset pack, not in the base module.
  - Fix: `splitApplicationBinary` is now set for AAB builds. Without it, Addressables kept the arenas in base.
- **Critical bug found on the device and fixed:** with asset packs, the Play Asset Delivery provider refuses synchronous loads.
  - Effect: every Mumbai mission failed to load its arena on the phone.
  - Editor tests and the old APK installs could not show it.
  - Fix: `LevelLoader` now loads arenas asynchronously.
- **Arena optimiser**, run when each mission prefab is baked:
  - Small props and ground tiles no longer cast shadows.
  - LOD-group distance culling on every prop.
  - Hidden high-poly details stripped (stall spare wheels: −65k triangles per mission).
- **`PerfMonitor`** for on-device FPS and memory sampling. It is off unless a flag file is present.
- **Glyph fixes:**
  - ✓ in the objective list rendered as an empty box on the phone.
  - ★ was used in the new bonus text and would have done the same.
  - Both are replaced with text.

## 3. Test results

| Suite | Result |
|---|---|
| EditMode | **78/78** |
| PlayMode | **147/147** |

### The 10 required checks

| # | Check | Evidence |
|---|---|---|
| 1 | All country selections | `WorldTourTests`: 30 markers, region filters, locked/unlocked states, selection card, mission picker. Device: World Tour screen, India card, bonus chip |
| 2 | Arena loading | `CountryArenaMissionTests` and `CountryArenaLoaderTests` (Addressables). **Device: Mumbai IN1 loads from the asset pack** after the async fix |
| 3 | Mission launching | World Tour → Play Mission 1 on the device; PlayMode tests launch IN1–IN3 through the real flow |
| 4 | Vehicle controls | `VehicleDrivingTests`, `VehicleKeyboardInputTests`; device: gas held 6 s, drove through the market |
| 5 | Parking validation | `ParkingLevelTests` (overhang, angle, speed, hold), `MissionTests`, `MechanicsBatch1Tests` |
| 6 | Mission completion | Every IN mission completes in `CountryArenaMissionTests`; IN3 completes with "ACCURACY 100%" in `ProgressionFlowTests` |
| 7 | Failure and retry | Time-up, collision-limit, kerb strikes, out of time, barrier hit, wall scrapes: all fail correctly in tests; retry path in `GameplayUiTests` |
| 8 | Country unlocking | `CountryArchitectureTests` (sticky unlocks), `WorldTourTests` |
| 9 | Save and reload | `ProgressionFlowTests` (stars, coins, garage, upgrades, country bonus survive a reload), v2→v3→v4 migrations |
| 10 | Android build | AAB built (109 MB), installed with bundletool on the A33, launched, played |

### 3a. Final full run
Run after every fix in this report (async arena loading, glyph fixes, bonus chip): EditMode 78/78 and PlayMode 147/147, with 0 failures and 0 skipped tests.

## 4. Android performance report (Galaxy A33, Vulkan, 2400×1080)

| Screen | Avg FPS | 1% low | Worst frame | Unity heap |
|---|---|---|---|---|
| Main menu | 53.2 | 29.9 | 602 ms (scene load) | 78 MB |
| Mumbai IN1, first 10 s (arena load, idle) | 57.1 | 29.8 | 134 ms | 110 MB |
| Mumbai IN1, driving through the market | **59.8** | **59.7** | 17 ms | 106 MB |

Measured with `PerfMonitor` (10-second windows, logcat). Target frame rate 60.

**Arena prefab sizes after optimisation** (from `ArenaOptimizer.Report`, totals for the whole arena — the camera sees a fraction):

| Mission | Renderers | Shadow casters (before → after) | Triangles (before → after) | LOD groups | Colliders |
|---|---|---|---|---|---|
| IN1 | 371 | 391 → 213 | 272k → 207k | 87 | 92 box, 0 mesh |
| IN2 | 313 | 333 → 155 | 206k → 141k | 81 | 86 box, 0 mesh |
| IN3 | 680 | 700 → 490 | 634k → 570k | 133 | 138 box, 0 mesh |

**Download size:**
- AAB: 109 MB.
- Base module: about 103 MB uncompressed. It was 320 MB before the texture conversion and asset packs.
- Mumbai arena asset pack: 30 MB.
- The base module is under Play's 200 MB limit.

**Not done / limits, stated honestly:**
- **No baked lighting.** Arenas are prefabs loaded at runtime through Addressables, and Unity lightmaps belong to scenes. The lighting is one realtime directional light with hard shadows (45 m, one cascade, 1024 map) plus ambient light.
- **No mesh LODs.** The imported models each have a single LOD and there is no decimation tool in the project. "LODs" here means distance culling only.
- **No static batching.** Runtime-loaded prefabs cannot use build-time static batching. The SRP Batcher is on.
- **Draw calls were not measured on the device.** Unity's render counters read 0 in the headless test runner. The FPS figures above are the real measurement.
- **IN3 is still triangle-heavy (570k).** The next step would be swapping or decimating the heaviest meshes: the taxi (15k triangles × 12) and the old house (13.7k).

## 5. Scenes

`Bootstrap`, `MainMenu`, `WorldTour`, `LevelSelect`, `Garage`, `Gameplay`, `Arenas/Arena_TestB` (architecture test country). All are generated by `Tools/bootstrap.sh`; country arenas are Addressable prefabs (`Prefabs/Arenas/<Arena>/Missions/Env_<id>.prefab`), not scenes.

## 6. Scripts

105 runtime, 30 editor, 29 test files (counts at the time of writing). By area:

- **Missions** (`Runtime/Missions`):
  - Objectives: `MissionObjective`, `ParkObjective`, `ReverseParkObjective`, `ParallelParkObjective`, `PrecisionParkObjective`, `TimedParkObjective`, `ValetParkObjective`, `CheckpointObjective`, `FloorParkObjective`, `NarrowAlleyObjective`, `BarrierGateObjective`, `MechanicalParkObjective`.
  - Machines: `MechanicalParkingSystem`, `MovingBarrier`.
  - Framework: `MissionRunner`, `MissionContext`, `MissionGauge`, `IScoredObjective`, `MissionType`, `ParkingSensor`, `ParkingSensorAudio`.
- **Levels:** `LevelData`, `LevelLoader` (async arena loading), `LevelManager`, `LevelCatalog`, `LevelSelection`, `LevelResult`, `StarRating`.
- **World Tour:** `CountryData`, `CountryCatalog`, `CountryProgressManager` (percentage, rewards), `CountryUnlockManager`, `CountrySelectionManager`, `CountryArenaLoader`, `CountryArenaRoot`, `CountryArenaTypes`, `WorldTourService`, `WorldMapData`, `WorldMapProjection`.
- **Cars:** `CarData`, `CarCatalog`, `CarStats`, `CarPaint`, `GarageService`, `GarageManager`, `CarUpgrades`.
- **Progression and save:** `ProgressionService`, `ProgressionRecorder`, `SaveData` (v4), `SaveManager`.
- **Vehicle:** `VehicleController`, `VehicleHandling`, `VehicleInput`, `CollisionHandler`, `WheelVisuals`.
- **Parking:** `ParkingValidator`, `ParkingZone`, `ParkingRules`, `ParkingGeometry`, `ParkingStatus`, `IParkingCondition`.
- **Environment:** `TrafficSide`, `EnvironmentBuilder`, `EnvironmentTheme`, `KerbMarker`, `ParkedFleet`, `PropPlacement`, `PaintVariant`, `SceneLightingOverride`.
- **UI:**
  - `MainMenuController`, `WorldTourController`, `WorldMapView`, `WorldMapGraphic`, `CountryMarker`, `LevelSelectController`, `LevelCard`, `GarageController`, `CarShowroom`, `SettingsPanel`.
  - Gameplay: `GameplayHud`, `GameplayUiController` (pause / results / fail), `MobileControls`, `ParkingGuide`.
  - Shared helpers: `ScreenTransition`, `UiMotion`, `UiPalette`, and others.
- **Debug:** `LevelDebugOverlay`, `PerfMonitor`.
- **Editor:**
  - Bootstrap and scenes: `ProjectBootstrapper`, `UiSceneBuilder` (+ `.Gameplay`, `.WorldTour`, `.Bootstrap`).
  - Arenas and levels: `WorldTourBuilder`, `MumbaiArenaBuilder`, `ArenaPropFactory`, `ArenaLevelWriter`, `ArenaOptimizer`, `ArenaDelivery` (Addressables / Play Asset Delivery), `CountryArenaTestBuilder`.
  - Build: `BuildScript`.
  - Other factories and builders: `RealPropFactory`, `RealCarFactory`, `CarFactory`, `DrivingSchoolBuilder`, `MissionLevelBuilder`, `LevelDesigner`, `MaterialProbe`.
  - `StoreScreenshotBuilder` was added by another session and is not part of this work.

## 7. Imported asset catalog and licences

- Research catalog (candidates, licences checked against source APIs): [ASSET_CATALOG.md](ASSET_CATALOG.md) / [ASSET_CATALOG.csv](ASSET_CATALOG.csv).
- **Licence records for everything in the project:** [LICENSES.md](LICENSES.md), a copy of `Assets/_Project/ThirdParty/ATTRIBUTION.md`.
  - 75 arena models (Mumbai, New York, London, Japan, plus the 9 downloaded Dubai models). All are CC BY 4.0.
  - Title, author and source for each were read from the file's embedded metadata.
  - Also covered: the earlier car, garage, street and barrier packs (CC BY 4.0 / CC0), Poly Haven and AmbientCG textures (CC0), flag-icons (MIT) and Natural Earth (public domain).

## 8. Known issues

1. **Only India has an arena.** The other 29 countries reuse the campaign levels. The New York, London, Tokyo and Dubai arenas still need building (assets ready). The other 25 need asset research and your download approval.
2. **No in-game Credits screen.** CC BY 4.0 requires visible attribution; the records exist (ATTRIBUTION.md), but a Credits screen must be added before a store release.
3. **Mechanics not started:** multi-car, hill, snow, rain, night, spiral ramp, trailer and moving traffic. Partial: pickup rule, exam sheet, large vehicle.
4. **No baked lighting, no mesh LODs, no static batching** for arenas (see §4).
5. **First-load hitch:** about 0.5 s while an arena loads (worst frame 134 ms; 1% low 30 fps in the first 10 s). There is no loading indicator on the gameplay screen during the async arena load.
6. **The custom asset-pack name is ignored:** Mumbai's arena lands in the default `AddressablesAssetPack` rather than `arena_in`. This is harmless for install-time delivery. Per-country on-demand packs would need this fixed first.
7. **Render counters read 0 in headless tests,** so draw-call numbers come from prefab analysis, not from the device.
8. **bundletool local-testing warning** about splitcompat ("package not debuggable") on install. Harmless for a release build with no dynamic feature modules.

## 9. Build and install

Prerequisites: Unity 6000.5.9f1 with Android Build Support (bundled SDK, NDK, OpenJDK), and `adb` on PATH.

Regenerate the project content (scenes, prefabs, levels, Addressables groups):

```bash
Tools/bootstrap.sh
```

Run all tests:

```bash
Tools/run_tests.sh both
```

Build the Play Store App Bundle (`Builds/UltimateCarParking3D.aab`, arenas in asset packs):

```bash
Tools/build_android.sh
```

Build and install on a connected phone (uses Unity's bundled bundletool with `--local-testing`):

```bash
ANDROID_SERIAL=RZCT929739K Tools/build_android.sh install
```

Quick single APK for sideloading (arenas inside the APK, ~239 MB, `Builds/UltimateCarParking3D.apk`):

```bash
Tools/build_android.sh apk
```

On-device FPS logging:

```bash
adb shell touch /sdcard/Android/data/om.ultimateparking.carparking3d/files/perfmon
```

```bash
adb logcat -s Unity | grep PerfMon
```

Signing: the build uses the debug keystore. Configure a release keystore in Player Settings before uploading to Play.
