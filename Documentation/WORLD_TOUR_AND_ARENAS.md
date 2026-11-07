# World Tour and country arenas

## 1. World Tour screen (country selection UI)

**Main menu:**
- **Play** continues with the next level to beat.
- **World Tour** opens the map.
- **Garage** opens the garage.
- **Missions** opens the existing level list.
- **Settings** is a new grid button that opens the same panel as the gear.
- The gear and the Garage B1 link are unchanged.

**World Tour scene:**
- **Map:** an interactive vector map built from Natural Earth (public domain). Drag pans, pinch zooms (mouse wheel in the Editor), and zoom is clamped.
- **Region chips:** World, Asia, Europe, Americas, Africa & Middle East, Oceania. Each glides the map to that region.
- **Pins:** 30 country pins, each with:
  - A flag (flag-icons, MIT licence).
  - A lock overlay, a completed tick and a selection ring.
  - A name tag.
- **Country shapes** are tinted by state: locked, unlocked, completed or selected.
- **Country card:**
  - Flag, name, region and stop number.
  - Arena city and the environment it really uses.
  - A 16:9 preview rendered from the level's real environment.
  - Mission type, description, missions done, stars, time limit and status.
  - The lock reason, and Play / Play again / Locked.
- **Layout:** 1920×1080 reference matched on height. The card keeps its size; the map uses the remaining width. On narrow screens (4:3) pins shrink so neighbours don't overlap.
- **Levels:** levels 1–30 map to countries 1–30 in the requested order, one mission each. Country previews reuse the existing environments: Driving School, City Lot, Sky Garage and Yard.
- **Exits:** a level started from the tour returns to the tour, which keeps the same country selected.

## 2. Country arena architecture

**Data — `CountryData` (ScriptableObject).**

| Requirement | Field |
|---|---|
| Country ID | `countryId` (ISO 3166-1 alpha-2) |
| Country name | `displayName` |
| Flag | `flag` (Sprite) |
| Preview image | `preview` (Sprite) |
| Environment prefab or scene | `arena` → `CountryArenaReference { source: None / ResourcesPrefab / Scene, key }` |
| Mission list | `levels` (`LevelData[]`) |
| Unlock requirements | `unlock` → `CountryUnlockRequirement` |
| Traffic configuration | `traffic` → `TrafficSettings` |
| Road direction | `roadDirection` (`RightHand` / `LeftHand`) |
| Weather settings | `weather` → `WeatherSettings` (preset, sun, ambient, fog, road wetness, precipitation) |

- `CountryUnlockRequirement` fields: `unlockedByDefault`, `requirePreviousCountry`, `requiredCountryIds[]`, `minTourStars` and `minCompletedCountries`. All set conditions must hold.
- `TrafficSettings` fields: parked-car density, moving traffic and its density, speed, and pedestrians.
- `WeatherSettings.ForPreset()` gives values for Clear, Overcast, Rain, Fog, Snow, Dusk and Night.

**Arenas are referenced by name, never by object.**
- Loading the country list therefore never loads an arena; `CountryArchitectureTests` checks the asset dependencies of all 32 countries for this.
- The key is a string, so moving to Addressables later only changes the loader.

**Managers (`Runtime/WorldTour/`):**
- **`CountrySelectionManager`** (static):
  - Selects a country and saves it in `SaveData.selectedCountryId`.
  - Refuses locked countries.
  - Raises a `SelectionChanged` event.
  - `SelectedOrCurrent()` falls back to the current tour stop.
- **`CountryArenaLoader`** (MonoBehaviour):
  - Loads the selected arena asynchronously, either `Resources.LoadAsync` + `Instantiate` or an additive `SceneManager.LoadSceneAsync`.
  - Before loading, it unloads the previous arena: destroys the prefab instance or unloads the scene, then calls `Resources.UnloadUnusedAssets`.
  - A newer request supersedes one in flight, and the stale result is discarded.
  - The same country loaded twice is a no-op.
  - Applies the country's weather when the arena loads.
  - Exposes `State`, `Current`, `ArenaRoot`, `LastError` and the events `Loaded`, `Failed` and `Unloaded`.
  - Error cases end in `Failed` with a message, never an exception: no country, no arena configured, missing Resources prefab, a scene not in Build Settings, a scene that fails to load, or an instantiation error.
