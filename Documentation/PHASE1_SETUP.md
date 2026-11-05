# Ultimate Car Parking 3D — Phase 1 setup

Unity **6000.5.9f1** (Unity 6), URP 17.3.1, Input System 1.20.0, Android landscape, IL2CPP / ARM64.
No paid or third-party assets: every mesh is a Unity primitive and every material is generated.

## Opening the project

1. Unity Hub → **Add** → `~/StudioProjects/UltimateCarParking3D` → open with 6000.5.9f1 (Android Build Support installed).
2. If the editor asks to restart to enable the new Input System backend, accept.
3. Run **Tools ▸ Ultimate Car Parking ▸ Bootstrap Project** once. It (re)generates everything below and ends with
   `[Bootstrap] VERIFY OK` in the Console. It is safe to re-run; it overwrites its own output.
4. Open `Assets/_Project/Scenes/Gameplay.unity` and press Play.
   Or open `Bootstrap.unity` and press Play: it loads Gameplay (the menu scenes are empty until a later phase).

Headless equivalents (logs in `~/StudioProjects/UltimateCarParking3D_logs/`):

```bash
Tools/bootstrap.sh          # generate settings, materials, prefab, scenes
Tools/run_tests.sh          # PlayMode acceptance tests
Tools/build_android.sh      # Builds/UltimateCarParking3D.apk (add "install" to adb-install and launch)
```

## Editor controls

| Key | Action |
|---|---|
| W / ↑ | Throttle forward |
| S / ↓ | Brake while rolling forward, then reverse |
| A D / ← → | Steer |
| X | Foot brake |
| Space | Handbrake (rear wheels) |
| Mouse wheel | Camera distance |

Mobile UI (later phase) calls the public methods on `VehicleInput`:
`SetThrottle(-1..1)`, `SetSteering(-1..1)`, `SetBrake(0..1)`, `SetHandbrake(bool)`, `ResetMobileInput()`.
Keyboard and mobile values are summed and clamped, so both work in the Editor at once.

## Folder layout

```
Assets/_Project/
  Scripts/Runtime/Core      BootstrapLoader, SceneNames
  Scripts/Runtime/Vehicle   VehicleController, VehicleInput, WheelVisuals
  Scripts/Runtime/Camera    VehicleCamera
  Scripts/Editor            ProjectBootstrapper (generator), BuildScript
  Tests/PlayMode            driving + keyboard acceptance tests
  Prefabs/Vehicles          PlayerCar.prefab
  Materials                 URP/Lit materials (generated)
  Scenes                    Bootstrap, MainMenu, Garage, LevelSelect, Gameplay
  Settings                  URP_Mobile asset + renderer
  Art/Models, Audio, UI     empty, for later phases
```

## PlayerCar prefab

```
PlayerCar  (tag Player)  Rigidbody, VehicleInput, VehicleController, WheelVisuals
├─ Model            chassis / cabin / roof / bumpers / lights   (meshes only, no colliders)
├─ Colliders        BodyCollider, CabinCollider                  (BoxColliders)
├─ WheelColliders   FrontLeftCollider … RearRightCollider        (y = 0.5, x = ±0.78, z = ±1.35)
└─ WheelMeshes      FrontLeftMesh … RearRightMesh                (pivots; Tire, Rim, 2 spokes as children)
```

To use a real car model later: put its body under `Model`, resize the boxes under `Colliders`, and put each
wheel mesh under its `…Mesh` pivot (pivot axes: X = axle, Y = up, Z = forward). Move the wheel colliders to
the new wheel centres (+ ~0.16 m up for suspension travel).

### Rigidbody
Mass **1300**, Linear Damping **0.02**, Angular Damping **0.3**, Interpolate **Interpolate**,
Collision Detection **Continuous**. (Centre of mass is set by VehicleController.)

### WheelCollider (all four)
| Field | Value |
|---|---|
| Mass | 20 |
| Radius | 0.34 |
| Wheel Damping Rate | 0.25 |
| Suspension Distance | 0.22 |
| Force App Point Distance | 0.15 |
| Suspension Spring | Spring 35000, Damper 4000, Target Position 0.5 |
| Forward Friction | Extremum 0.4 / 1, Asymptote 0.8 / 0.5, Stiffness 1.5 |
| Sideways Friction | Extremum 0.25 / 1, Asymptote 0.6 / 0.75, Stiffness 1.8 |

