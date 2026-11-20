# Ultimate Car Parking 3D: premium upgrade, Phases 1–4

Date: 2026-11-19. Unity 6000.5.9f1, URP, Android. Everything below was measured in the Unity editor (batch mode, automated tests) unless it says otherwise. **No frame-rate or thermal figures from a phone are included: none were measured for this upgrade.**

## Test results (latest runs)

| Suite | Result |
|---|---|
| EditMode | 186 / 186 passed |
| PlayMode, full suite | 284 / 285 passed. The one failure (monster-truck camera framing) was fixed; the camera classes then re-ran 8 / 8 passed |
| Bootstrap verification (scenes, wiring, catalog) | VERIFY OK |
| Vehicle benchmark (20 cars) | passed; 30 fps vs 120 fps rendering within 2.7 % |
| Frame-allocation check | **Inconclusive**: the GC counter reports 0 in the editor runtime, so per-frame garbage could not be measured here |
| Draw calls per arena | **Not measured**: batch-mode tests do not render (counters read 0) |

## Phase 1: driving controls and HUD

- Steering modes: on-screen wheel (default; grabbed by one finger, self-centres, 270° to full lock at sensitivity 1), Left/Right buttons, device tilt (calibrated straight-ahead, 2.5° dead zone, smoothing). Ackermann and speed-dependent lock unchanged.
- Pedals: separate Gas, Brake, Handbrake and a P / R / D selector. The brake never reverses; gears change only below 1.5 km/h ("STOP TO CHANGE GEAR" otherwise). Classic combined Brake/Reverse kept as an option, now guarded: a press made while rolling only brakes until released.
- Multi-touch: every control tracks its own finger (tested: wheel + gas held together).
- HUD: speedometer with gear letter, camera VIEW and ZOOM buttons, objective progress ("OBJECTIVES 1/3"), restyled pedals and wheel.
- Settings → Driving Controls: steering mode, pedal mode, sensitivity, control size, opacity, height, left/right-handed layout, tilt calibration, reset. Saved in the save file; older saves get the new defaults.
- Keyboard controls unchanged.

## Phase 2: driving physics and 20-car balance

Per-car chassis (suspension frequency and damping, travel, tyre grip, anti-roll, steering return, low-speed launch limit), ABS, traction control, a progressive pedal curve and a weight-scaled standstill hold. Prices locked (FleetPricing.Pinned) to the values players see.

Before → after, from `VehicleBenchmarkTests` (flat pad, same inputs for every car):

