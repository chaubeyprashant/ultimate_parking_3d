# World Tour — 90-mission audit and mechanics plan

Audited 2026-11-06. 30 countries × 3 missions. Mission order follows the World Tour order in `WorldTourBuilder.Countries` (India → … → Turkey).

## Where things stand

| | Count | Notes |
|---|---|---|
| Missions built and tested | **30** | The first ten countries: India, USA, UK, Japan, UAE, China, South Korea, Thailand, Singapore, Malaysia (3 each). Playable, addressable arenas, tested in PlayMode. |
| Arenas with assets staged, not built | 0 | — (licences in `ATTRIBUTION.md`) |
| Arenas with no assets yet | 25 | Need research + your download approval per batch of five |
| Mechanics implemented and tested | **11 / 20** | See below |

Every mission in the table below other than IN1–IN3 is a **plan**, not a built level.

## The 20 mechanics

| # | Mechanic | Status | C# | Scored on |
|---|---|---|---|---|
| 1 | Parallel parking | ✅ done (Phase C) | `ParallelParkObjective` | time + hits; kerb-gap rule, kerb strikes fail |
| 2 | Reverse parking | ✅ done (Phase C) | `ReverseParkObjective` | time + hits; rear-gap rule, sensor beeps |
| 3 | Precision parking | ✅ **batch 1** | `PrecisionParkObjective` | **accuracy %** (centring, depth, heading); must reach a minimum to count |
| 4 | Valet parking | ✅ **batch 1** | `ValetParkObjective` | **guest rating %** (speeding, harsh driving, hits); handover stop first |
| 5 | Multi-car parking | ⏳ batch 2 | – | – |
| 6 | Pickup truck | 🟡 partial | `forcedCar` = Car_Pickup | needs its own rule (bed clearance / longer bay) — batch 2 |
| 7 | Trailer towing | ⏳ batch 3 | – | needs a trailer joint + asset |
| 8 | Multi-storey parking | ✅ done | `FloorParkObjective` + `CheckpointObjective` | time + hits; WRONG FLOOR rule |
| 9 | Spiral ramp | ⏳ batch 3 | – | – |
| 10 | Timed parking | ✅ **batch 1** | `TimedParkObjective` | **time left %**; hits cost seconds; clock out fails |
| 11 | Moving barriers | ✅ **batch 1** | `MovingBarrier` + `BarrierGateObjective` | barrier hits fail; slide/sink/swing, safety sensor |
| 12 | Hill parking | ⏳ batch 2 | – | – |
| 13 | Snow / slippery | ⏳ batch 3 | – | – |
| 14 | Rain / reduced grip | ⏳ batch 3 | – | – |
| 15 | Night parking | ⏳ batch 2 | – | – |
| 16 | Narrow alley | ✅ **batch 1** | `NarrowAlleyObjective` | **tightest side gap**; wall scrapes fail; reverse-through option |
| 17 | Mechanical parking platform | ✅ done | `MechanicalParkingSystem` + `MechanicalParkObjective` | aborts fail |
| 18 | Multi-stage exam | 🟡 partial | objective sequences work (`MissionRunner`) | needs exam marking sheet — batch 2 |
| 19 | Moving traffic | ⏳ batch 3 | – | – |
| 20 | Large vehicle | 🟡 partial | `forcedCar` | needs bus/van vehicle + swept-path rule — batch 3 |

Shared by all mechanics (batch 1): `IScoredObjective` → `MissionRunner.Quality/ScoreLabel` → `LevelData.scoreMode` (`TimeAndCollisions` | `Quality`) → `StarRating` → results screen label (e.g. "ACCURACY 93%"). Existing levels keep the classic scoring.

Proposed batches: **2** multi-car, pickup rule, hill, night, exam sheet · **3** trailer, spiral ramp, snow, rain, moving traffic, large vehicle (6, or split).

## The first 15 missions — one distinct primary mechanic each

| # | Mission | Arena | Primary mechanic | Scoring | Distinct because |
|---|---|---|---|---|---|
| 1 | IN1 Market Parking | Mumbai ✅ | standard + route (LHT) | time + hits | drive through a live market lane, keep left |
| 2 | IN2 Narrow Lane Reverse | Mumbai ✅ | reverse | time + hits | rear-gap rule, sensor beeps |
| 3 | IN3 Shopping Complex | Mumbai ✅ | precision (upgrade to `PrecisionParkObjective`) | accuracy % | stars from centring, not speed |
| 4 | JP1 Coin Parking ✅ | Tokyo | precision reverse-in, 2.4 m bay, forced compact hatch | accuracy % | Japanese reverse-in convention, tight coin-lot bay |
| 5 | JP2 Alley Reverse ✅ | Tokyo | narrow alley reverse-through (3 m) + carport | tightest gap | side-gap gauge, scrapes fail |
| 6 | JP3 Stacker Parking ✅ | Tokyo | mechanical platform (hydraulic stacker lifts the car 2.2 m) | aborts | the machine carries the car |
| 7 | CN1 Beijing Rush | Beijing | timed | time left | own countdown, hits cost seconds |
| 8 | CN2 Hutong Kerb | Beijing | parallel | kerb gap | kerb strikes |
| 9 | CN3 Night Market | Beijing | night (batch 2) | time + hits | headlights only |
| 10 | KR1 Seoul Hillside | Seoul | hill (batch 2) | handbrake rule | roll-back |
| 11 | KR2 Gangnam Tower | Seoul | multi-storey | time + hits | correct floor |
| 12 | KR3 Licence Exam | Seoul | multi-stage exam (batch 2) | exam marks | marking sheet |
| 13 | AE1 Hotel Valet ✅ | Dubai | valet (supercar, porte-cochère handover) | guest rating | handover + smooth driving |
| 14 | AE2 Supercar Garage ✅ | Dubai | precision underground (supercar, 2 hits fail) | accuracy % | expensive car, zero hits for 3★ |
| 15 | AE3 Desert Resort ✅ | Dubai | moving barrier (resort gate) + timed park (30 s) | time left | wait for the gate, then race the clock |

