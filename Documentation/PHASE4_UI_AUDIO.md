# Ultimate Car Parking 3D — Phase 4: premium UI, audio and gameplay polish

Builds on Phases 1–3 without replacing working systems. Changes to existing code are additive: save v2 (settings),
`VehicleCamera.Shake`, a click sound + idle early-out in `UIButtonFX`, music/settings hooks in the three menu
controllers, `VehicleAudio` on every car prefab, the Phase 2 debug overlay hidden by default (F1 toggles it).

## New scripts

| Script | Role |
|---|---|
| `Settings/GameSettings`, `Settings/SettingsService` | music, sound, steering sensitivity; stored in the save (v2), live `Changed` event, `Commit()` writes |
| `Audio/AudioLibrary` (ScriptableObject in `Resources/`) | every clip in one place |
| `Audio/AudioManager` | persistent; music source + 6 pooled one-shot sources; volumes from settings; `Play(Sfx)`, `Click()` |
| `Audio/VehicleAudio` (on each car) | engine loop pitch/volume from speed + throttle, brake squeal on hard braking from speed, impact sound per counted collision |
| `UI/Gameplay/GameplayHud` | level, timer (red < 10 s), hits vs limit, gear D/R/P, guide text + hold bar, collision flash + camera shake |
| `UI/Gameplay/MobileControls`, `UI/Gameplay/HoldButton` | steer left/right (ramped × sensitivity, auto-centre), gas, brake/reverse, handbrake; multi-touch |
| `UI/Gameplay/ParkingGuide` | lime chevron bobbing above the target bay, recoloured by status, hidden when parked |
| `UI/Gameplay/ScreenFlash` | edge vignette flash (collision, failure) |
| `UI/Gameplay/GameplayUiController` | pause (time + audio frozen), completed panel (stars one by one, coins roll-up), failed panel (reason), Back/Escape toggles pause |
| `UI/SettingsPanel` | three sliders + DONE (writes the save on close); used by Main Menu and Pause |
| `UI/CameraDrift` | slow orbit/dolly of the showroom cameras |
| Editor `AudioSetup` | clip import settings + `Resources/AudioLibrary.asset` |
| Editor `UiSceneBuilder.Gameplay` | builds the gameplay canvas, controls, panels and settings modal |
| `Tools/audio/gen_audio.py` | synthesises all 10 clips (nothing sampled or downloaded) |

## Audio

All clips are generated from sines, noise and envelopes by `Tools/audio/gen_audio.py` (deterministic):
engine loop (1 s, seamless), brake squeal, collision (thud + crunch + ring), collision warning, UI click, star chime
(played 3× rising), coin, success chord, fail sting, 16 s ambient music loop (Am–F–C–G pads, soft bass).
Import: mono, Vorbis, effects decompress-on-load, music streams. Music plays full in menus and ducked (45 %) in
gameplay; pausing pauses gameplay audio (UI sounds still play). Regenerate:

```bash
python3 Tools/audio/gen_audio.py Assets/_Project/Audio
```

## UI hierarchy — Gameplay scene

```
GameplayCanvas                Canvas (Overlay, order 10) · CanvasScaler 1920x1080, match height 1 · GraphicRaycaster
├─ Flash                      stretch full · Image(Vignette) · ScreenFlash · no raycast
├─ SafeArea                   stretch full · SafeAreaFitter
│  ├─ Hud                     stretch full · GameplayHud
│  │  ├─ PauseButton          anchor top-left  (84, -78)   92x92   circle + Pause icon
│  │  ├─ LevelBlock           anchor top-left  (150, -38)  420x84  "LEVEL 07" + theme
│  │  ├─ TimerPill            anchor top-centre (0, -36)   230x84
│  │  ├─ Guide                anchor top-centre (0, -136)  600x62  dot + text + hold bar (CanvasGroup)
│  │  ├─ GearPill             anchor top-right (-270, -36) 84x84
│  │  ├─ HitsPill             anchor top-right (-40, -36)  210x84
│  │  └─ Controls             stretch full · MobileControls
│  │     ├─ SteerLeft         anchor bottom-left  (170, 170)  200x200 circle · HoldButton
│  │     ├─ SteerRight        anchor bottom-left  (410, 170)  200x200
│  │     ├─ Handbrake         anchor bottom-right (-390, 330) 200x74 pill
│  │     ├─ BrakeReverse      anchor bottom-right (-390, 150) 200x200 rounded
│  │     └─ Gas               anchor bottom-right (-160, 200) 190x300 rounded
│  └─ Panels                  stretch full
│     ├─ PausePanel           CanvasGroup · Backdrop (dim) · Body 700x640 centred: RESUME / RESTART / SETTINGS / LEVELS
│     ├─ CompletePanel        Body 820x660: kicker, PARKED, 3 stars, TIME | HITS | COINS, NEW BEST, LEVELS / RETRY / NEXT
│     ├─ FailPanel            Body 700x520: kicker, FAILED, reason, RETRY / LEVELS
│     └─ SettingsPanel        Body 820x600: MUSIC / SOUND / STEERING sliders + values, DONE · SettingsPanel
└─ (EventSystem + InputSystemUIInputModule)
GameSystems                   ParkingValidator, LevelManager, LevelLoader, ProgressionRecorder, LevelDebugOverlay (hidden)
ParkingGuide                  ParkingGuide (chevron parented to the level's ParkingZone)
```