| Car | Price | 3.5 s distance (m) | 0–20 km/h (s) | Dry stop 40→0 (m) | Wet stop (m) | Turn radius (m) | 0.5 s pedal tap creep (m) |
|---|---|---|---|---|---|---|---|
| Harbor Sedan | 0 | 19.1 → 17.5 | 1.83 → 1.97 | 7.0 → 7.3 | 8.9 → 8.9 | 4.51 → 4.52 | 1.93 → 1.18 |
| Volt Hatch | 950 | 22.5 → 20.6 | 1.58 → 1.73 | 7.1 → 7.4 | 8.7 → 8.9 | 3.71 → 3.52 | 2.47 → 1.50 |
| Lumen Wagon | 3,500 | 25.7 → 22.5 | 1.42 → 1.67 | 7.1 → 7.2 | 9.0 → 8.7 | 4.67 → 4.67 | 3.52 → 1.42 |
| Vesper Coupe | 8,500 | 27.5 → 25.7 | 1.33 → 1.47 | 7.4 → 7.2 | 8.7 → 8.6 | 4.42 → 4.39 | 3.81 → 2.08 |
| Ridge SUV | 13,000 | 29.1 → 21.7 | 1.28 → 1.70 | 7.5 → 7.4 | 9.1 → 8.9 | 4.98 → 4.98 | 5.36 → 1.21 |
| Urban '10 | 14,650 | 28.3 → 23.4 | 1.32 → 1.63 | 7.2 → 7.5 | 9.0 → 9.0 | 4.75 → 4.77 | 4.48 → 1.18 |
| Bokaroo '67 | 16,350 | 28.6 → 24.1 | 1.30 → 1.60 | 7.4 → 7.4 | 8.7 → 9.1 | 4.07 → 4.06 | 4.69 → 1.36 |
| Fairheaven LT '80 | 18,000 | 31.9 → 28.7 | 1.18 → 1.38 | 7.5 → 6.4 | 9.2 → 6.9 | 4.90 → 4.88 | 5.18 → 2.32 |
| Phoenix 455 '71 | 19,650 | 34.0 → 30.2 | 1.13 → 1.35 | 7.1 → 6.4 | 9.2 → 6.5 | 4.65 → 4.62 | 5.81 → 2.39 |
| Tiara GT '83 Tuned | 21,350 | 37.2 → 33.5 | 1.05 → 1.23 | 7.2 → 7.1 | 8.7 → 8.8 | 4.22 → 4.22 | 6.16 → 2.79 |
| Asti Stradale '89 | 23,000 | 36.0 → 32.7 | 1.08 → 1.25 | 7.5 → 5.9 | 12.3 → 8.7 | 4.50 → 4.47 | 5.79 → 2.74 |
| Roadster '00 | 24,650 | 37.9 → 33.7 | 1.05 → 1.23 | 7.6 → 7.0 | 12.5 → 9.0 | 4.04 → 4.01 | 6.03 → 2.73 |
| Chapman '73 | 26,350 | 40.7 → 36.6 | 0.97 → 1.18 | 7.5 → 5.0 | 13.0 → 8.8 | 4.93 → 4.88 | 6.99 → 2.93 |
| Apex GT | 28,000 | 32.5 → 30.4 | 1.17 → 1.30 | 7.5 → 6.8 | 9.1 → 8.7 | 4.78 → 4.75 | 5.22 → 2.87 |
| Mammoth '80 Monster | 32,000 | 22.5 → 18.3 | 1.60 → 1.95 | 7.2 → 7.1 | 8.9 → 8.4 | 3.95 → 4.66 | 1.67 → 1.00 |
| Goliath '85 Monster | 36,600 | 24.2 → 19.5 | 1.48 → 1.87 | 7.0 → 6.8 | 9.1 → 8.8 | 4.05 → 4.62 | 2.48 → 1.03 |
| Saba V12 '95 | 41,200 | 36.4 → 32.5 | 1.08 → 1.28 | 7.6 → 5.6 | 13.1 → 8.9 | 4.50 → 4.47 | 6.45 → 2.72 |
| Cheetah '84 | 45,800 | 37.9 → 33.5 | 1.05 → 1.25 | 7.6 → 5.6 | 13.1 → 8.3 | 4.63 → 4.60 | 6.62 → 2.85 |
| Exterminator '00 | 50,400 | 41.3 → 37.0 | 0.98 → 1.15 | 7.2 → 7.1 | 13.4 → 12.6 | 4.62 → 4.58 | 8.56 → 3.69 |
| Velocity F1 | 55,000 | 39.8 → 36.9 | 1.00 → 1.15 | 7.5 → 7.6 | 8.7 → 9.1 | 5.38 → 6.24 | 6.58 → 3.39 |

Full rows (snow braking, reverse, standstill drift): `Documentation/vehicle_specs.csv` (after) and `../UltimateCarParking3D_logs/vehicle_benchmark_before.csv`.

Trade-offs to know:
- Straight-line acceleration is 7–25 % lower (largest on the SUVs and monster trucks): the low-speed launch limit makes a pedal tap move every car 1–3.7 m instead of up to 8.6 m.
- Snow braking is 0.5–2 m longer on sports and muscle cars (ABS in very low grip); shorter or unchanged on the rest.
- Exterminator '00 still needs 12.6 m on a wet road.
- Standstill drift is 0.0–0.4 mm on every car.

## Phase 3: cameras and parking assistance

- Views: Chase, Top-down, Bumper, Cockpit (12 of 20 cars, the ones whose model has a steering wheel and interior), Orbit (one-finger drag). Pinch zoom 4–14 m. Smooth view transitions. View and distance remembered. Framing scales with the visible size and height of the car.
- Verified: every view with every garage car, no clipping into the car or ground.
- Reverse camera: unchanged behaviour (renders only while reversing, mirrored, steering-based guide lines), resolution follows the quality tier, "STOP" under 0.3 m, guide lines hidden in Expert mode.
- Parking sensors: three zones at each bumper (6 box casts at 10 Hz), coloured bars, nearest distance, beeps (generated tone; can be turned off). Measured: wall at 1.00 m → 0.95 m.
- Assistance levels: Beginner (adds angle/side alignment line), Standard, Expert (no sensors, bay chevron or guide lines).
- Collisions: contacts are scrape or impact (feedback strength); a crash into several colliders of one object within 0.4 s counts once (tested). Under 0.3 m/s still never counts.

## Phase 4: garage, progression, polish

- Garage spec sheet from the benchmark (not estimates): 0–20 km/h, dry and wet stop from 40, turning radius, top speed, size, parking difficulty (1–5), strength line. COMPARE shows differences against the car in use. Pinch / wheel zoom in the showroom.
- Progression: prices locked, coins and purchases untouched, existing Engine/Brakes/Steering upgrades kept (no new monetisation).
- Parking target: bay lines breathe while holding and pop when parked.

