# Ultimate Car Parking 3D — Phase A: 3D asset audit (2026-11-05)

Every license below was read on the asset's own page (or the site's license page) on 2026-11-05 — not from search
snippets. "Unverified" means the page did not establish a usable license; those assets are **not recommended**.
Nothing has been downloaded or imported yet.

## 1. What the project has today

| Category | Present | Notes |
|---|---|---|
| 3D model files (FBX/OBJ/GLB/glTF/blend) | **0** | every car, prop and structure is built from Unity primitives by the bootstrapper |
| Textures | 18 UI sprites only | no surface textures; the 45 materials are flat URP/Lit colours |
| Player car | 5 primitive cars | separate wheel meshes already (needed by `WheelVisuals`) |
| Garage / outdoor lot / props | primitive modules (`Mod_*`) | cones, barriers, pillars, walls, parked cars, lights |

**Missing:** everything in the brief — a detailed car, realistic garage structure, outdoor lot with buildings/roads,
real props and PBR surfaces. The gameplay code (VehicleController, parking, levels, HUD) is model-agnostic and stays.

## 2. Requested candidates

| # | Asset | Source | License (verified) | Format / size | Polys / textures | Android | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | Modern Sports Sedan | [SummerEngine](https://www.summerengine.com/asset-store/modern-sports-sedan-7ab651cc) | **Unverified** — page says only "Free"; SummerEngine's [Terms](https://www.summerengine.com/terms) say creators keep ownership and define no license for asset-store downloads | GLB, size not shown | unknown | — | **Not recommended.** AI-generated (Meshy-6) by a user; AI meshes are usually one merged mesh (no separate wheels) |
| 2 | Realistic Sedan Car | [SummerEngine](https://www.summerengine.com/asset-store/realistic-sedan-car-5f8e64fb) | **Unverified** (same as above) | GLB | unknown | — | **Not recommended.** AI "image-to-3D" from an unnamed reference image — possible likeness of a real branded car |
| 3 | Parking garage (SPLEEN VISION) | [Sketchfab](https://sketchfab.com/3d-models/parking-garage-98f7bc75350f4fb7ad36830f58fc99be) | **CC BY 4.0** (credit required) | OBJ + textures; size needs login | **679k tris, two 8K textures**, photogrammetry scan | Poor as-is | Usable only after heavy decimation (→ ≤80k tris, 2K textures). One baked scan: lighting baked in, no separate pillars/ramps. A commenter alleges it is "stolen" (unproven) — **second choice** |
| 4 | Road barrier set | [Innerscene](https://www.innerscene.com/tools/library/3d-parts/road-barrier-set-4d29eb0a) | **CC0** (original Innerscene model; commercial use, no credit) | GLB **6.6 MB**, no login | 3 cones, A-frame barricade w/ lamp, 2 water barriers, delineator post; real metre scale | Good | **Recommended** |
| 5 | Traffic Road Assets (jamesdev) | [itch.io](https://milkandbanana.itch.io/traffic-road-assets) | **CC0** (asset license field); code Unlicense; "no generative AI" | FBX + GLB, zip **51 MB** | low-poly, 1 palette atlas, 3 materials | Excellent | **Not recommended for realism** (stylised low-poly). Fallback only |
| 6 | Poly Haven textures | [polyhaven.com/textures](https://polyhaven.com/textures) | **CC0** ([license page](https://polyhaven.com/license)) | JPG/PNG sets, 1K–16K | PBR: diffuse, normal (GL), roughness, AO | Good at 1K–2K | **Recommended** |

## 3. Better matches found during the audit

### Cars (unbranded — CC licenses cover the model's copyright, not car makers' trademarks/designs, so real-brand models are avoided)

| Asset | Source | License | Polys | Notes | Verdict |
|---|---|---|---|---|---|
| **Generic passenger car pack** (comrade1280) | [Sketchfab](https://sketchfab.com/3d-models/generic-passenger-car-pack-20f9af9b8a404d5cb022ac6fe87f21f5) | **CC BY 4.0** | 10 cars, **6–8k tris each** (sedan 7,122) | sedan, wagon, hatchback, compact, coupe, minivan, sport, pickup, SUV, offroad; 6 colour textures each; PBR metallic/gloss/opacity; author: "completely trademark claim proof", "can be used… in mobile games as player vehicle" | **Recommended — player car + parked cars** |
| Fairheaven LT '80 (Daniel Zhabotinsky) | [Sketchfab](https://sketchfab.com/3d-models/fairheaven-lt-80-low-poly-model-e2678da920cc4be68dbc193727919ffb) | **CC BY 4.0** + author's explicit permission for any digital product | 19.7k | fictional '80s sedan, higher detail | Optional alternative player car |
| Generic civil service pack (comrade1280) | [Sketchfab](https://sketchfab.com/3d-models/generic-civil-service-vehicles-pack-8ff2a13f30914932a70c7950cfa58465) | **CC BY 4.0** | 6–13k each | taxi, police, ambulance, vans, buses | Later (variety) |

Separate wheels, pivots and interiors can only be confirmed after download (Phase C checks them).

### Environments

| Asset | Source | License | Polys | Notes | Verdict |
|---|---|---|---|---|---|
| **Parking garage [FREE]** (idmental.id) | [Sketchfab](https://sketchfab.com/3d-models/parking-garage-free-download-5310b7d77b70427d936ec4253fff679c) | **CC BY 4.0** | **5.4k** | concrete floor, square columns with light strips, ceiling strip lights, bay lines | **Recommended garage base** (very light; add ramps/signs/props) |
| Parking Garage (dylanheyes) | [Sketchfab](https://sketchfab.com/3d-models/parking-garage-88c3f49f553f41f59382e0e38781193c) | **CC BY 4.0** | 33.8k | abandoned look, baked textures | Alternative |
| **Parking lot** (windofglass) | [Sketchfab](https://sketchfab.com/3d-models/parking-lot-80e54d8326ea4646949961e8ada35518) | **CC BY 4.0** | **23k** | game-ready rooftop/exterior lot: booth, barrier gate (separate moving arm), P sign, streetlights, fence | **Recommended outdoor base** |
| Poly Haven `modular_urban_apartments_facade` | [Poly Haven](https://polyhaven.com/a/modular_urban_apartments_facade) | **CC0** | 118k | modular building facade kit | Outdoor buildings (decimate) |

### Props

| Asset | Source | License | Polys | Notes |
|---|---|---|---|---|
| **Street Asset Pack** (vmatthew) | [Sketchfab](https://sketchfab.com/3d-models/street-asset-pack-f3eb47d02e2e4ab290e66752fa354b48) | **CC BY 4.0** | 37.8k | 4 traffic signs, 4 metal barriers, concrete + plastic barrier, drum, cone, bin, hydrant, poles — 1–2K PBR |
| Barrier & Traffic Cone Pack (sabriayes) | [Sketchfab](https://sketchfab.com/3d-models/barrier-traffic-cone-pack-23c4dfca76a24bf0b21894847867af2a) | **CC BY 4.0** | 5.1k | 12 models, 2K PBR, built for Unity |
| T-Bollard | [Sketchfab](https://sketchfab.com/3d-models/t-bollard-e7e5f9b0c9c046869e81dd2fecd9dbc1) | **CC BY 4.0** | 475 tris | 1K textures |
| Poly Haven `mounted_fluorescent_lights` | [Poly Haven](https://polyhaven.com/a/mounted_fluorescent_lights) | **CC0** | 17.8k | garage ceiling lights — 1K glTF 1.8 MB / 2K 4.9 MB |
| Poly Haven `street_lamp_02` | [Poly Haven](https://polyhaven.com/a/street_lamp_02) | **CC0** | 20k | 1K 1.8 MB / 2K 4.9 MB |
| Poly Haven `concrete_road_barrier_02` | [Poly Haven](https://polyhaven.com/a/concrete_road_barrier_02) | **CC0** | 42.6k | decimate; 2K 9.4 MB |
| Poly Haven `covered_car` | [Poly Haven](https://polyhaven.com/a/covered_car) | **CC0** | 12.6k | parked car under a cover; 2K 7.7 MB |
| Poly Haven `WetFloorSign_01`, `utility_box_02`, `security_light`, `water_manhole_cover` | Poly Haven | **CC0** | 0.2k–6k | small dressing; 1K 0.3–2.3 MB each |

### Textures (Poly Haven, CC0) — 4 maps each (diffuse, normal-GL, roughness, AO)

| Use | Texture | 1K set | 2K set |
|---|---|---|---|
| Outdoor asphalt | `clean_asphalt`, `asphalt_02` | 2.7 / 2.9 MB | 12.7 / 11.7 MB |
| Garage floor | `hangar_concrete_floor`, `smooth_concrete_floor` | 2.4 / 2.4 MB | 10.1 / 11.0 MB |
| Garage walls/ceiling | `concrete_wall_004`, `painted_concrete` | 2.5 / 2.7 MB | 10.5 / 12.3 MB |
| Pavements | `concrete_pavement_02` | 3.3 MB | 14.1 MB |

### Other sources checked

| Source | Result |
|---|---|
| Kenney ([Car Kit](https://kenney.nl/assets/car-kit)) | **CC0** verified — but toy-like low-poly style; not suitable for a realistic simulator |
| CGAxis | **Could not verify** — cgaxis.com returns 403 to automated access. Check its license page yourself before using anything from it |
| Unity Asset Store | Standard Unity Asset Store EULA allows use in games, but every download needs **your Unity account** in Package Manager — manual only |
| Sketchfab | Downloads need a **Sketchfab login**; archive sizes are only shown after login |

## 4. Recommended set for the first polished level (garage)

Player car **Generic passenger car pack → sedan**; garage **idmental.id Parking garage**; dressing **Innerscene barrier set**,
**Street Asset Pack**, **T-Bollard**, Poly Haven **mounted_fluorescent_lights**, **WetFloorSign_01**, **covered_car**;
surfaces **hangar_concrete_floor**, **concrete_wall_004**, **painted_concrete** (2K, downscaled to 1K on import where
distance allows). Ramps and directional signage will be built in Unity (ProBuilder geometry with the Poly Haven
concrete; sign faces drawn from the game's own fonts) — no source above provides a modular ramp. Outdoor level after
approval: **windofglass Parking lot**, Poly Haven street lamps, facades, asphalt and pavement.

## 5. Attribution required (CC BY)

CC BY assets must be credited in the game (a Credits screen) with title, author, source link and license.
Credits file to be kept at `Assets/_Project/ThirdParty/ATTRIBUTION.md` from the first import.

## 6. Android optimisation plan (applied in Phase E)

Decimate anything over ~25k tris per prop / 80k per environment; textures to 1–2K with ASTC; merge static meshes
and mark them static; LOD groups on cars and large props (LOD0 full, LOD1 ~50 %, cull at 2–3 % screen); box/convex
colliders only; baked lightmaps for the garage with light probes for the car; one realtime shadow (main light) at
most; the garage's strip lights become emissive + baked, not realtime.