Anchoring rules: HUD items anchor to the nearest corner/edge so wider phones only add space in the middle; modals
anchor centre; full-screen layers stretch. Everything interactive sits inside `SafeArea`. Touch targets are ≥ 190 px
in the 1080-high reference (≈ 12 mm on a typical phone). Main Menu, Level Select and Garage follow the same rules
(see `PHASE3_SETUP.md`); Main Menu adds a Settings gear (top-right, left of the coin pill) and the settings modal.

## Prefab requirements

- **Car** (each `Prefabs/Vehicles/*`): Rigidbody, VehicleInput, VehicleController, WheelVisuals, CollisionHandler,
  CarPaint, **VehicleAudio** (after VehicleController). BoxColliders define the parking footprint.
- **LevelCard** (`Prefabs/UI/LevelCard`): Button, UIButtonFX, LevelCard with number, status, 3 stars, lock, border.
- **AudioLibrary** must live at `Resources/AudioLibrary.asset` (the persistent AudioManager loads it by name).
- Every scene with UI needs an EventSystem with **InputSystemUIInputModule** (the project uses the Input System only).

## Art direction

Charcoal surfaces (#0B0D10 / #14181D / #1C2128), one accent (electric lime #B6FF2E), crisp white type (Rajdhani for
headings, Barlow for body), amber only for "fix this" guidance, red only for danger (time < 10 s, limit reached,
failure). Motion is short and eased: 0.25–0.45 s fades/scales, press-scale on buttons, star pops, coin roll-up,
gentle shake on errors, slow showroom camera drift. The Level Select glow was toned down to 3.5 % opacity.

## Performance notes

No per-frame UI work when idle: `UIButtonFX` returns early when at rest, `MobileControls` disables itself whenever
the wheel is centred, the HUD writes the timer text only when the displayed tenth changes, panels/flash animate in
coroutines that end. Per-frame scripts in gameplay: VehicleAudio (engine pitch), ParkingGuide (chevron bob),
GameplayHud (timer/hold compare). No particles; no extra real-time lights (URP additional lights stay off);
the chevron and showroom ring are emissive materials, not lights.

## Testing checklist

Automated (`Tools/run_tests.sh`): see the report for the latest totals. Phase 4 tests:

- [x] Save v1 → v2 adds default settings and keeps progress; settings apply live, persist on commit, clamp bad values
- [x] HUD shows LEVEL nn, a counting-down timer, 0/3 hits, DRIVE TO THE BAY
- [x] Guide text follows the status (partially inside → GET FULLY INSIDE, angled → STRAIGHTEN UP, valid → HOLD IT);
      chevron sits above the bay and hides when parked
- [x] Gas pedal drives forward; Brake/Reverse brakes then reverses; gear shows R; releasing zeroes throttle
- [x] Steering buttons ramp (not instant), reach full lock, return to centre, stop updating; sensitivity 2 > 0.5
- [x] Pause freezes timer and time, hides HUD; Resume continues
- [x] Completed panel reveals 3 stars, rolls coins up to the reward, shows NEXT, plays success + star + coin sounds
- [x] Failed panel shows TIME'S UP with the fail sound; RETRY restarts the level
- [x] LEVELS from pause leaves with time and audio restored
- [x] Collision: counter 1/3, impact sound, screen flash
- [x] Engine pitch rises with speed; music plays (ducked in gameplay); sound volume 0 silences the engine
- [x] Settings from pause: sliders change values, DONE saves them
- [x] Main Menu settings gear opens settings with a click sound; showroom camera drifts

Manual on device:

- [ ] Main Menu: car fully framed on the right, ring subtle, camera drifting; gear → settings → sliders → DONE
- [ ] Garage: browse 5 cars, buy one with coins (coin sound), SELECTED state, drag to rotate
- [ ] Level Select: tabs, locked/unlocked/completed cards; tap Level 1
- [ ] Drive with the touch controls: hold GAS, steer with both thumbs, BRAKE then reverse, HANDBRAKE
- [ ] Guide chip and chevron lead to the bay; park → stars, coins, NEXT
- [ ] Hit a wall → flash, shake, sound, counter; exceed the limit → FAILED panel
- [ ] Pause with the button and with Android Back; Settings from pause; Levels returns to Level Select
- [ ] Background the app mid-level → returns paused