## Android optimisation status

Done in code: sensors throttled to 10 Hz, rear camera renders only when shown, HUD text rewritten only on change, no per-frame allocations introduced in the new HUD paths by design (StringBuilder reuse, cached arrays). **Not done:** a profiling session on the Galaxy A33 (frame time, GPU, thermal). That needs the game launched on the phone with the perf monitor; it has not been run for this upgrade.

## Remaining issues

1. No on-device measurement of FPS, thermal or touch latency for this upgrade.
2. Tilt steering cannot be exercised in the editor (no accelerometer); needs a hands-on check.
3. On 4:3 tablets the speedometer and sensor widget may overlap the controls.
4. Exterminator '00 wet braking (12.6 m).
5. Testing unlocks are still on (TestingUnlocks.DefaultUnlockAll = true) and AdMob uses test IDs: both must change before a store release.

## Release readiness

Not release-ready yet: the code paths are covered by automated tests, but the release blockers are items 1, 2 and 5 above.

---

# Phase 5: advanced parking missions and realistic scoring (2026-11-19)

## What was already there (audited, reused)
Objective types: standard, reverse, parallel, precision, timed, valet, multi-storey floor, checkpoint, narrow alley, barrier gate, mechanical parking; night/rain/snow/fog conditions; quality-based stars. The multi-storey-with-ramps challenge is covered by the existing garage and World Tour missions; no new multi-storey environment was built.

## Added
- **Realistic parking scorecard** (`ParkingScorecard`): centring 35 % (measured against the room *that car* has in *that bay*, so a monster truck and a hatchback are judged on the same scale), alignment 25 %, collisions 25 % (impacts cost 0.25, scrapes 0.10), time 10 %, stillness 5 %. Shown on the results ("PARKING 96% · CENTRE 87% · ANGLE 0.0° · HITS 0"). New `ScoreMode.Parking`; older levels keep their own star rules.
- **Parking brake rule** (`ParkingRules.requireParkingBrake`): the hold only counts in P or with the handbrake on.
- **Difficulty tiers** (Beginner / Intermediate / Advanced / Expert): bay size, angle tolerance, hold time, hit limits, star scores, and an assistance cap (Advanced: no alignment hint; Expert: no sensors, bay chevron or guide lines).
- **Driving exam objective** (`ExamRouteObjective`): gates in order, speed limit (fault after 1 s over), full stop at stop lines, more than the allowed faults fails.
- **New mission types**: Obstacle course, Heavy vehicle.
- **Challenge chapter** (`ChallengeLevels`, a data template per level): 12 levels, campaign levels 31–42, in the existing Driving School yard with existing props:

| Level | Name | Difficulty | Type | Notes |
|---|---|---|---|---|
| C01 | Wide Open | Beginner | Standard | wide bay, guides |
| C02 | First Reverse | Beginner | Reverse | |
| C03 | Between Two | Intermediate | Parallel | between two parked cars |
| C04 | Cone Course | Intermediate | Obstacle course | 3 cone gates, then park |
| C05 | Rush Hour | Intermediate | Timed | 40 s, hits cost 3 s |
| C06 | Tight Squeeze | Advanced | Reverse | between cars, 0.6 m to the wall |
| C07 | Night Precision | Advanced | Precision | night |
| C08 | Wet Kerb | Advanced | Parallel | rain |
| C09 | Snow Valet | Advanced | Valet | snow, pickup then delivery |
| C10 | Three Points | Expert | Multi-stage | nose-in, reverse, parallel |
| C11 | Heavy Metal | Expert | Heavy vehicle | monster truck forced |
| C12 | Driving Exam | Expert | Exam | 20 km/h limit, stop line, finish in P |

## Tests (actual results)
- EditMode 193 / 193 (new: scorecard fairness, star rules, tier ordering, bays fit every car, chapter data, existing levels unchanged).
- PlayMode: every challenge level played through objective by objective in the real Gameplay scene and completed with a scorecard (12 / 12); clean vs sloppy park on C01: 3★ (1.00) vs 2★ (0.70); restart clears the score; best result kept; exam: rolling through the stop line = 1 fault, parking only counts with the handbrake. Related suites re-run: 145 / 146, the one failure (menu expected "/ 30 LEVELS", now 42) fixed and re-run 7 / 7.
- **Not proven:** that a person can comfortably drive each route by hand. The playthroughs place the car at each target; they test detection, objectives and scoring, not drivability. Needs hands-on play.

