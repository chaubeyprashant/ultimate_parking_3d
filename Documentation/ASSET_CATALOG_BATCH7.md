# Asset batch 7: premium cars (sports, supercars, premium SUVs, F1, monster trucks)

Date 2026-11-19. Source: Sketchfab (downloadable, CC BY 4.0 only, signed-in account). Licence, title and author were re-read from each file's own glTF `asset.extras` after download. No real car brands: titles, tags, node, material and texture names were screened, and textures of the F1 candidates were inspected by eye.

## Added to the garage (17 cars)
| Class | Car | Model (author) |
|---|---|---|
| Premium sports | Chapman '73, Roadster '00, Asti Stradale '89, Tiara GT '83 Tuned, Duke '69, Negotiator '80, Fairheaven LT '80, Phoenix 455 '71 | Daniel Zhabotinsky low-poly series |
| Premium SUV | Bokaroo '67, Rancher '80 Lifted, Urban '10 | Daniel Zhabotinsky |
| Elite supercar | Cheetah '84, Saba V12 '95, Exterminator '00 | Daniel Zhabotinsky |
| Elite F1 | Velocity F1 | Low Poly-F1 (salasilma13), no textures or logos |
| Elite monster trucks | Goliath '85 Monster, Mammoth '80 Monster | derived from two lifted Zhabotinsky trucks (bigger wheels, raised body) |

Every car is rigged the same way as the rest (four WheelColliders, rotating wheels, box colliders), trimmed to at most 1.93 m wide and 5.2 m long so it fits every mission bay, and tested by driving and braking it.

## Rejected, and why
- **Real brands or marks:** De Tomaso P72, SSC Tuatara, Lincoln Navigator, ZIL-111 limousine, Mercury Grand Marquis limousine, Spark Formula E (titles); Sintura S99 F1 (Michelin, Brembo, Avis, Champion logos in its textures); Team St. George TSG-01 F1 (Bridgestone on the tyres); Generic F1 (McLaren in its material names); Crusher monster truck (Jeep Wrangler in its material names); "Pete" monster truck (Peterbilt) and a Pontiac GTO truck.
- **Looks like ripped game assets:** two monster trucks (material and node names from a game or a frame capture).
- **Cannot be rigged properly:** single merged meshes with no separate wheels (three monster trucks, a dune buggy); a sports car file that contains three cars side by side; a kart and a second F1 with unclear wheel parts (not pursued).

## Notes
- The F1 car is dark with red and yellow stripes. It has no logos, but a team-livery lookalike risk is possible; recolouring is a small change if wanted.
- Credits screen for the CC BY attributions is still to be built.
