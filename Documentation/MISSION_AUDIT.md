# Mission variety — Phase A audit

Audited on 2026-11-05 against the working tree (commit 90ac5ed plus later uncommitted asset work).

## How a level works today

| Piece | File | What it does |
|---|---|---|
| Level config | `Runtime/Levels/LevelData.cs` | One target bay (position, yaw, size), spawn, timer, collision limit, optional `ParkingRules` override, stars, coins. |
| Level generator | `Editor/LevelDesigner.cs` | Builds all 30 levels from one formula. Difficulty `d = 0..1` narrows bays, adds parked cars and aisle obstacles, tightens tolerances. |
| Win check | `Runtime/Parking/ParkingValidator.cs` | Four footprint corners inside the bay, heading within tolerance, speed under 2 km/h, held for N seconds. |
| Flow | `Runtime/Levels/LevelManager.cs` | Starts, counts time and collisions, completes on `validator.Parked`, fails on time or collisions. |
| HUD | `Runtime/UI/Gameplay/GameplayHud.cs` | Level label, timer, hits, gear, one guide line driven by the validator status. |

## Repeated mission logic

1. **One objective for every level.** Every level ends the same way: stop inside one rectangle. There is no concept of a sequence, a second car, a checkpoint or a condition other than the rectangle.
2. **"Reverse" and "parallel" are only parameters.**
   - Reverse-in levels (8, 13, 18, 23, 27) differ only by `allowEitherDirection = false` and a bay yaw rotated 180°.
   - Parallel levels (5, 10, 15, 20, 25, 30) differ only by the bay's shape and placement along a kerb. The kerb is not checked: you can stop 1 m from it, or ride over it, and still pass.
3. **Difficulty is one dial.** `d` scales bay width, fill rate, obstacle count, angle tolerance, hold time and the clock together. Level 12 and level 17 differ only by random seeds and a few centimetres.
4. **Feedback is the same everywhere.** The HUD shows the same five status strings (DRIVE TO THE BAY, GET FULLY INSIDE, STRAIGHTEN UP, STOP, HOLD IT) in every level. There is no mission briefing, objective list, gauge or mission-specific warning.
5. **Failure reasons are fixed.** Only `TimeUp` and `TooManyCollisions` exist.
6. **Environment is tied to the level number.** Levels 1–10 use City Lot, 11–20 Sky Garage, 21–30 The Yard: primitive-module shells. Real imported models appear only as parked cars (swapped in at runtime by `EnvironmentBuilder.ResolveParked`) and in the separate Garage B1 showcase level.

## What can be kept

- `VehicleController`, `VehicleInput`, `CollisionHandler`: unchanged.
- `ParkingValidator` + `ParkingGeometry`: a good, tested "is the car in this rectangle" engine. Missions should reuse it rather than re-implement it.
- `LevelManager` state machine, timer, collision limit, star rating, rewards, save and progression: keep. Only the completion source changes.
- Pause / restart / retry / fail panels in `GameplayUiController`: keep.

## Gaps against the 14 requested mission types

| Type | Exists today? | Needs |
|---|---|---|
| Standard parking | Yes | A proper objective + HUD text |
| Reverse parking | Partly (heading only) | Depth check (backed fully in), rear sensor feedback, wrong-way feedback |
| Parallel parking | Partly (shape only) | Kerb distance check, kerb-strike rule, kerb gauge |
| Precision | No | Centring and angle score, tighter gates |
| Parking exam | No | Objective sequence with per-task scoring |
| Timed | Clock only | Checkpoint time bonuses / split targets |
| Narrow routes | No | Gate/checkpoint objectives, touch-to-fail barriers |
| Multi-storey ramp | Garage B1 ramp exists | Checkpoints across levels of a structure |
| Moving obstacles | No | Moving real cars on paths, hit = fail |
| Valet (multi-car) | No | Switch the player into several cars in sequence |
| Multi-stage | No | Objective sequence |
| Slippery surface | No | Per-zone wheel friction override |
| Night | No | Lighting preset, headlights |
| Restricted camera | No | Camera mode lock (e.g. top-down off, fog, no chase cam) |

The "previously supplied level descriptions" are not in this project's history: the only earlier level brief (Phase 3) asked for three environment themes and 30 levels, with no per-level ideas. Chapter content below is designed from the chapter names in the new brief.