- **`CountryProgressManager`** (static): missions done, stars, completion, tour totals and the next mission. All of it is read from the existing per-level save records, so there's no second copy of progress.
- **`CountryUnlockManager`** (static):
  - Evaluates requirements and returns the first unmet one as UI text, for example "FINISH INDIA TO UNLOCK" or "EARN 1 MORE STARS TO UNLOCK".
  - `RecordUnlocks()` makes unlocks sticky in `SaveData.unlockedCountryIds`.
- **`CountryArenaRoot`** (optional component on an arena): an arena id, a spawn point and named parking slots.

**Save:** version 3 adds `selectedCountryId` and `unlockedCountryIds`. Older saves migrate automatically and keep all progress; the v2→v3 test covers this.

**Temporary test countries** (`Assets/_Project/WorldTour/Test/`, separate from the 30-country tour):

| | Test Country A | Test Country B |
|---|---|---|
| Arena | Resources prefab `Arenas/Arena_TestA` (wraps the Driving School) | Additive scene `Arena_TestB` (wraps Garage B1), in Build Settings |
| Unlock | Open by default | Complete A **and** have 2+ tour stars |
| Road | Left-hand | Right-hand |
| Weather | Overcast | Rain |
| Traffic | Parked 0.5, no moving traffic | Parked 0.8, moving traffic 0.3 at 15 km/h |

## 3. Not done yet

- **The 30 tour countries have no dedicated arenas** (`arena.source = None`). Their missions use the existing level environments, as before.
- **The gameplay scene doesn't call `CountryArenaLoader` yet.** Levels still build their own environment. Wiring it in means levels must place spawn points and bays from `CountryArenaRoot` anchors instead of fixed coordinates. That belongs to the arena build-out phase.
- **Traffic and road direction are data only.** No moving-traffic system exists to consume them.
- **Weather applies lighting and fog only.** Road wetness and precipitation have no rain/snow effects or grip changes yet.
- **`LevelCatalog` holds direct references to all 30 levels, and so to their environment prefabs and themes.** This is how the game already worked. Real per-country arenas should be referenced only through `CountryArenaReference`.
- **Moving the arena loader to Addressables would need the Addressables package, which is a download.** That change is confined to `CountryArenaLoader`.

## 4. Adding a country arena

1. Build the arena as a prefab under `Resources/Arenas/`, or as a scene added to Build Settings. Put a `CountryArenaRoot` on its root with a spawn point and parking slots.
2. In the country asset, set `arena.source` and `arena.key` (the Resources path, or the scene name), and set weather, traffic, road direction and unlock requirements.
3. Run `Tools/bootstrap.sh`. Verify checks that every configured arena resolves.

## New York (USA) arena — `Editor/NewYorkArenaBuilder*.cs`

Right-hand traffic. Built only from the imported New York models (CC BY 4.0, see ATTRIBUTION.md) plus CC0 textures.

- **Avenue** along X: two lanes each way, double yellow centre line. North side: brownstone rows (`nyc_brownstone_set`, real scale, fronts to +Z). South side: brownstones, the supermarket and the loading yard. Behind them: downtown towers (`nyc_downtown_set`). Skyline: Empire State, Chrysler, One WTC, Woolworth, NY Times and MetLife from `nyc_buildings`, scaled to real heights and placed 300–600 m away.
- **Street furniture:** yellow cabs, sedans, a coupe, a police cruiser, pickups, a utility truck (imported fronts point along −X, hence yaw 90), hydrants, parking meters, newspaper boxes, a hot-dog cart, a subway entrance, a traffic light and US warning signs (picked by node name from `nyc_us_signs`).
- **US1 Supermarket Run:** `nyc_parking_lot_uf` scaled ×10 (its cars measure 0.46 units long), lowered 1.22 m so the lot is at ground level, with a collider added for the store. Painted bays are added on its forecourt. A `MovingBarrier` ticket gate (`Gate_Sliding`, two imported metal barrier panels) guards the entrance, behind a jersey-barrier line. Objectives: `BarrierGateObjective`, then a nose-in `ParkObjective` facing the store.
- **US2 Loading Dock:** a 7 m alley off the avenue leads to an enclosed yard. Forced car: the Hauler pickup. The pickup must reverse into a dock bay between parked trucks, with `ReverseParkObjective` (rear gap ≤ 0.5 m).
- **US3 Level 2 Only:** two stacked `GarageModule` halls (level 2 at +4.2 m), a 40 m ramp at 6°, and a level-2 apron with columns.
  - Invisible safety walls and visible jersey rails line every deck edge.
  - Objectives: enter, take the ramp (a checkpoint with a height band), then `FloorParkObjective` "LEVEL 2". The same bay on level 1 is left free as a decoy.
