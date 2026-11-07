# Ultimate Car Parking 3D — Phase 3: levels, garage and progression

Builds on Phases 1–2. Vehicle, parking and level systems are reused; the only changes to existing runtime code are
additive: `VehicleController.ApplyHandling` (+ an overspeed brake so high-torque cars hold their limit),
LevelData gained `levelNumber`/`theme`/`props`, LevelLoader spawns the selected car and builds modular worlds,
LevelSelection carries the level's catalog, Bootstrap now opens the Main Menu.

## What was added

| Area | Scripts (Runtime unless noted) |
|---|---|
| Save | `Save/SaveData` (versioned, v1), `Save/SaveManager` (JSON in persistentDataPath, temp-file write + `.bak`, migration) |
| Progression | `Progression/ProgressionService` (unlocks, stars, rewards), `Progression/ProgressionRecorder` (Gameplay → save) |
| Cars | `Cars/CarData`, `Cars/CarCatalog`, `Cars/CarPaint`, `Cars/CarStats`, `Cars/GarageService`, `Cars/GarageManager`, `Vehicle/VehicleHandling` |
| Environment | `Environment/EnvironmentTheme`, `Environment/PropPlacement`, `Environment/EnvironmentBuilder`, `Environment/PaintVariant` |
| UI | `UI/UiPalette`, `UI/UiMotion`, `UI/ScreenTransition`, `UI/UIButtonFX`, `UI/SafeAreaFitter`, `UI/CoinCounter`, `UI/BackNavigation`, `UI/LevelCard`, `UI/LevelSelectController`, `UI/GarageController`, `UI/MainMenuController`, `UI/CarShowroom`, `UI/ShowroomDragArea`, `UI/StatBar` |
| Editor | `CarFactory` (5 cars), `EnvironmentKit` (16 modules + 3 themes), `LevelDesigner` (30 levels), `UiAssets` (TMP, fonts, sprites), `UiSceneBuilder` (menu scenes) |
| Tools | `Tools/extract_unitypackage.py` (TMP Essential Resources; `ImportPackage` never finishes in batch mode) |

## Generated content

- **Modules** `Prefabs/Modules/Mod_*.prefab`: Cone (3 kg rigidbody), Barrier (4 colours), Pillar (hazard-banded),
  Wall, DeckWall, Hoarding, ParkedCar (8 colours via `PaintVariant`), BayLine (white/yellow), WheelStop, Kerb,
  LightPole, Planter, Container (5 colours), SandPile, PipeStack, Beam. Pivot on the ground, +Z forward.
- **Themes** `Environment/Themes/Theme_*.asset`: City Lot (64×52, asphalt, concrete walls, light poles, planters),
  Sky Garage (60×48, deck concrete, parapet + rail, overhead lit beams, structural columns, haze, warm dim light),
  The Yard (66×54, dirt, hoarding fence, stacked containers, pipes, sand, low warm sun).
- **30 levels** `Levels/Level_01…30.asset` + `LevelCatalog.asset`, designed by rule (deterministic seeds):
  1–10 City, 11–20 Garage, 21–30 Yard. Difficulty 0→1 narrows bays 3.0→2.65 m and the aisle 13→8.5 m, fills
  45 %→92 % of bays, adds 0→6 aisle obstacles, tightens angle 12°→6°, hold 1.2→2.0 s, margin 0.05→0.08 m,
  collision limit 3→1, and the clock. **Parallel**: 5, 10, 15, 20, 25, 30. **Reverse in**: 8, 13, 18, 23, 27.
  Coins: base 80+10n, per star 20+2n. Spawn zones and each target's approach are kept clear.
- **Test fixtures**: the Phase 2 levels moved to `Levels/Test/` (ids T01/T02, `TestCatalog.asset`).
- **Cars** `Cars/Car_*.asset` + `CarCatalog.asset`, prefabs in `Prefabs/Vehicles`:

| Car | Body | Drive | Mass | Top | Torque | Steer | Price |
|---|---|---|---|---|---|---|---|
| City Sedan (`PlayerCar`, starter) | 1.8 × 4.3 m | FWD | 1300 | 60 | 1600 | 35° | free |
| Volt Hatch | 1.7 × 3.8 | FWD | 1050 | 55 | 1450 | 38° | 750 |
| Apex GT (spoiler) | 1.85 × 4.5 | RWD | 1250 | 75 | 2500 | 32° | 2000 |
| Ridge SUV (roof rails) | 1.95 × 4.7 | AWD | 1900 | 58 | 3000 | 33° | 3000 |
| Hauler (pickup bed) | 1.95 × 5.0 | RWD | 2000 | 55 | 3000 | 32° | 4500 |

  Five distinct primitive bodies from one parametric builder, each with its own paint material; suspension
  spring/damper scale with mass. The sedan is identical to Phase 1.