### VehicleController
| Group | Field | Default | Notes |
|---|---|---|---|
| References | Input, Front Left … Rear Right | wired by bootstrapper | Missing wheel → error + component disables itself |
| Body | Center Of Mass | (0, 0.3, 0.05) | Low COM is the main anti-flip measure |
| Engine | Drive Type | Front Wheel Drive | FWD / RWD / AWD |
| | Motor Torque | 1600 N·m | total, split across driven wheels |
| | Reverse Torque | 1100 N·m | |
| | Max Forward Speed | 60 km/h | |
| | Max Reverse Speed | 20 km/h | |
| | Speed Limit Band | 6 km/h | torque fades over the last 6 km/h |
| Brakes | Brake Torque | 2500 N·m / wheel | |
| | Handbrake Torque | 4000 N·m / rear wheel | |
| | Coast Brake Torque | 120 | rolling resistance with no pedal |
| | Standstill Brake Torque | 800 | holds the car once stopped |
| | Direction Change Speed | 2 km/h | below this, opposite throttle changes direction |
| Steering | Max Steer Angle | 35° | at rest |
| | High Speed Steer Angle | 12° | at max forward speed |
| | Steer Speed / Return Speed | 110 / 160 °/s | smoothing |
| | Use Ackermann | on | inner wheel turns more |
| Stability | Anti Roll Stiffness | 7000 | per axle |
| | Downforce | 25 N per m/s | |
| Sub-stepping | Threshold / Below / Above | 5 / 12 / 15 | `WheelCollider.ConfigureVehicleSubsteps` |

Runtime API: `ForwardSpeedKmh`, `SpeedKmh`, `IsReversing`, `SteerAngle`, `ControlsEnabled`,
`ResetVehicle(position, rotation)`.

### VehicleInput
Keyboard Enabled **on**, Keyboard Throttle Ramp Time **0.3 s**.

### WheelVisuals
Wheels: 4 pairs (WheelCollider → mesh pivot), Body: the car's Rigidbody.
The pose is computed in the car's local space and placed on the interpolated transform, so the wheels do
not shake against the body.

### VehicleCamera (Main Camera in Gameplay)
| Field | Default |
|---|---|
| Target | PlayerCar (auto-finds a VehicleController if empty) |
| Distance / Min / Max | 7 / 4 / 14 m |
| Height / Look Height | 3 / 1 m |
| Position Smooth Time | 0.18 s |
| Rotation Sharpness / Heading Sharpness | 10 / 4 |
| Mouse Wheel Zoom / Zoom Step | on / 0.8 m |
| Avoid Obstacles / Layers / Radius | on / Everything / 0.3 m |

Runtime API: `Distance`, `Zoom(delta)`, `SetTarget(transform, snap)`, `SnapToTarget()`.

## Gameplay test lot

70 × 60 m walled asphalt lot on a grass plane: two rows of twelve 2.8 × 5.5 m bays with white lines and
wheel-stop curbs, parked box cars (static colliders) with four bays left empty, one bay outlined in yellow
(**visual only** — parking detection is not part of Phase 1), six concrete pillars, two jersey barriers,
a low curb strip to drive over, and ten knock-over cones (3 kg Rigidbodies). The car spawns at (−20, 0, 0)
facing +X down the aisle.

## Project settings applied by the bootstrapper

- Company `Ergebins`, product `Ultimate Car Parking 3D`, package `com.ergebins.ultimatecarparking3d`, version 0.1.0 (1).
- Orientation: auto-rotate between Landscape Left / Right only.
- Android: IL2CPP, ARM64, min API 26, target = highest installed, Vulkan + OpenGLES3, ASTC, stripping Low.
- Linear colour space; Active Input Handling = Input System package (new).
- URP_Mobile: forward, HDR off, MSAA 2×, render scale 1, one 1024 shadow cascade at 45 m, hard shadows,
  additional lights off, depth/opaque textures off. Assigned to Graphics and every Quality level.
- Build scenes: Bootstrap (0), MainMenu, Garage, LevelSelect, Gameplay.

## Verification (2026-11-05)

- Bootstrap: `[Bootstrap] VERIFY OK`, no compile errors.
- PlayMode: **21/21 pass** (`Tools/run_tests.sh`). Measured on the PlayerCar prefab:
  - 0 → 47.9 km/h in 4 s; top speed settles at 59.3 km/h (limit 60), never exceeds it.
  - Foot brake from 57.4 km/h: stops in 1.8 s / 14.1 m. Holding S at 36 km/h brakes, then reverses to −19.6 km/h (limit 20).
  - Steering ramps (4.4° after two physics steps → 35° lock at rest, 14.4° at 54 km/h); 175° turn in 4 s each way.
  - Wheel meshes spin and steer (front axle 27°, rear 0°), sitting one radius above the ground.
  - Max body tilt 0.6° through a full-lock slalom at top speed, full-lock circle, handbrake panic stop and reverse at full lock.
  - Holds still with no input (0 cm creep in 3 s).
  - Camera at 60 fps: 7.2–9.9 m from the car, ≤ 5.7° off-centre, peak acceleration 23.5 m/s², never out-steps the car.
  - Keyboard mapping and keyboard + mobile combining checked through a virtual Input System keyboard.
  - Bootstrap scene loads Gameplay; the car drives 15 m down the aisle in 3 s and stays upright.
- Android: `Tools/build_android.sh` → `Builds/UltimateCarParking3D.apk`, 24.5 MB, 0 errors (debug-signed).
  **Not yet run on a device** (no phone was attached during Phase 1).