- **Tests:** `CountryArenaMissionTests` now checks reachability with a surface-following search (ramps and floors). Moving barriers count as passable, and the car footprint is re-measured per objective (forced cars).

## London (United Kingdom) arena — `Editor/LondonArenaBuilder.cs`

Left-hand traffic. Built from the imported London models (see ATTRIBUTION.md).

- **`ldn_mobile_pack`, scaled ×0.42** (its phone box is 6.7 units tall): terraced houses, hotel, bank, red phone boxes, bench, planter, billboard, and the "Permit holders only", "Disabled badge" and "Ramp ahead" signs.
- **Vehicles:** the black cab and the Routemaster come without wheels, so `Wheeled()` assembles them from the pack's wheel. The '67 saloon is used as is.
- **Other imported models:** brick warehouses from `ldn_industrial_buildings` (real scale), UK regulatory signs (their faces point along −X, hence yaw 90), Belisha beacons and a keep-left bollard from `ldn_zebra_crossing`, and Victorian street lamps.
- **High street:**
  - Carriageway z ±4.75 with a parking strip each side.
  - 13 cm kerbs (`KerbMarker`, so kerb strikes count) and raised pavements.
  - Double yellow lines except along the parking strips, plus a zebra crossing.
- **GB1 High Street Parallel:** `ParallelParkObjective` at the left kerb (`TrafficSide.KerbSide`), kerb gap ≤ 0.45 m, kerb strikes > 2 fail.
- **GB2 Mews Reverse:** a 3.6 m cobbled mews between brick warehouses.
  - `NarrowAlleyObjective` (reverse-through, 2 scrapes allowed), then `ReverseParkObjective` at the end.
  - Quality-scored (tightest gap).
- **GB3 Underground Car Park:** a 36 m ramp down 4.2 m in a concrete trench, then `GarageModule` turned 90° under the street, with walls, a ceiling and fluorescent fittings.
  - `FloorParkObjective` "LEVEL -1", reverse into the permit bay.
  - Low chase camera (2.2 m).

## Tokyo (Japan) arena — `Editor/TokyoArenaBuilder.cs`

Left-hand traffic. Built from the imported Japan models (see ATTRIBUTION.md):
- three residential homes (modelled in cm: ×0.01; front door to −X, hence yaw 90)
- a konbini
- snack and drink vending machines
- tall street lamps and an elevated expressway section from `jp_storefront`, crossing overhead with its piers clear of the carriageway
- the rotary tower car park (`jp_parking_lift`) as a landmark
- Japanese hatchbacks and a box truck
- a stone lantern

`jp_asian_shop` is not used because its sign is Korean; it is kept for Seoul.

- **JP1 Coin Parking:**
  - `PrecisionParkObjective`, reverse-in, in a 2.4 m bay; forced car: the Volt Hatch.
  - Quality-scored (accuracy).
- **JP2 Alley Reverse:**
  - A 3 m residential alley: `NarrowAlleyObjective` (reverse-through, 2 scrapes allowed), then `ReverseParkObjective` into the carport.
  - Forced car: the hatch.
