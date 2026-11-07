# Asset catalog — Ultimate Car Parking 3D

Research date: 5 November 2026. Research only: **nothing has been downloaded or imported**, and no gameplay code was changed. A spreadsheet copy of every row is in [ASSET_CATALOG.csv](ASSET_CATALOG.csv).

## How each entry was checked

- **Sketchfab:** name, author, licence, downloadable flag, face count, formats, texture count and largest texture size come from Sketchfab's public API (`/v3/search` and `/v3/models/{uid}`). Each page link is the canonical URL the API returned for that model. The search was limited to downloadable models under CC0 or CC BY.
- **Poly Haven:** name, authors, poly count, maximum texture size and formats come from `api.polyhaven.com/info` and `/files`. Every link returned HTTP 200.
- **AmbientCG:** ids come from `ambientcg.com/api/v2`, and the links returned HTTP 200. Of AmbientCG's 34 3D models, all are food items, so it contributes **materials, road-line decals and sky HDRIs only**.
- **Unity Asset Store:** the 8 package pages were read directly. They give the price, licence, file size and render-pipeline table. None of the pages states poly counts or texture sizes.
- **CGTrader:** the model pages refused automated access (HTTP 202 with an empty body), and Claude in Chrome was not connected. Their licences are **not verified**, so they are listed separately and are **not recommended until checked by hand**.

## Licences in this catalog

| Licence | Commercial use in a paid or ad-supported game | Attribution | Notes |
|---|---|---|---|
| CC0 1.0 (Poly Haven, AmbientCG) | Yes | Not required | Public-domain dedication. |
| CC BY 4.0 (all Sketchfab picks) | Yes | **Required**: title, author, source link, licence, and a note that it was changed | Add each one to `ThirdParty/ATTRIBUTION.md` and to the in-game Credits screen, which doesn't exist yet. |
| Standard Unity Asset Store EULA, *Extension Asset* | Yes, inside a product with substantial original content, including in-app purchases (EULA Appendix 1 §2.2.1) | Not required | Seat licence: up to 2 computers (§2.3.2). Use for AI training is not allowed. |
| CGTrader "Royalty Free" | Usually yes, but it is set per model by the seller | Not stated in the general terms | **Unverified here.** Some free models are "Royalty Free, No AI" or editorial-only. |

## Things to know before approving downloads

1. **Realistic vs low-poly.** Most free, *mobile-ready* models are stylised low-poly. The photoreal sources (Poly Haven, scans) are heavy: often 8K textures and 20k–300k+ polygons. A realistic look on the Galaxy A33 means using Poly Haven and similar sources at **1K textures**, adding LODs, and decimating. Mixing art styles in one arena will look wrong. **Preview each model on its page before approving it.**
2. **App size.** The current APK is already 220 MB because imported models carry uncompressed embedded textures. The audit's Phase 1 (texture extraction, ASTC compression, Addressables) should happen **before** importing more than a handful of these.
3. **No real car brands.** Free vehicle models of real makes (Fiat, Jaguar, Volvo, Ford and others) were excluded. CC BY covers the model's copyright, not the manufacturer's trademarks or trade dress. All vehicles listed are generic or have fictional names.
4. **Landmarks.** CC BY covers the 3D model only. Some buildings and their names are protected as trademarks, or their images are controlled. The Sydney Opera House, Burj Khalifa, Petronas Towers and the Eiffel Tower's night lighting are commonly cited examples. **Get a legal check before shipping any landmark**, and prefer using them as distant skyline backdrops.
5. **Sign standards.** Road signs differ by country. Only Japan, Russia and the UK have country-specific packs here. Others need custom sign textures, which are simple flat decals.

Priority column: **A** = mobile-ready, recommended for the first download wave. **B** = good, but needs texture downscaling. **C** = heavy (decimation or baking needed) or background only.