## Also changed in this round
- Default pedals are now **Simple** (GAS + BRAKE/REVERSE); P/R/D is an option in Settings → Driving Controls → Pedals.

---

# Phase 6: career mode and Driving Academy

- **Five chapters** over the existing 42 missions (referenced, not copied; campaign catalog, ids, stars and times unchanged): Beginner Academy (8), City Driver (8), Professional Valet (8), Heavy Vehicle Expert (8), Parking Master (10). A chapter opens once 6 missions of the previous one are done; missions open in order; finished missions can always be replayed.
- **Rewards**: chapter completion 1,500 / 3,000 / 5,000 / 7,500 / 10,000 and mastery (all three stars) 750 / 1,500 / 2,500 / 3,500 / 5,000, each paid once. Economy (measured): campaign 3★ 17,819 + arena missions ≈37,400 + countries 40,000 + chapters 40,250 ≈ 136,000 coins one-off, ≈28 % of the 478,950 needed for every car. Car prices unchanged.
- **Best score and time** per mission (save field `bestQuality`, version 5). Old saves migrate automatically (coins, cars, stars, country rewards kept); rewards for chapters an older save already finished are paid once when the career screen opens.
- **Driving Academy**: 7 hands-on lessons (steering, gas and brake, gears and reverse, reverse parking, parallel parking, parking sensors, camera views), each step completed by doing it.
- **Career screen** (MISSIONS on the main menu): chapter cards with progress, stars and state, mission cards, mission details with best score / time and PLAY / REPLAY, reward pop-up; the results screen shows chapter rewards.
- Tests: EditMode career rules, one-off rewards, v4→v5 migration, economy; PlayMode career screen, rewards on screen, play / replay, academy lessons played for real (steering, pedals, reverse, sensors, cameras): passing.

# Parking assistance gaps (second "Phase 7" brief)

- Gentle contacts now show "LIGHT TOUCH · NO PENALTY"; counted contacts say "SCRAPE" or "HIT!". Counting rules unchanged.
- Rear camera: 800 × 450 on mid-range phones (was 640 × 360), eased entrance, frame tinted amber / red as the gap closes.
- Sensor beam raised to 0.35–0.9 m so kerbs are ignored.
- Measured: wall 0.80 m → 0.75; parked car 1.50 m → 1.45; monster truck and F1 1.00 m → 0.95; moving obstacle tracked 1.80 → 0.30; kerb and narrow-garage side walls ignored; player's own car never detected (4 shapes); guide lines vs the real reversing path: worst gap 0.05 m over 0.5–3 m.

# Phase 8: HUD

- HUD size setting (85–115 %), compact objectives (current one, tap SHOW ALL; collapses while the rear camera is up), responsive layout (controls shrink to at most 70 % on narrow screens; speedometer and sensors centred in the gap; top-centre column narrows), thin light edges on the main panels.
- Layout check (editor, canvas emulation): 30 layouts — 16:9, 20:9, 19.5:9 with a notch, 16:10, 4:3 × right/left-handed × default / largest / smallest sizes — every essential element inside the safe area, no overlaps (after fixing the 32 overlaps the check first found).
- Not yet checked on a real phone screen.

---

# Phase 7: vehicle customisation and immersion

- Garage CUSTOMIZE panel: paint (12 colours + factory), finish (gloss / metallic / matte), rims (chrome, gloss black, gunmetal, gold, bronze), window tint (light / dark / limo), headlight colour, custom plate text (2–8 characters). Live preview on the turntable, two-tap purchase (BUY → CONFIRM), free re-fit of bought options, free reset to factory. Prices 150–700 coins (a full custom look < 3,500). Saved per car (save version 6, automatic migration).
- Applied by MaterialPropertyBlock per material slot: no material instances; shared materials untouched (parked scenery cars keep their look).
- Measured on all 20 cars: paint, tint and rims on 19; Velocity F1 paint only (no glass; its wheels share a plain material).
- Working lights on every car (headlights, tail / brake, reverse, indicators, hazards), horn (hold), engine start / stop (off: no drive, brakes still work).
- Synthesised audio (no downloads; existing clips byte-identical): horn, indicator tick, engine start, tyre squeal from real sideways slip, rain / wind / city ambience.
- Weather: automatic headlights at night / rain / fog / snow; wet or snowy ground surfaces (72 surfaces in the rain test level).
- Not done: animated doors / bonnet / boot (models are single merged meshes), decals.
- Tests: EditMode 203 / 203; PlayMode customisation and related suites 41 / 41; HUD layouts 30 / 30.