- **JP3 Stacker Parking:**
  - `jp_hydraulic_lift` split into its frame (static) and deck (moving), at ×0.46.
  - The deck is set flush with the ground (pit installation), with post colliders on each side.
  - `MechanicalParkingSystem` with negative depth lifts the car 2.2 m (labels: "PLATFORM RISING", "LOCKING THE PLATFORM").
  - The other two stackers are static decoration with cars parked above and below.

## Dubai (UAE) arena — `Editor/DubaiArenaBuilder.cs`

Right-hand traffic. Built from the 9 imported Dubai models (re-downloaded and validated 2026-11-06; CC BY 4.0, see ATTRIBUTION.md):
- a glass tower hotel (×3 along the boulevard)
- a steel porte-cochère (modelled in mm: ×0.001)
- ornate pavilion towers
- a modern villa with a pool
- date, coconut and fan palms, and desert rocks
- the marina hotel diorama as a far landmark

Many-part models use the new `ArenaPropFactory.Spec.combine` option (one mesh per material). The villa went from 1,345 parts to 21 meshes; renderers per mission fell from about 1,950 to about 450.

- **Boulevard:** 2+2 lanes, a raised median with date palms, sandstone-coloured paving. Desert sand south of the city.
- **AE1 Hotel Valet:** `ValetParkObjective`, stand under the porte-cochère, then the guest lot. Forced car: the supercar. Quality-scored (guest rating).
- **AE2 Supercar Garage:**
  - A ramp north to an underground `GarageModule`, then `PrecisionParkObjective` in the VIP bay.
  - Collision limit 1 (2 hits fail); a low camera.
- **AE3 Desert Resort:** a desert road to a sliding resort gate (`MovingBarrier` "ae3_gate", between pavilions), then a `TimedParkObjective` (30 s) at the villa. Quality-scored (time left).
- **Performance pass in this round:** fluorescent fittings in the underground car parks thinned to 8 (about 13k triangles each); London lamps every 24 m. London went from about 650k to 380k triangles per mission.

## Batch 2: China, South Korea, Thailand, Singapore, Malaysia

Asset research and approval: [ASSET_CATALOG_BATCH2.md](ASSET_CATALOG_BATCH2.md). 39 Sketchfab models, all CC BY 4.0 (records in ATTRIBUTION.md).

- **China (Beijing)**, `Editor/ChinaArenaBuilder.cs`. A dusk look with lanterns, temple-hall hutong houses, a pagoda tower, shop rows and a night-city skyline. Right-hand traffic.
  - CN1 Beijing Rush: timed parking in a car park.
  - CN2 Hutong Kerb: parallel parking in a narrow hutong lane.
  - CN3 Night Market: a lantern-lit lane, then precision parking.
- **South Korea, Thailand, Singapore, Malaysia**, `Editor/CityKitArenas.cs` (shared layout) + `Editor/BatchTwoArenas.cs` (per-country models, lighting, missions).
  - The shared layout is an avenue, a lot, a wide lane, a narrow lane and an underground garage.
  - Each country brings its own imported models and lighting, and its own three missions.
  - Missions are built only from the 11 mechanics that exist (night, hill, rain, moving traffic and the exam sheet are not built).
  - Thailand, Singapore and Malaysia drive on the left.

| Country | Missions |
|---|---|
| South Korea | KR1 Apartment Reverse, KR2 Tower Garage (underground), KR3 Licence Exam (start line → parallel → precision bay) |
| Thailand | TH1 Soi Squeeze (narrow lane + park), TH2 Bazaar Rush (timed gate + timed park), TH3 Hotel Valet |
| Singapore | SG1 HDB Bay (2.4 m precision), SG2 Ticket Garage (gate + underground), SG3 Marina Exam |
| Malaysia | MY1 Riverside Parallel, MY2 Kampung Reverse (reverse down a lane), MY3 Mosque Forecourt (one knock allowed) |

**Honest limits.** These four arenas share one street layout, and their model sets were thin:
- Korea has no apartment blocks or road signs.
- Singapore's colonial shophouses and Chijmes are untextured white.
- Malaysia is mostly kampung houses plus a mosque and the Twin Towers backdrop.
- Missions that were planned with night, hill, rain, moving traffic or the exam sheet currently use other mechanics. See MISSION_PLAN_90.md.