## 1. Buildings and city environments

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Exterior Aircon Unit](https://polyhaven.com/a/exterior_aircon_unit) | Poly Haven — Monsta3D | CC0 1.0 | Free – no account | 18,986 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Building dressing (hot climates) |  |
| A | [Modular Fire Escape](https://polyhaven.com/a/modular_fire_escape) | Poly Haven — Juniix | CC0 1.0 | Free – no account | 11,322 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | USA (New York) |  |
| A | [Brooklyn Street Row Buildings Low Poly](https://sketchfab.com/3d-models/brooklyn-street-row-buildings-low-poly-370ca970f4cd4c638f95fe17617d0bc1) | Sketchfab — B0rn2D13 (B0rn2D13) | CC BY 4.0 | Free – Sketchfab account required | 6,208 | 2 × ≤2048 px | 7.8 MB | glb, glTF, USDZ, original source upload | USA (New York) | Row-house street wall, 6.2k faces |
| A | [European Mansion Building Facade (Low poly)](https://sketchfab.com/3d-models/european-mansion-building-facade-low-poly-4a1a565b876c4ebab16f323bea6e830c) | Sketchfab — Ishxxn (1shxxn) | CC BY 4.0 | Free – Sketchfab account required | 3,486 | 1 × ≤512 px | 0.5 MB | glb, glTF, USDZ, original source upload | France / Germany | 3.5k faces, 512 px texture – mobile ready |
| A | [European building facades](https://sketchfab.com/3d-models/european-building-facades-4d16a815c650402a9ecdc4a270f48ec5) | Sketchfab — bean(alwayshasbean) (alwayshasbean) | CC BY 4.0 | Free – Sketchfab account required | 34,202 | 4 × ≤2048 px | 15.1 MB | glb, glTF, USDZ, original source upload | France / Spain / Italy street | Facades only (no interiors) – ideal for kerb-side parking |
| A | [Old Building / Lowpoly](https://sketchfab.com/3d-models/old-building-lowpoly-1f304c6e56324c19955a18aa79b15b9f) | Sketchfab — Frid.blend (Fridqeir) | CC BY 4.0 | Free – Sketchfab account required | 2,872 | 1 × ≤1024 px | 1.1 MB | glb, glTF, USDZ, original source upload | Russia / Eastern Europe | Very light (2.9k faces, 1 texture 1K) |
| B | [Modular Factory Facade](https://polyhaven.com/a/modular_factory_facade) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 175,014 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Industrial / port arenas | Heavy – decimate / LOD |
| B | [Modular Urban Apartments Facade](https://polyhaven.com/a/modular_urban_apartments_facade) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 118,215 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Europe / USA city | Heavy – decimate / LOD |
| B | [City pack 7](https://sketchfab.com/3d-models/city-pack-7-6550e37439814c348e2c884aa740ca3c) | Sketchfab — Pasha (Pasha.) | CC BY 4.0 | Free – Sketchfab account required | 31,124 | 107 × ≤1024 px | 14.2 MB | glb, glTF, USDZ, original source upload | Generic city filler | 107 small textures – atlas them |
| B | [Low Poly Night City Building Skyline](https://sketchfab.com/3d-models/low-poly-night-city-building-skyline-b0035b8713b048bb8ddf311ee67c28c8) | Sketchfab — 99.Miles (99.Miles) | CC BY 4.0 | Free – Sketchfab account required | 6,078 | 5 × ≤2048 px | 10.0 MB | glb, glTF, USDZ, original source upload | USA – night city; any night-parking arena | Skyline backdrop; 5 textures ≤2K |
| B | [Low-poly City Buildings](https://sketchfab.com/3d-models/low-poly-city-buildings-e0209ac5bb684d2d85e5ade96c92d2ff) | Sketchfab — smooth998 (smooth998) | CC BY 4.0 | Free – Sketchfab account required | 7,166 | 4 × ≤4096 px | 32.9 MB | glb, glTF, USDZ, original source upload | Generic city filler (any) | 4K textures – downscale to 1K |
| B | [Modern Building - Gallery - Office](https://sketchfab.com/3d-models/modern-building-gallery-office-5546323923cf4b539071809d87a3e3b8) | Sketchfab — cjmitchell (cjmitchell) | CC BY 4.0 | Free – Sketchfab account required | 4,995 | 6 × ≤4096 px | 78.2 MB | glb, glTF, USDZ, original source upload | Modern business district (UAE, Singapore) | 4K textures – downscale |
| C | [Buildings Front](https://sketchfab.com/3d-models/buildings-front-75876ad5f6bf4a30baa71673c303213d) | Sketchfab — Zambur (Zambur) | CC BY 4.0 | Free – Sketchfab account required | 8,322 | 12 × ≤4096 px | 161.3 MB | glb, glTF, USDZ, original source upload | UK / Europe street wall | 161 MB glb, 12×4K textures – heavy; bake down |
| C | [Procedural Hong Kong building](https://sketchfab.com/3d-models/procedural-hong-kong-building-528a732e84c44fd49c4726f341014a23) | Sketchfab — uday (udayjeet) | CC BY 4.0 | Free – Sketchfab account required | 197,097 | 7 × ≤2048 px | 34.3 MB | glb, glTF, USDZ, original source upload | China (Hong Kong) | 197k faces – background only after decimation |

## 2. Roads and sidewalks

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Painted Wooden Bench](https://polyhaven.com/a/painted_wooden_bench) | Poly Haven — Kirill Sannikov | CC0 1.0 | Free – no account | 630 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Any |  |
| A | [Utility Box 01](https://polyhaven.com/a/utility_box_01) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 4,404 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any street dressing |  |
| A | [Utility Box 02](https://polyhaven.com/a/utility_box_02) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 6,268 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any street dressing |  |
| A | [Water Manhole Cover](https://polyhaven.com/a/water_manhole_cover) | Poly Haven — Raunox | CC0 1.0 | Free – no account | 6,301 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Any road |  |
| A | [Modular City Road Pack – Game Ready](https://sketchfab.com/3d-models/modular-city-road-pack-game-ready-d9df5cfa079f4da8a18ef1372c00d01c) | Sketchfab — Salah3D (abbasyounis214) | CC BY 4.0 | Free – Sketchfab account required | 9,402 | 9 × ≤1024 px | 3.1 MB | glb, glTF, USDZ, original source upload | All arenas – city road kit | 9.4k faces, 1K textures, 3 MB |
| A | [Modular Havana Street – Low-Poly Asset Kit](https://sketchfab.com/3d-models/modular-havana-street-low-poly-asset-kit-74f1e89642114d448168b2f0a1f1d56b) | Sketchfab — TRYFIELD (tryfield) | CC BY 4.0 | Free – Sketchfab account required | 7,079 | 18 × ≤1024 px | 17.5 MB | glb, glTF, USDZ, original source upload | Cuba (Havana) | Street kit with buildings, 1K textures |
| A | [Modular Roads Pack](https://sketchfab.com/3d-models/modular-roads-pack-99023ee0b2564395b47b40d22f51f7c2) | Sketchfab — SquadCell (SquadCell) | CC BY 4.0 | Free – Sketchfab account required | 1,461 | 2 × ≤1024 px | 1.6 MB | glb, glTF, USDZ, original source upload | All arenas – base road kit | 1.5k faces, 2×1K textures |
| A | [Modular sidewalk curb kit](https://sketchfab.com/3d-models/modular-sidewalk-curb-kit-c762ff5b7d294f36b8349f8ca3662eaa) | Sketchfab — Just8 (Just8) | CC BY 4.0 | Free – Sketchfab account required | 266 | 8 × ≤2048 px | 28.9 MB | glb, glTF, USDZ, original source upload | All arenas – kerbs (parallel parking) | 266 faces; 8 textures up to 2K |
| B | [Metal Trash Can](https://polyhaven.com/a/metal_trash_can) | Poly Haven — GurJas Studios | CC0 1.0 | Free – no account | 13,960 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any |  |
| B | [Modular Street Seating](https://polyhaven.com/a/modular_street_seating) | Poly Haven — Stuart Attenborrow | CC0 1.0 | Free – no account | 25,156 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Plaza / mall arenas |  |
| B | [Large modular pack of Russian roads](https://sketchfab.com/3d-models/large-modular-pack-of-russian-roads-9fd19243e4fe4ddaaa627551477a9e27) | Sketchfab — Unka II (UnkaII) | CC BY 4.0 | Free – Sketchfab account required | 3,405 | 19 × ≤2048 px | 27.5 MB | glb, glTF, USDZ, original source upload | Russia | Country-specific road set |
| B | [Old Street Pack](https://sketchfab.com/3d-models/old-street-pack-a633537bfc7c4d99a3a618662513cbf9) | Sketchfab — iagoArt3D (iagoDev) | CC BY 4.0 | Free – Sketchfab account required | 964 | 7 × ≤4096 px | 13.3 MB | glb, glTF, USDZ, original source upload | Italy / Old-town arenas | Cobbled street pieces; 4K textures |

## 3. Parking garages and lots

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Security Camera 01](https://polyhaven.com/a/security_camera_01) | Poly Haven — Alexander Otterbeck, Yann Kervran | CC0 1.0 | Free – no account | 13,571 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Garages |  |
| A | [Wet Floor Sign 01](https://polyhaven.com/a/WetFloorSign_01) | Poly Haven — Fran Calvente | CC0 1.0 | Free – no account | 228 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Already in project | Already in project |
| A | [Parking Garage](https://sketchfab.com/3d-models/parking-garage-88c3f49f553f41f59382e0e38781193c) | Sketchfab — dylanheyes (dylanheyes) | CC BY 4.0 | Free – Sketchfab account required | 33,776 | 2 × ≤2048 px | 8.5 MB | glb, glTF, USDZ, original source upload | Multi-storey arena (any country) | 33.8k faces, 2×2K textures |
| A | [Parking garage [FREE DOWNLOAD]](https://sketchfab.com/3d-models/parking-garage-free-download-5310b7d77b70427d936ec4253fff679c) | Sketchfab — Idmenthal (idmental.id) | CC BY 4.0 | Free – Sketchfab account required | 5,370 | 4 × ≤4096 px | 54.2 MB | glb, glTF, USDZ, original source upload | Already in project (Garage B1) | Already imported – re-use |
| B | [Parking Lot UF](https://sketchfab.com/3d-models/parking-lot-uf-b820496c29b94bf3986fe6223d44fc8c) | Sketchfab — hkang2 (hkang2) | CC BY 4.0 | Free – Sketchfab account required | 51,897 | 10 × ≤4096 px | 10.0 MB | glb, glTF, USDZ, original source upload | Supermarket lot (Australia / UK) | 51.9k faces, 4K textures |
| B | [Parking lot](https://sketchfab.com/3d-models/parking-lot-80e54d8326ea4646949961e8ada35518) | Sketchfab — Veterock (windofglass) | CC BY 4.0 | Free – Sketchfab account required | 23,020 | 41 × ≤4096 px | 103.2 MB | glb, glTF, USDZ, original source upload | Open-air lot (USA / Canada) | 103 MB, 41 textures up to 4K – optimise |

## 4. Shopping malls

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Planter Box 01](https://polyhaven.com/a/planter_box_01) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 8,094 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Mall / plaza |  |
| A | [Low Poly Turnstiles](https://sketchfab.com/3d-models/low-poly-turnstiles-12536f966f9048dcba7e33fa08d9d6b5) | Sketchfab — anDDDres (anDDDres) | CC BY 4.0 | Free – Sketchfab account required | 642 | 2 × ≤512 px | 0.2 MB | glb, glTF, USDZ, original source upload | Mall entrance dressing | Turnstiles, 642 faces |
| A | [[FREE] Simple Storefront Interior Pack](https://sketchfab.com/3d-models/free-simple-storefront-interior-pack-348da01c195d46eca29e478e98aab16c) | Sketchfab — 99.Miles (99.Miles) | CC BY 4.0 | Free – Sketchfab account required | 4,779 | 1 × ≤2048 px | 3.2 MB | glb, glTF, USDZ, original source upload | Shop-front interiors visible from car park | 4.8k faces, 1×2K texture |
| B | [Coffee Cart 01](https://polyhaven.com/a/CoffeeCart_01) | Poly Haven — Joe Seabuhr | CC0 1.0 | Free – no account | 27,659 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Plaza / mall |  |
| B | [LowPoly Football field and a Supermarket](https://sketchfab.com/3d-models/lowpoly-football-field-and-a-supermarket-920667014e2d4580af8affb0e54ccc77) | Sketchfab — JG3D (jg-3d) | CC BY 4.0 | Free – Sketchfab account required | 32,838 | none (vertex colour) | 1.6 MB | glb, glTF, USDZ, original source upload | Suburban supermarket (any) | Vertex-coloured, no textures – very light |
| B | [Shopping mall](https://sketchfab.com/3d-models/shopping-mall-e0841a27bfb04ebeae32fe1fb02f65ba) | Sketchfab — iskandar1ch (iskandar1ch) | CC BY 4.0 | Free – Sketchfab account required | 27,728 | 66 × ≤4096 px | 84.2 MB | glb, glTF, USDZ, original source upload | Mall arena (UAE / Singapore / USA) | 27.7k faces but 66 textures up to 4K |

## 5. Houses and shops

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Rollershutter Door](https://polyhaven.com/a/rollershutter_door) | Poly Haven — MP | CC0 1.0 | Free – no account | 1,104 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any shop row / garage |  |
| A | [Asian Shop Pack Free Gameready](https://sketchfab.com/3d-models/asian-shop-pack-free-gameready-2f603ad8d5c640f7b9794f6916f9fbdd) | Sketchfab — abhayexe (abhayexe) | CC BY 4.0 | Free – Sketchfab account required | 39,203 | 28 × ≤1024 px | 22.7 MB | glb, glTF, USDZ, original source upload | Thailand / Malaysia / India | Shop pack, 1K textures |
| A | [Autumn House](https://sketchfab.com/3d-models/autumn-house-52772448c62348e0a4951b51758d5587) | Sketchfab — Zhenya (zhenyarech) | CC BY 4.0 | Free – Sketchfab account required | 7,991 | 5 × ≤2048 px | 12.7 MB | glb, glTF, USDZ, original source upload | Canada / Norway | 8k faces, 2K |
| A | [Gas station](https://sketchfab.com/3d-models/gas-station-eeb913b90b4344ddbd7852f82a7ef160) | Sketchfab — Elbolillo (Elbolilloduro) | CC BY 4.0 | Free – Sketchfab account required | 51,967 | 64 × ≤1024 px | 10.8 MB | glb, glTF, USDZ, original source upload | Gas-station arena (USA / Australia) | 52k faces, 1K textures |
| A | [Gas station Props](https://sketchfab.com/3d-models/gas-station-props-8fcd52f1b12e4b4296589b4c6328b777) | Sketchfab — Elbolillo (Elbolilloduro) | CC BY 4.0 | Free – Sketchfab account required | 12,659 | 55 × ≤1024 px | 4.8 MB | glb, glTF, USDZ, original source upload | Gas-station props | 12.7k faces, 1K |
| A | [Suburban shabby house low poly](https://sketchfab.com/3d-models/suburban-shabby-house-low-poly-f4678340de204c829e79a01f216e1be0) | Sketchfab — Simlion (simlion) | CC BY 4.0 | Free – Sketchfab account required | 9,991 | 5 × ≤2048 px | 27.8 MB | glb, glTF, USDZ, original source upload | USA suburbs | 10k faces, 2K |
| B | [American House](https://sketchfab.com/3d-models/american-house-798ffaeadacf4e08a2665785422fb45d) | Sketchfab — AleixoAlonso (AleixoAlonso) | CC BY 4.0 | Free – Sketchfab account required | 41,991 | 43 × ≤2048 px | 64.1 MB | glb, glTF, USDZ, original source upload | USA suburbs (hero house) | 42k faces, 43 textures |
| C | [Fire Hydrant](https://polyhaven.com/a/fire_hydrant) | Poly Haven — Gonçalo Felício | CC0 1.0 | Free – no account | 86,316 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | USA / Canada | Heavy – decimate / LOD |
| C | [Abandoned / Building / Shop / Old / House](https://sketchfab.com/3d-models/abandoned-building-shop-old-house-0ed14367314946b298d9a58db66da040) | Sketchfab — YadroGames (yadrogames) | CC BY 4.0 | Free – Sketchfab account required | 12,792 | 22 × ≤4096 px | 349.8 MB | glb, glTF, USDZ, original source upload | Mexico / Latin America | 350 MB glb – heavy, 22×4K |
| C | [Japanese house 2.0](https://sketchfab.com/3d-models/japanese-house-20-5ae0121e33b54b7789c6b9113da444bf) | Sketchfab — annzep (annzep) | CC BY 4.0 | Free – Sketchfab account required | 174,487 | 9 × ≤1024 px | 8.2 MB | glb, glTF, USDZ, original source upload | Japan | 174k faces – decimate |

## 6. Trees and vegetation

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Coconut Palm](https://sketchfab.com/3d-models/coconut-palm-26e787f2ff2e4c0fb004c3b0210805a3) | Sketchfab — evolveduk (evolveduk) | CC BY 4.0 | Free – Sketchfab account required | 7,432 | 15 × ≤1024 px | 11.8 MB | glb, glTF, USDZ, original source upload | Tropical (Thailand, Brazil, India) | Coconut palm, 1K |
| A | [Palm Trees](https://sketchfab.com/3d-models/palm-trees-55690379305145488e20afb05fc687e6) | Sketchfab — YadroGames (yadrogames) | CC BY 4.0 | Free – Sketchfab account required | 2,857 | 7 × ≤2048 px | 4.9 MB | glb, glTF, USDZ, original source upload | UAE / Brazil / Australia / Mexico | Palms, 2.9k faces |
| A | [Sakura Tree 01 - Low Poly Model](https://sketchfab.com/3d-models/sakura-tree-01-low-poly-model-147ae7d0d332456a99ec6195e9b0cd4f) | Sketchfab — Jogoss (thejogoss9) | CC BY 4.0 | Free – Sketchfab account required | 7,007 | 2 × ≤2048 px | 5.1 MB | glb, glTF, USDZ, original source upload | Japan / South Korea | Sakura, 7k faces |
| A | [Trees](https://sketchfab.com/3d-models/trees-dbd3f20966de4cae9170413b9c6f891d) | Sketchfab — DJMaesen (bumstrum) | CC BY 4.0 | Free – Sketchfab account required | 9,858 | 12 × ≤2048 px | 24.2 MB | glb, glTF, USDZ, original source upload | Temperate arenas | 9.9k faces |
| A | [Trees and bush Pack LOWPOLY](https://sketchfab.com/3d-models/trees-and-bush-pack-lowpoly-f2a25ee70df440c9ab03d57aba2dc3f2) | Sketchfab — sixonetwo (evan4129) | CC BY 4.0 | Free – Sketchfab account required | 212 | 7 × ≤2048 px | 8.9 MB | glb, glTF, USDZ, original source upload | Any (distant filler) | 212 faces |
| B | [Shrub 03](https://polyhaven.com/a/shrub_03) | Poly Haven — Rico Cilliers | CC0 1.0 | Free – no account | 16,955 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Any |  |
| B | [Shrub 04](https://polyhaven.com/a/shrub_04) | Poly Haven — Rico Cilliers | CC0 1.0 | Free – no account | 47,813 | PBR set; choose 1K/2K/4K/8K (max 4096 px) | — | glTF, FBX, Blend, USD | Any |  |
| B | [Low Poly Winter Tree Pack](https://sketchfab.com/3d-models/low-poly-winter-tree-pack-824b124215fe4e459b895c9a5ff6f5a5) | Sketchfab — 99.Miles (99.Miles) | CC BY 4.0 | Free – Sketchfab account required | 78,207 | 12 × ≤2048 px | 26.5 MB | glb, glTF, USDZ, original source upload | Norway / Canada / Switzerland (winter) | 78k faces for the pack |
| B | [Small bush](https://sketchfab.com/3d-models/small-bush-f6ed4c70fc024ac88e8e6a19991695af) | Sketchfab — yanix (yanix) | CC BY 4.0 | Free – Sketchfab account required | 320 | 1 × ≤4096 px | 5.8 MB | glb, glTF, USDZ, original source upload | Any – hedges / bushes | 320 faces but 1×4K texture – downscale |
| C | [Jacaranda Tree](https://polyhaven.com/a/jacaranda_tree) | Poly Haven — Rob Tuytel, Rico Cilliers | CC0 1.0 | Free – no account | 312,356 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | South Africa / Australia | Heavy – decimate / LOD |
| C | [Quiver Tree 02](https://polyhaven.com/a/quiver_tree_02) | Poly Haven — Dario Barresi, Rico Cilliers | CC0 1.0 | Free – no account | 153,887 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | South Africa / Namibia | Heavy – decimate / LOD |

## 7. Streetlights and traffic signs

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Security Light](https://polyhaven.com/a/security_light) | Poly Haven — Maximilian Schuster | CC0 1.0 | Free – no account | 4,668 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Garages / lots |  |
| A | [Street Lamp 02](https://polyhaven.com/a/street_lamp_02) | Poly Haven — Josh Dean | CC0 1.0 | Free – no account | 20,338 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any |  |
| A | [Japanese Traffic Assets](https://sketchfab.com/3d-models/japanese-traffic-assets-1a4833770ace4df6aecfdabb76e36d60) | Sketchfab — Erik Kinč (erikkinc) | CC BY 4.0 | Free – Sketchfab account required | 29,480 | 82 × ≤2048 px | 64.0 MB | glb, glTF, USDZ, original source upload | Japan | Japan-standard signs and signals |
| A | [London Street Objects](https://sketchfab.com/3d-models/london-street-objects-286fbd6933c0411a9808db6cf815bbd3) | Sketchfab — Pukka Films (pukkafilms) | CC BY 4.0 | Free – Sketchfab account required | 6,652 | 14 × ≤4096 px | 83.3 MB | glb, glTF, USDZ, original source upload | UK (London) | Red phone box / post box style street objects; 4K textures – downscale |
| A | [Modular Russian Road Signs Pack (URBAN)](https://sketchfab.com/3d-models/modular-russian-road-signs-pack-urban-5d6e2672d73c44b3b0f7bf62e43fad55) | Sketchfab — Unka II (UnkaII) | CC BY 4.0 | Free – Sketchfab account required | 10,934 | 50 × ≤1024 px | 2.0 MB | glb, glTF, USDZ, original source upload | Russia | Russian road signs, 2 MB |
| A | [Sign package](https://sketchfab.com/3d-models/sign-package-2d153f387f264bca819357601eff3574) | Sketchfab — Pedram Ashoori (pedramashoori) | CC BY 4.0 | Free – Sketchfab account required | 572 | 2 × ≤512 px | 0.5 MB | glb, glTF, USDZ, original source upload | Any – generic sign posts | 572 faces, 512 px |
| A | [Street Asset Pack](https://sketchfab.com/3d-models/street-asset-pack-f3eb47d02e2e4ab290e66752fa354b48) | Sketchfab — vmatthew (vmatthew) | CC BY 4.0 | Free – Sketchfab account required | 37,840 | 138 × ≤2048 px | 151.4 MB | glb, glTF, USDZ, original source upload | Already in project | Already imported – re-use |
| B | [Street Lamp 01](https://polyhaven.com/a/street_lamp_01) | Poly Haven — Josh Dean | CC0 1.0 | Free – no account | 30,610 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Europe / any |  |
| B | [Abandoned street lights pack](https://sketchfab.com/3d-models/abandoned-street-lights-pack-c853eca300e8435d8fe9206bcb524eda) | Sketchfab — sergeilihandristov (sergeilihandristov) | CC BY 4.0 | Free – Sketchfab account required | 22,315 | 3 × ≤2048 px | 14.4 MB | glb, glTF, USDZ, original source upload | Any (worn look) | Street-light pack, 2K |
| B | [Road Traffic Sign](https://sketchfab.com/3d-models/road-traffic-sign-13eeb2da01c046a1a0fc8ce8a7e262ab) | Sketchfab — Solarliu (jacky15621419) | CC BY 4.0 | Free – Sketchfab account required | 7,210 | 3 × ≤1024 px | 4.0 MB | glb, glTF, USDZ, original source upload | Generic (verify sign standard) | 7.2k faces |
| B | [Road signs pack](https://sketchfab.com/3d-models/road-signs-pack-7fac2b1b6c5746daa4ac773441c45180) | Sketchfab — robiworks (robiworks) | CC BY 4.0 | Free – Sketchfab account required | 69,000 | 22 × ≤2048 px | 21.2 MB | glb, glTF, USDZ, original source upload | Europe (verify standard) | 69k faces, 22 textures |
| C | [Modular Electricity Poles](https://polyhaven.com/a/modular_electricity_poles) | Poly Haven — James Ray Cock | CC0 1.0 | Free – no account | 200,610 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Suburban / rural | Heavy – decimate / LOD |
| C | [Traffic Light](https://sketchfab.com/3d-models/traffic-light-240630f6755d43d6bb0af54f7f2d135b) | Sketchfab — miwaVV (mishel1337) | CC BY 4.0 | Free – Sketchfab account required | 43,183 | 32 × ≤2048 px | 77.9 MB | glb, glTF, USDZ, original source upload | Any – hero traffic light | 78 MB, 32 textures |

## 8. Parking barriers and cones

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Barrel_01](https://polyhaven.com/a/Barrel_01) | Poly Haven — Jorge Camacho | CC0 1.0 | Free – no account | 2,682 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Construction |  |
| A | [Barrier & Traffic Cone Pack - Part 2](https://sketchfab.com/3d-models/barrier-traffic-cone-pack-part-2-426e2d6eb39b4947856332aec6be4fb4) | Sketchfab — Sabri Ayeş (sabriayes) | CC BY 4.0 | Free – Sketchfab account required | 967 | 15 × ≤2048 px | 21.3 MB | glb, glTF, USDZ, original source upload | Any | 967 faces |
| A | [Low Poly Vehicle Barrier](https://sketchfab.com/3d-models/low-poly-vehicle-barrier-c8f8615cd51e4488b281cbae169f6c22) | Sketchfab — Leandro Dotta (leandrodotta) | CC BY 4.0 | Free – Sketchfab account required | 136 | none (vertex colour) | 0.0 MB | glb, glTF, USDZ, original source upload | Any – boom gate | 136 faces, vertex colour |
| A | [Modular Speed Humps Plastic FREE Low Poly](https://sketchfab.com/3d-models/modular-speed-humps-plastic-free-low-poly-2275b9794a7349c3a243a9d029618b01) | Sketchfab — Lady Lion Studios (LadyLionStudios) | CC BY 4.0 | Free – Sketchfab account required | 3,692 | 9 × ≤1024 px | 6.5 MB | glb, glTF, USDZ, original source upload | Any – speed humps | 3.7k faces, 1K |
| A | [Road Gate Barrier](https://sketchfab.com/3d-models/road-gate-barrier-586414c41f6c460ca9256ce691fbf93d) | Sketchfab — ngooiwong (ngooi1397) | CC BY 4.0 | Free – Sketchfab account required | 1,088 | 6 × ≤2048 px | 18.8 MB | glb, glTF, USDZ, original source upload | Any – boom gate (detailed) | 1.1k faces, 6×2K |
| B | [Concrete Road Barrier 02](https://polyhaven.com/a/concrete_road_barrier_02) | Poly Haven — Amal Kumar | CC0 1.0 | Free – no account | 42,578 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any |  |
| B | [Large Iron Gate](https://polyhaven.com/a/large_iron_gate) | Poly Haven — Josh Dean | CC0 1.0 | Free – no account | 38,140 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Hotel / mansion arenas |  |
| B | [Modular Chainlink Fence](https://polyhaven.com/a/modular_chainlink_fence) | Poly Haven — James Ray Cock, Amal Kumar | CC0 1.0 | Free – no account | 89,232 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Construction / lot perimeter | Heavy – decimate / LOD |
| B | [Jersey Barrier](https://sketchfab.com/3d-models/jersey-barrier-b52246c9611a42a99a03b425535a0237) | Sketchfab — emran.bayati (emran.bayati) | CC BY 4.0 | Free – Sketchfab account required | 1,714 | 3 × ≤4096 px | 40.4 MB | glb, glTF, USDZ, original source upload | Any – jersey barrier | 4K textures – downscale |
| C | [Concrete Road Barrier](https://polyhaven.com/a/concrete_road_barrier) | Poly Haven — Amal Kumar | CC0 1.0 | Free – no account | 80,776 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Any | Heavy – decimate / LOD |
| C | [Road Barrier](https://sketchfab.com/3d-models/road-barrier-85b489ac36344ca9af98a78ecffa186f) | Sketchfab — Sousinho (sousinho) | CC BY 4.0 | Free – Sketchfab account required | 2,228 | 9 × ≤4096 px | 70.0 MB | glb, glTF, USDZ, original source upload | Construction arenas | 70 MB, 4K textures |
| C | [Traffic Cones](https://sketchfab.com/3d-models/traffic-cones-e03b1d2dfdc0416bb8324d15573ea60c) | Sketchfab — NotAnotherApocalypticCo. (notanotherapocalypticco) | CC BY 4.0 | Free – Sketchfab account required | 18,966 | 6 × ≤4096 px | 51.4 MB | glb, glTF, USDZ, original source upload | Any – hero cones | 19k faces, 4K |

## 9. Traffic vehicles

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Covered Car](https://polyhaven.com/a/covered_car) | Poly Haven — MP | CC0 1.0 | Free – no account | 12,592 | PBR set; choose 1K/2K/4K/8K (max 8192 px) | — | glTF, FBX, Blend, USD | Already in project | Already in project |
| A | [Canyon '75 Taxi - Low poly model](https://sketchfab.com/3d-models/canyon-75-taxi-low-poly-model-9e8f92a215784f3d8aaaaeab1bef54c4) | Sketchfab — Daniel Zhabotinsky (DanielZhabotinsky) | CC BY 4.0 | Free – Sketchfab account required | 20,037 | 14 × ≤2048 px | 12.4 MB | glb, glTF, USDZ, original source upload | USA (taxi) | Fictional model name – no brand |
| A | [Fairheaven LT '80 Cop Cruiser - Low poly model](https://sketchfab.com/3d-models/fairheaven-lt-80-cop-cruiser-low-poly-model-61766c1e6e1e4b2b803c174f33433a74) | Sketchfab — Daniel Zhabotinsky (DanielZhabotinsky) | CC BY 4.0 | Free – Sketchfab account required | 19,505 | 13 × ≤2048 px | 15.9 MB | glb, glTF, USDZ, original source upload | USA (police) | Fictional |
| A | [French delivery van - Low poly model](https://sketchfab.com/3d-models/french-delivery-van-low-poly-model-a350a422d200485ea57778dc42139edc) | Sketchfab — Daniel Zhabotinsky (DanielZhabotinsky) | CC BY 4.0 | Free – Sketchfab account required | 6,963 | 13 × ≤2048 px | 10.3 MB | glb, glTF, USDZ, original source upload | France (delivery van) | Fictional, 7k faces |
| A | [Generic civil service vehicles pack](https://sketchfab.com/3d-models/generic-civil-service-vehicles-pack-8ff2a13f30914932a70c7950cfa58465) | Sketchfab — Comrade1280 (comrade1280) | CC BY 4.0 | Free – Sketchfab account required | 92,709 | 50 × ≤2048 px | 56.0 MB | glb, glTF, USDZ, original source upload | All arenas (police / ambulance / taxi / bus style) | Same author as current car pack – consistent style; 92.7k faces total |
| A | [Generic passenger car pack](https://sketchfab.com/3d-models/generic-passenger-car-pack-20f9af9b8a404d5cb022ac6fe87f21f5) | Sketchfab — Comrade1280 (comrade1280) | CC BY 4.0 | Free – Sketchfab account required | 69,312 | 39 × ≤2048 px | 26.7 MB | glb, glTF, USDZ, original source upload | Already in project | Already imported – the 5 drivable cars + parked fleet |
| A | [Negotiator '80 - Low poly model](https://sketchfab.com/3d-models/negotiator-80-low-poly-model-892b2684fb37442299bfad9222cf331c) | Sketchfab — Daniel Zhabotinsky (DanielZhabotinsky) | CC BY 4.0 | Free – Sketchfab account required | 19,747 | 13 × ≤2048 px | 11.5 MB | glb, glTF, USDZ, original source upload | USA (sedan) | Fictional |
| A | [Shvan '92 - Low poly model](https://sketchfab.com/3d-models/shvan-92-low-poly-model-09d718c9cf72401b8534d265a06a803f) | Sketchfab — Daniel Zhabotinsky (DanielZhabotinsky) | CC BY 4.0 | Free – Sketchfab account required | 23,045 | 13 × ≤2048 px | 12.6 MB | glb, glTF, USDZ, original source upload | Europe (van) | Fictional |

## 10. Country-specific landmarks

| Pri | Model | Source / author | Licence | Download | Faces | Textures | glb size | Formats | Arena | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| A | [Burj Khalifa](https://sketchfab.com/3d-models/burj-khalifa-59e6dd74e5f647158de568b5a7f9cab7) | Sketchfab — ManySince910 (noears6) | CC BY 4.0 | Free – Sketchfab account required | 16,692 | 1 × ≤32 px | 1.0 MB | glb, glTF, USDZ, original source upload | UAE | 16.7k faces – see legal note |
| A | [Clock Tower (Big Ben)](https://sketchfab.com/3d-models/clock-tower-big-ben-9a1c691fac774bcca4e2ba565c4c9d9b) | Sketchfab — ManySince910 (noears6) | CC BY 4.0 | Free – Sketchfab account required | 38,540 | 1 × ≤1024 px | 2.6 MB | glb, glTF, USDZ, original source upload | UK | 38.5k faces, 1×1K |
| A | [Gula's CN Tower](https://sketchfab.com/3d-models/gulas-cn-tower-36b4f641d3e74d77918770adac8e2f6a) | Sketchfab — gula (gula) | CC BY 4.0 | Free – Sketchfab account required | 1,809 | 3 × ≤1024 px | 3.5 MB | glb, glTF, USDZ, original source upload | Canada | 1.8k faces |
| A | [Hagia Sophia](https://sketchfab.com/3d-models/hagia-sophia-63d3b49f01be4f4995cc08b7f8b66ada) | Sketchfab — Iron Furkan 7 (firkancelebi) | CC BY 4.0 | Free – Sketchfab account required | 21,440 | none (vertex colour) | 0.8 MB | glb, glTF, USDZ, original source upload | Turkey | 21k faces |
| A | [Japanese Torii Gate](https://sketchfab.com/3d-models/japanese-torii-gate-2027a248de1b4b70985ff97e708fb50d) | Sketchfab — Sahir Virmani (sahirvirmani) | CC BY 4.0 | Free – Sketchfab account required | 1,252 | 3 × ≤2048 px | 5.6 MB | glb, glTF, USDZ, original source upload | Japan | Torii gate, 1.3k faces |
| A | [PETRONAS TWIN TOWER](https://sketchfab.com/3d-models/petronas-twin-tower-97423ef101ac40d38604e94f72863d00) | Sketchfab — Nurfaamk (nurhudaiffah) | CC BY 4.0 | Free – Sketchfab account required | 32,470 | 1 × ≤64 px | 1.8 MB | glb, glTF, USDZ, original source upload | Malaysia | 32k faces – see legal note |
| A | [Shinjuku Park Tower (Tokyo)](https://sketchfab.com/3d-models/shinjuku-park-tower-tokyo-0b53fac684a448349220104e8309956c) | Sketchfab — aitortilla01 (aitortilla01) | CC BY 4.0 | Free – Sketchfab account required | 14,165 | 3 × ≤4096 px | 2.9 MB | glb, glTF, USDZ, original source upload | Japan (Tokyo skyline) | 14k faces |
| B | [( FREE ) La tour Eiffel](https://sketchfab.com/3d-models/free-la-tour-eiffel-8553f94d06e24cb4b0fde1080f281674) | Sketchfab — SDC PERFORMANCE™️ (Lambo_SC04) | CC BY 4.0 | Free – Sketchfab account required | 147,604 | none (vertex colour) | 5.7 MB | glb, glTF, USDZ, original source upload | France | 148k faces, no textures – decimate |
| B | [Barcelona Sagrada Familia](https://sketchfab.com/3d-models/barcelona-sagrada-familia-da32af2e358844eb92a0b2b97c793b46) | Sketchfab — Roberto Domínguez (vmmaniac) | CC BY 4.0 | Free – Sketchfab account required | 3,968 | 1 × ≤4096 px | 4.8 MB | glb, glTF, USDZ, original source upload | Spain | 4k faces, 4K texture |
| B | [Christ the Redeemer](https://sketchfab.com/3d-models/christ-the-redeemer-d143e5357efd42529777438d0f72c79c) | Sketchfab — JuanG3D (juang3d) | CC BY 4.0 | Free – Sketchfab account required | 58,762 | none (vertex colour) | 3.8 MB | glb, glTF, USDZ, original source upload | Brazil | 58.8k faces, no textures |
| B | [Golden Gate Bridge](https://sketchfab.com/3d-models/golden-gate-bridge-a0ee5a9c285849c0819af5f366be3835) | Sketchfab — JuanG3D (juang3d) | CC BY 4.0 | Free – Sketchfab account required | 93,110 | none (vertex colour) | 4.1 MB | glb, glTF, USDZ, original source upload | USA (San Francisco) | 93k faces |
| B | [Kukulcan Chichen Itza](https://sketchfab.com/3d-models/kukulcan-chichen-itza-7657ecbf3ba64584a6aab9fd81455cdc) | Sketchfab — JuanXGL (juanfx1.0) | CC BY 4.0 | Free – Sketchfab account required | 61,428 | 4 × ≤2048 px | 12.1 MB | glb, glTF, USDZ, original source upload | Mexico | 61k faces |
| B | [Leaning tower of Pisa](https://sketchfab.com/3d-models/leaning-tower-of-pisa-f0456bc36bdc4bfd8890f0dfdaf86656) | Sketchfab — Aglaiapoulida (Aglaiapoulida) | CC BY 4.0 | Free – Sketchfab account required | 54,454 | 1 × ≤8192 px | 23.0 MB | glb, glTF, USDZ, original source upload | Italy | 54k faces, 1×8K texture – downscale |
| B | [Machu Picchu - Peru, Andes Mountains](https://sketchfab.com/3d-models/machu-picchu-peru-andes-mountains-ce5fa32af6cf46ca9de57e1a780db651) | Sketchfab — Nate Loper  🗺️ ⛏🏺 (arizonaguide) | CC BY 4.0 | Free – Sketchfab account required | 8,192 | 1 × ≤4096 px | 37.8 MB | glb, glTF, USDZ, original source upload | Peru | 8k faces, 4K texture – terrain backdrop |
| B | [Statue of liberty](https://sketchfab.com/3d-models/statue-of-liberty-04a8e7b1ba9142ba8cebc6108cd5bb97) | Sketchfab — 3Dystopia (Dystopia) | CC BY 4.0 | Free – Sketchfab account required | 19,780 | 3 × ≤4096 px | 21.6 MB | glb, glTF, USDZ, original source upload | USA | 19.8k faces, 3×4K |
| B | [Sydney Opera House](https://sketchfab.com/3d-models/sydney-opera-house-317b2d540f0a4f7e8d87dd3b0372712d) | Sketchfab — Nick Reinhardt (Nickardiamond) | CC BY 4.0 | Free – Sketchfab account required | 62,424 | 23 × ≤2048 px | 27.4 MB | glb, glTF, USDZ, original source upload | Australia | 62k faces – see legal note |
| C | [Brandenburg Gate (9 Mb)](https://sketchfab.com/3d-models/brandenburg-gate-9-mb-012f2cb87cfa47029808063da072ad5c) | Sketchfab — Res1n (Res1n) | CC BY 4.0 | Free – Sketchfab account required | 390,218 | 10 × ≤512 px | 13.9 MB | glb, glTF, USDZ, original source upload | Germany | 390k faces |
| C | [Greek style ancient temple](https://sketchfab.com/3d-models/greek-style-ancient-temple-b3445beab77142eb888085033a4db42f) | Sketchfab — MMC Works (mmcworks) | CC BY 4.0 | Free – Sketchfab account required | 161,748 | 23 × ≤2048 px | 76.8 MB | glb, glTF, USDZ, original source upload | Greece | Generic temple, 162k faces |
| C | [Japanese Torii gate Game Asset](https://sketchfab.com/3d-models/japanese-torii-gate-game-asset-e12d2fa1b2b94928b8b87cb7787e2462) | Sketchfab — Bazylonator (Bazylonator) | CC BY 4.0 | Free – Sketchfab account required | 15,564 | 3 × ≤4096 px | 73.5 MB | glb, glTF, USDZ, original source upload | Japan (alt torii) | 73 MB glb |
| C | [Taj Mahal](https://sketchfab.com/3d-models/taj-mahal-1c7e6ccc93d74681ae74c3d71c252789) | Sketchfab — Meshick (meshick) | CC BY 4.0 | Free – Sketchfab account required | 310,585 | none (vertex colour) | 24.6 MB | glb, glTF, USDZ, original source upload | India | 310k faces – decimate heavily |
| C | [The Great Pyramid of Giza](https://sketchfab.com/3d-models/the-great-pyramid-of-giza-4a251113722f4d969b6cf2ca5f35c502) | Sketchfab — ianicolo (ianicolo) | CC BY 4.0 | Free – Sketchfab account required | 360,321 | 1 × ≤4096 px | 18.0 MB | glb, glTF, USDZ, original source upload | Egypt | 360k faces |

## Surface materials, decals and skies (CC0)

These cover roads, sidewalks and line markings better than modelled roads do: tile a material on simple ground meshes and paint lines with decals.

| Asset | Source | Type | Use | Download |
|---|---|---|---|---|
| [Road007](https://ambientcg.com/view?id=Road007) | AmbientCG | Material: JPG/PNG maps 1K–8K | Asphalt with dashed centre line | Free – no account |
| [Road012A](https://ambientcg.com/view?id=Road012A) | AmbientCG | Material: JPG/PNG maps 1K–8K | Asphalt road surface | Free – no account |
| [Asphalt033](https://ambientcg.com/view?id=Asphalt033) | AmbientCG | Material: JPG/PNG maps 1K–8K | Dark asphalt (car park) | Free – no account |
| [RoadLines019A](https://ambientcg.com/view?id=RoadLines019A) | AmbientCG | Decal: JPG/PNG maps 1K–8K | Road-line decal set | Free – no account |
| [RoadLines022A](https://ambientcg.com/view?id=RoadLines022A) | AmbientCG | Decal: JPG/PNG maps 1K–8K | Road-line decal set | Free – no account |
| [ManholeCover010](https://ambientcg.com/view?id=ManholeCover010) | AmbientCG | Decal: JPG/PNG maps 1K–8K | Manhole decal | Free – no account |
| [ManholeCover011](https://ambientcg.com/view?id=ManholeCover011) | AmbientCG | Decal: JPG/PNG maps 1K–8K | Manhole decal | Free – no account |
| [PavingStones150](https://ambientcg.com/view?id=PavingStones150) | AmbientCG | Material: JPG/PNG maps 1K–8K | Cobble / paving (Europe) | Free – no account |
| [Foliage001](https://ambientcg.com/view?id=Foliage001) | AmbientCG | Decal: JPG/PNG maps 1K–8K | Foliage atlas for hedges / bushes | Free – no account |
| [DayEnvironmentHDRI114](https://ambientcg.com/view?id=DayEnvironmentHDRI114) | AmbientCG | HDRI: JPG/PNG maps 1K–8K | Amsterdam city sky – Netherlands arena | Free – no account |
| [asphalt_02](https://polyhaven.com/a/asphalt_02) | Poly Haven (texture) | PBR texture set 1K–8K | Asphalt | Free – no account |
| [clean_asphalt](https://polyhaven.com/a/clean_asphalt) | Poly Haven (texture) | PBR texture set 1K–8K | Car-park asphalt | Free – no account |
| [asphalt_snow](https://polyhaven.com/a/asphalt_snow) | Poly Haven (texture) | PBR texture set 1K–8K | Winter arenas | Free – no account |
| [concrete_pavement](https://polyhaven.com/a/concrete_pavement) | Poly Haven (texture) | PBR texture set 1K–8K | Sidewalk | Free – no account |
| [brick_pavement](https://polyhaven.com/a/brick_pavement) | Poly Haven (texture) | PBR texture set 1K–8K | Europe sidewalk | Free – no account |
| [herringbone_pavement](https://polyhaven.com/a/herringbone_pavement) | Poly Haven (texture) | PBR texture set 1K–8K | Plaza / mall | Free – no account |
| [cobblestone_floor_01](https://polyhaven.com/a/cobblestone_floor_01) | Poly Haven (texture) | PBR texture set 1K–8K | Old-town Europe | Free – no account |
| [red_sandstone_pavement](https://polyhaven.com/a/red_sandstone_pavement) | Poly Haven (texture) | PBR texture set 1K–8K | India / Middle East | Free – no account |
| [hexagonal_concrete_paving](https://polyhaven.com/a/hexagonal_concrete_paving) | Poly Haven (texture) | PBR texture set 1K–8K | Modern plaza | Free – no account |
| [road_damaged](https://polyhaven.com/a/road_damaged) | Poly Haven (texture) | PBR texture set 1K–8K | Worn roads | Free – no account |

## Unity Asset Store (free, verified pages)

All are listed as free under the Standard Unity Asset Store EULA (Extension Asset). Attribution isn't required. The pages state no polygon counts or texture sizes. All are low-poly or cartoon in style, so they suit filler better than a realistic look.

| Package | Publisher | Size / pipeline (from page) | Category |
|---|---|---|---|
| [Low Poly Street Pack](https://assetstore.unity.com/packages/3d/environments/urban/low-poly-street-pack-67475) | Dynamic Art | 70.5 MB; v1.0 2016; pipeline not stated | Roads |
| [Free Low Poly Simple Urban City 3D Asset Pack](https://assetstore.unity.com/packages/3d/environments/urban/free-low-poly-simple-urban-city-3d-asset-pack-239474) | Draftpunk Studios | 2.1 MB; Built-in only (URP 'Not compatible') | Buildings |
| [Cartoon City FREE - Low Poly City 3D Models Pack](https://assetstore.unity.com/packages/3d/environments/urban/cartoon-city-free-low-poly-city-3d-models-pack-328170) | ithappy | 113.6 MB; Unity 6 URP compatible; cartoon style | Buildings |
| [Low Poly City Asset Pack](https://assetstore.unity.com/packages/3d/environments/urban/low-poly-city-asset-pack-306443) | LTKDev | 9.4 MB; Built-in only (URP 'Not compatible') | Buildings |
| [Low Poly Urban Pack](https://assetstore.unity.com/packages/3d/environments/low-poly-urban-pack-375158) | AIRIDEV | 16.4 MB; Unity 6000.3 URP compatible | Buildings |
| [Parking details (small pack)](https://assetstore.unity.com/packages/3d/props/parking-details-small-pack-126101) | Game Stuff Studio | 16.3 MB; pipeline not stated | Barriers & cones |
| [Street Props - Prototype Collection](https://assetstore.unity.com/packages/3d/props/street-props-prototype-collection-291021) | Amplify Creations | 1.4 GB download; URP compatible | Lights & signs |
| [SimplePoly City - Low Poly Assets](https://assetstore.unity.com/packages/3d/environments/simplepoly-city-low-poly-assets-58899) | VenCreations | 3.4 MB; 2017; pipeline not stated | Buildings |

The Asset Store packages marked "Built-in only" would need their materials converted to URP.

## CGTrader (NOT verified; do not download yet)

These were found by search, but their pages could not be read automatically. Licence, formats and poly counts are unknown. Check each page in a browser before considering it.

- [Low Poly City Pack 01 Collection](https://www.cgtrader.com/free-3d-models/exterior/cityscape/low-poly-city-01-ready-collection)
- [City Buildings Lowpoly Game Ready](https://www.cgtrader.com/free-3d-models/exterior/cityscape/city-buildings-lowpoly-game-ready)
- [City building pack lowpoly](https://www.cgtrader.com/free-3d-models/exterior/cityscape/bu-779540bb-edb1-44ec-8d09-bd2623ad7b01)
- [Low Poly City Buildings](https://www.cgtrader.com/free-3d-models/exterior/house/low-poly-city-pack-demo)

Other sources not searched in depth: Kenney.nl (CC0, stylised), Quaternius (CC0, stylised) and the Smithsonian 3D open-access collection (CC0 scans). The first two match a stylised direction; ask if you want them added.

## Proposed country arenas and coverage

A draft list of 30 arenas, chosen from the landmark and dressing assets that exist. It isn't final; the arena and level structure is still your decision.

| # | Country | Assets found |
|---|---|---|
| 1 | USA | Statue of Liberty, Golden Gate, row houses, fire escapes, taxi/police/sedan, gas station |
| 2 | Canada | CN Tower, autumn house, winter trees |
| 3 | Mexico | Kukulcan pyramid, palms |
| 4 | Cuba | Havana street kit |
| 5 | Brazil | Christ the Redeemer, palms |
| 6 | Peru | Machu Picchu backdrop |
| 7 | UK | Big Ben, London street objects (left-hand traffic) |
| 8 | France | Eiffel Tower, European facades, French delivery van |
| 9 | Germany | Brandenburg Gate (heavy), mansion facade |
| 10 | Italy | Leaning Tower, old street pack |
| 11 | Spain | Sagrada Família, facades |
| 12 | Netherlands | Amsterdam HDRI sky, brick paving (no windmill found) |
| 13 | Greece | Generic temple (heavy) |
| 14 | Turkey | Hagia Sophia |
| 15 | Russia | Russian roads and signs, old building |
| 16 | Egypt | Great Pyramid (heavy) |
| 17 | UAE | Burj Khalifa, palms, modern building, mall |
| 18 | India | Taj Mahal (heavy), shop pack, red sandstone paving |
| 19 | China | Hong Kong building (heavy) |
| 20 | Japan | Torii, Shinjuku tower, Japanese signs, sakura (left-hand traffic) |
| 21 | South Korea | Sakura, generic city (no landmark found) |
| 22 | Malaysia | Petronas Towers, shop pack (left-hand traffic) |
| 23 | Singapore | Modern building, mall (no landmark found) |
| 24 | Thailand | Shop pack, coconut palm (left-hand traffic) |
| 25 | Australia | Opera House, supermarket lot, gas station (left-hand traffic) |
| 26 | South Africa | Jacaranda and quiver trees (left-hand traffic) |
| 27 | Norway | Winter trees, autumn house, asphalt-snow |
| 28 | Switzerland | Winter trees (no Matterhorn found) |
| 29 | Cambodia | No suitable low-poly Angkor Wat found |
| 30 | Morocco / generic desert | No dedicated assets found |

**Gaps (no suitable free, mobile-friendly model found):** Dutch windmill, Kremlin, Great Wall, Marina Bay Sands, Gateway of India, Parthenon, Matterhorn, Neuschwanstein (only a pixel-style version), Angkor Wat. Search hits for these were either very heavy scans (1–8 million faces) or not the subject. Country number plates and most national sign sets also need to be made in-house as textures.

## Suggested first download wave (needs your approval)

Priority-A rows only, enough for one realistic proof arena plus shared street dressing:

- Painted Wooden Bench (Poly Haven, CC0 1.0)
- Utility Box 01 (Poly Haven, CC0 1.0)
- Utility Box 02 (Poly Haven, CC0 1.0)
- Water Manhole Cover (Poly Haven, CC0 1.0)
- Modular City Road Pack – Game Ready (Sketchfab, CC BY 4.0)
- Modular Havana Street – Low-Poly Asset Kit (Sketchfab, CC BY 4.0)
- Modular Roads Pack (Sketchfab, CC BY 4.0)
- Modular sidewalk curb kit (Sketchfab, CC BY 4.0)
- Security Camera 01 (Poly Haven, CC0 1.0)
- Parking Garage (Sketchfab, CC BY 4.0)
- Security Light (Poly Haven, CC0 1.0)
- Street Lamp 02 (Poly Haven, CC0 1.0)
- Japanese Traffic Assets (Sketchfab, CC BY 4.0)
- London Street Objects (Sketchfab, CC BY 4.0)
- Modular Russian Road Signs Pack (URBAN) (Sketchfab, CC BY 4.0)
- Sign package (Sketchfab, CC BY 4.0)
- Barrel_01 (Poly Haven, CC0 1.0)
- Barrier & Traffic Cone Pack - Part 2 (Sketchfab, CC BY 4.0)
- Low Poly Vehicle Barrier (Sketchfab, CC BY 4.0)
- Modular Speed Humps Plastic FREE Low Poly (Sketchfab, CC BY 4.0)
- Road Gate Barrier (Sketchfab, CC BY 4.0)
- Canyon '75 Taxi - Low poly model (Sketchfab, CC BY 4.0)
- Fairheaven LT '80 Cop Cruiser - Low poly model (Sketchfab, CC BY 4.0)
- French delivery van - Low poly model (Sketchfab, CC BY 4.0)
- Generic civil service vehicles pack (Sketchfab, CC BY 4.0)
- Negotiator '80 - Low poly model (Sketchfab, CC BY 4.0)
- Shvan '92 - Low poly model (Sketchfab, CC BY 4.0)
- Plus CC0 surfaces: Road007, Asphalt033, RoadLines019A, concrete_pavement, clean_asphalt
