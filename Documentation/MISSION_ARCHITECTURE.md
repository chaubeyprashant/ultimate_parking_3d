# Mission architecture — Phase B

## Shape

```
LevelData (ScriptableObject)                      ← one per level, as before
 ├─ identity, environment prefab / theme, props, spawn, forced car, camera
 ├─ timer, collision limit, stars, rewards        (unchanged)
 ├─ missionType  : MissionType                     ← label, HUD kicker, level-select badge
 ├─ briefing     : string                          ← shown in the start banner
 └─ objectives   : [SerializeReference] MissionObjective[]   ← the ordered objective sequence

MissionObjective (abstract, [Serializable])        ← one class per behaviour, no switch statements
 ├─ ParkObjective              standard: rectangle + heading + stop + hold
 │   ├─ ReverseParkObjective   + must face out, + rear bumper within X of the back line, rear sensor gauge + beeps
 │   └─ ParallelParkObjective  + kerb-side gap ≤ X, + kerb strikes counted / fail over limit, kerb gauge
 └─ (Phase D/E)  CheckpointObjective, ValetObjective, ...

MissionRunner (MonoBehaviour, GameSystems)         ← runs the sequence
 ├─ Prepare(level, car) → clones the level's objectives (assets stay untouched)
 ├─ Run() / Stop()       ← driven by LevelManager's state machine
 ├─ events: ObjectiveStarted, ObjectiveCompleted, Notice (toast), Completed, Failed(detail), ActiveZoneChanged
 ├─ Hint  (live instruction line)   Gauge (caption, value, fill, severity)
 └─ MissionContext handed to each objective: car, validator, zone factory, gauge, notices, sensor audio

ParkingValidator (unchanged rules) + IParkingCondition
 └─ an objective can add an extra condition (rear depth, kerb gap). Unmet → status Unpositioned + the condition's hint.
```

## Flow

1. `LevelLoader` builds the world (theme shell or environment prefab, plus the level's props) and spawns the car.
2. `LevelManager.BeginLevel` calls `MissionRunner.Prepare`, then `Run` when the start delay ends.
3. Each objective's `Begin` creates what it needs (zones, sensors); `Tick` returns Running / Completed / Failed; `End` cleans up.
4. All objectives completed → `LevelManager.CompleteLevel` (stars, coins, save — unchanged).
   An objective fails → `LevelManager.FailLevel(FailReason.ObjectiveFailed)` with the objective's detail ("MOUNTED THE KERB 3×").
5. Levels with no objectives get one `ParkObjective` built from their old target fields, so every existing level and test keeps working.

## Mission config checklist (brief §4)

| Required | Where |
|---|---|
| Mission ID | `LevelData.levelId` |
| Mission type | `LevelData.missionType` |
| Environment prefab | `LevelData.environmentPrefab` (or `theme`) |
| Spawn point | `LevelData.spawnPosition / spawnYaw` |
| Vehicle selection | `LevelData.forcedCar` (empty = the player's garage car) |
| Objective sequence | `LevelData.objectives` |
| Parking target locations | inside each parking objective |
| Timer | `LevelData.timeLimitSeconds` |
| Collision limit | `LevelData.collisionLimit` |
| Special rules | fields on the objective (kerb limit, rear depth…) + `ParkingRules` |
| Completion conditions | the objective classes |
| Rewards | `LevelData` stars + coins |

## Why this shape

- **No giant switch:** behaviour lives in the objective subclass; the runner, HUD and level manager never branch on mission type.
- **Data stays editable:** objectives are `[SerializeReference]`, so they show in the Inspector inside the LevelData and are written by the level builders.
- **Runtime state never touches assets:** the runner clones objectives per attempt.
- **Reuses tested code:** rectangle, heading, stop and hold checks remain in `ParkingValidator`.
- **IL2CPP:** objective classes are referenced only from serialized data, so `Assets/link.xml` preserves the `UltimateCarParking.Missions` namespace.
