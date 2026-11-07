# Missions — Phase C: standard, reverse and parallel parking

## New scripts

| File | Role |
|---|---|
| `Runtime/Missions/MissionType.cs` | The 14 mission types and their display titles (a lookup table, not behaviour). |
| `Runtime/Missions/MissionObjective.cs` | Abstract objective: `Begin` / `Tick` / `End`, `Hint`, `Progress`, `FailDetail`. Cloned per attempt. |
| `Runtime/Missions/ParkObjective.cs` | Standard parking. Creates its bay, binds the shared `ParkingValidator`, completes when parked. Direction: Either / NoseIn / ReverseIn. |
| `Runtime/Missions/ReverseParkObjective.cs` | Must face out of the bay **and** have its rear bumper within `maxRearGap` of the back line. Rear-sensor gauge, sensor beeps, "WRONG WAY" and "BACK IN FURTHER" hints. |
| `Runtime/Missions/ParallelParkObjective.cs` | Must face the traffic direction **and** have its kerb-side edge within `maxKerbGap` of the kerb. Wheels touching a `KerbMarker` count strikes; more than `kerbStrikeLimit` fails the level. Kerb-gap gauge and strike toasts. |
| `Runtime/Missions/MissionRunner.cs` | Runs the objective sequence; events for the HUD, guide and LevelManager. |
| `Runtime/Missions/MissionContext.cs`, `MissionGauge.cs` | What objectives may touch: car, validator, bay factory, gauge, toasts, sensor audio. |
| `Runtime/Missions/ParkingSensor.cs`, `ParkingSensorAudio.cs` | Bumper box-cast distance sensor; synthesised beeps (no audio asset). |
| `Runtime/Parking/IParkingCondition.cs` | Extra condition hook in `ParkingValidator` (new status `Unpositioned`). |
| `Runtime/Environment/KerbMarker.cs` | Marks kerb and pavement colliders. |
| `Editor/DrivingSchoolBuilder.cs` | Builds `Prefabs/Real/Env_DrivingSchool.prefab` from real props. |
| `Editor/MissionLevelBuilder.cs` | Writes levels 1–3 as mission levels. |
| `Tests/PlayMode/MissionTests.cs` | 10 tests covering all three mission types. |

## Changed scripts (existing behaviour kept)

- **LevelData:** new `missionType`, `briefing` and `[SerializeReference] objectives` fields. `CreateObjectives()` builds one `ParkObjective` from the old target fields when `objectives` is empty, so levels 4–30, Garage B1 and the test levels run unchanged.
- **LevelManager:** completes on `MissionRunner.Completed` instead of `validator.Parked`. Fails with the new `FailReason.ObjectiveFailed` plus a `FailDetail` string.
- **LevelLoader:** no longer creates the bay itself (objectives do). `Zone` returns the active objective's bay. Props now also work with environment-prefab levels.
- **ParkingValidator:** accepts an optional `IParkingCondition` and exposes the world footprint.
- **GameplayHud:**
  - Start banner (mission title + briefing).
  - Objective checklist that ticks off with a lime pop, a flash and the star chime.
  - Live hint line from the current objective.
  - Mission gauge and toast notices.
- **ParkingGuide:** follows the active bay as objectives change.
- **GameplayUiController:** the fail panel shows the objective's reason (e.g. "HIT THE KERB 3 TIMES").
- **LevelDesigner:** clears the mission fields on generated levels.
- **ProjectBootstrapper:**
  - Adds `MissionRunner` and `ParkingSensorAudio` to GameSystems.
  - Builds the Driving School and levels 1–3.
  - Verify accepts mission levels and treats `MainMenuController.heroCar` as optional when `heroPrefab` is set.
- **link.xml:** preserves `UltimateCarParking.Missions` for IL2CPP.

## Scene and prefab changes (all generated; run `Tools/bootstrap.sh`)

- **Gameplay.unity:**
  - GameSystems gets `MissionRunner` (wired to the validator and the LineTarget material) and `ParkingSensorAudio` with its AudioSource.
  - The HUD gets `Objectives` (with an inactive `RowTemplate`), `MissionGauge`, `Toast` and `Banner`.
- **New `Prefabs/Real/Env_DrivingSchool.prefab`:**
  - Ground and kerb: a concrete yard, a raised kerb and pavement (Poly Haven textures).
  - Markings: painted lines and floor text.
  - Real props: jersey-barrier perimeter and back wall, bollards, water barriers, delineators, cone clusters, an A-frame barricade, barrels, bins, and Stop / Give Way / Warning / No Waiting signs.
- **New materials** in `ThirdParty/Materials` (tinted copies of existing texture materials, no new textures): `M_TrainingYard`, `M_YardApron`, `M_KerbStone` and `M_Pavement`.
- **Level_01–03.asset:** rewritten as mission levels. The ids L01–L03 are kept, so saved stars and unlocks carry over.

## Chapter 1 so far

| Level | Mission | What makes it different |
|---|---|---|
| 1 First Bay | Standard | Either direction, generous tolerances, guide chevron. Learn stop-inside-and-hold. |
| 2 Back It In | Reverse | Must back in between two real cars to within 0.7 m of a jersey-barrier back wall. The rear sensor gauge and beeps are the main feedback; nose-in is rejected with "WRONG WAY". |
| 3 Kerbside | Parallel | Gap between two real cars at a raised kerb. Must finish within 0.45 m of the kerb. Kerb strikes are counted and toasted; a third strike fails. |

## Setting up a new mission level by hand

Levels are written from code in `MissionLevelBuilder` (one short method per level); that is the supported path.

In the Inspector you can edit the fields of existing objectives. Unity 6 has no built-in type picker for `[SerializeReference]` arrays, so adding a new objective of a chosen class needs a small custom editor. That editor is not built yet; it is listed as unfinished work.