## Remaining 75 (plan)

Each row: primary mechanic first, secondary after "+".

| Country | M1 | M2 | M3 |
|---|---|---|---|
| Thailand (Bangkok, LHT) | moving traffic | narrow alley + timed | valet |
| Singapore (LHT) | precision | multi-storey + barriers | exam |
| Malaysia (Kuching, LHT) | rain | parallel | hill |
| Indonesia (Jakarta, LHT) | moving traffic | narrow alley | multi-car |
| Saudi Arabia (Riyadh) | large vehicle | valet | timed |
| United Kingdom (London, LHT) ✅ built | parallel at the left kerb (kerb strikes) | reverse down a 3.6 m mews (narrow alley, reverse-through) + end bay | underground car park (ramp to level -1, reverse into permit bay) |
| France (Paris) | parallel | spiral ramp | precision |
| Germany (Berlin) | exam | multi-storey + barriers | trailer |
| Italy (Rome) | narrow alley | hill | timed |
| Spain (Madrid) | spiral ramp | night | parallel |
| Russia (Moscow) | snow | large vehicle | multi-car |
| Netherlands (Amsterdam) | narrow alley (canal kerb) | rain | precision |
| Switzerland (Zurich) | hill + snow | mechanical platform | precision |
| Greece (Athens) | hill | narrow alley | timed |
| USA (New York) ✅ built | moving barrier (supermarket ticket gate) + nose-in | pickup truck reversed to a loading dock (0.5 m) | two-level garage: ramp + correct floor |
| Canada (Toronto) | snow | pickup + trailer | multi-car |
| Mexico (Mexico City) | moving traffic | parallel | night |
| Brazil (São Paulo) | multi-storey + barriers | rain | moving traffic |
| Argentina (Buenos Aires) | reverse | parallel | spiral ramp |
| Nigeria (Lagos) | moving traffic | large vehicle | timed |
| South Africa (Johannesburg, LHT) | pickup truck | trailer | barriers |
| Egypt (Cairo) | narrow alley | large vehicle | night |
| Australia (Sydney, LHT) | trailer | rain | parallel |
| New Zealand (Auckland, LHT) | hill | snow | trailer |
| Turkey (Istanbul) — finale | spiral ramp | moving traffic | multi-stage exam (every skill) |

Coverage: every mechanic appears at least 3 times; no country repeats a primary mechanic; the finale combines them.

## Batch 1 test evidence

`Tests/PlayMode/MechanicsBatch1Tests.cs` — 9/9 pass (real Gameplay scene, imported props):

- Precision: 0.18 m off-centre not accepted ("CENTRE THE CAR…"); centred → ACCURACY 93%, 3★.
- Timed: 3 s clock runs out → failed "OUT OF TIME"; parked in 2.4 s of 20 s → TIME LEFT 19.2s.
- Barrier: an imported metal barrier slides >3 m aside; the safety sensor holds it open while the car is in the lane and closes it when the car leaves; driving into the closed gate fails "HIT THE BARRIER"; driving through when open completes.
- Narrow alley: side sensors read 55 cm on each side between imported concrete barriers (expected 55 cm); clean run completes with TIGHTEST GAP 55cm; steering into the wall fails "SCRAPED THE WALLS"; the reverse variant rejects nose-first arrival and accepts reversing.
- Valet: holding still at the stand hands over the keys and moves the guide to the bay; flooring it drops the rating to 76% ("SMOOTHLY!" / "TOO FAST FOR THE GUEST"); parking finishes with GUEST RATING 76% and < 3★.

Regression: EditMode 74/74, plus PlayMode mission, parking, arena, mechanical and UI suites 52/52.


## Batch 2 as built (differs from the plan above where a mechanic didn't exist yet)

| Country | Built missions | Planned (not yet possible) |
|---|---|---|
| China | CN1 timed park, CN2 hutong parallel, CN3 night-market lane + precision | CN3 night: no night mechanic yet (dusk lighting only) |
| South Korea | KR1 apartment reverse, KR2 underground garage, KR3 licence exam (checkpoint → parallel → precision) | KR1 hill; KR3 exam sheet |
| Thailand | TH1 soi alley + park, TH2 gate + timed, TH3 valet | TH1 moving traffic |
| Singapore | SG1 precision, SG2 ticket-gate garage, SG3 exam sequence | SG3 real exam marking |
| Malaysia | MY1 parallel, MY2 reverse down a lane, MY3 precision | MY1 rain; MY3 hill |