## Rules

- **Unlocks**: level 1 open; each level opens when the previous one (catalog order) is completed.
- **Rewards**: first clear = base + per-star × stars. Replay = per-star × (stars above previous best) + 20 % of base.
  Best stars and best time are kept. Failures record nothing.
- **Garage**: starter owned; buying needs coins ≥ price, deducts once, then the car can be selected; only owned
  cars can be selected; an unknown/unowned saved selection falls back to the starter.
- **Save** (`ucp_save.json`, version 1): coins, selectedCarId, ownedCarIds, per-level {bestStars, bestTime,
  completions}. Written after every completion, purchase and selection. Corrupt file → `.bak`; corrupt with no
  backup → fresh save; unversioned → migrated; newer version → read without downgrading.

## UI

Dark automotive palette (`UiPalette`: background #0B0D10, surfaces #14181D/#1C2128, electric lime #B6FF2E,
text #F2F5F7/#8C96A3). TextMeshPro with OFL fonts **Rajdhani Bold** (headings) and **Barlow SemiBold** (body)
— licences in `UI/Fonts`. Sprites are generated (rounded panel, pill, circle, star, lock, coin, chevrons, check,
fades). Canvas Scaler 1920×1080, match height (landscape), everything anchored to an edge or the centre, content in
a `SafeAreaFitter`. Fade transitions between scenes (`ScreenTransition`), press-scale on every button, staggered
card entrance, tab slide, garage car slide and stat-bar easing, shake on locked/unaffordable.

- **Main Menu**: title, progress, PLAY (lime) / GARAGE (outline), coins, selected car turning in a dark showroom.
- **Level Select**: back, title, star total, coins; 3 theme tabs with star progress; 5×2 grid of cards — locked
  (dim, lock icon), unlocked (lime outline, PLAY), completed (lime fill, stars, best time).
- **Garage**: car on a turntable (drag to rotate), name, tagline, 4 stat bars, status (IN USE / OWNED /
  AVAILABLE / NEED n MORE), action (SELECTED / SELECT / BUY price), prev/next, index dots, coins.
- **Gameplay**: as Phase 2 plus a Levels button (M) and Android Back → Level Select; the dev overlay shows coins earned.

## Setup

1. Open the project; run **Tools ▸ Ultimate Car Parking ▸ Bootstrap Project** (or `Tools/bootstrap.sh`, which also
   unpacks TMP Essential Resources the first time). Expect `[Bootstrap] VERIFY OK` — it checks 30 levels, 5 cars,
   themes, fonts, sprites and every serialized reference in the three menu controllers.
2. Press Play in `Bootstrap.unity` → Main Menu.
3. To reset progress delete `ucp_save.json` from `Application.persistentDataPath`
   (macOS editor: `~/Library/Application Support/Ultimate Parking/Ultimate Car Parking 3D/`).
4. To add a level: create Level Data, choose a Theme, add Props (any `Mod_*` prefab, position, yaw, scale,
   colour variant), set spawn/target, add it to `LevelCatalog`. To add a car: build a prefab like the generated
   ones (Rigidbody, VehicleController, VehicleInput, WheelVisuals, CollisionHandler, CarPaint), create Car Data,
   add to `CarCatalog`. Hand-made assets are not touched by the bootstrapper; generated ones are overwritten.

## Acceptance tests

`Tools/run_tests.sh` — EditMode 48, PlayMode 83 (see the report for the latest result).

- Save round-trip after "restart" (new SaveManager on the same file), atomic write, backup fallback, fresh-on-corrupt,
  v0→v1 migration, newer-version read, repair of broken entries.
- Unlock chain, first-clear and replay rewards, best stars/time kept, failures ignored, star totals per theme.
- Garage: starter owned + selected, purchase needs coins and charges once, only owned cars selectable, fallback.
- Content: 30 levels (10/10/10 themes, ids, numbers), all props are `Mod_*` modules, cones/barriers/pillars/parked
  cars/lines used, theme walls are modules, difficulty ramps, parallel/reverse levels, every bay fits every car,
  five distinct cars with distinct paints/handling, starter = Phase 1 car.
- **Every one of the 30 levels** in Gameplay: world built, car at spawn, spawn and bay unobstructed, bay reachable
  from the spawn by a 2 m-wide footprint (grid search), placed car completes the level.
- **Every car**: own top speed respected, stays upright through slalom/full lock/reverse; fits level 29's bay and
  level 30's parallel slot.
- Flows: clearing L01 pays 3★ coins and unlocks L02 after restart; Garage buy + select persists and Gameplay then
  spawns that car with its paint and mass; Level Select shows completed/unlocked/locked, locked does nothing, a
  card opens its level; Main Menu shows progress/coins/car and its buttons navigate; duplicate managers remove themselves.
