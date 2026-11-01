# IMPORT STATUS — Blender-made assets → Unity 6 game (worker 3ecf9325, started 2026-11-01 14:55Z)

Why: Sujon (Nov 1, 7:54 PM): "Tune ghar banaya tha blender se uska kya hua" / "Jo jo banaya tha o sab vi add mat".
Rule for this job: source folders (`landmarks/`, `vehicles/`, `weapons/`, `emotes/`, `characters_*`, `pets*/`) are read-only; Unity gets copies under `Assets/Agni/Import/`.

## Inventory (before this job)
| Asset group | Source folder | In Unity before? | Plan |
|---|---|---|---|
| Landmarks: Ghanta Ghar Square (clock tower, 3 shop-houses, steel bridge, plaza) | landmarks/GhantaGhar | NO | import FBX + CC0 textures, LODGroups from _LOD0/1/2, UCX → colliders, finial → weathervane, place on map, loot spots |
| Landmarks: Vindhya Shikhar (hill, lookout haveli, water tank, radio mast) | landmarks/VindhyaShikhar | NO | same; it is a 240 m hill → needs a bigger island |
| Landmarks: Bambai Mill (mill, chimney, dock, tanks, roof w/o stairs) | landmarks/BambaiMill | NO | same, on the west coast |
| Landmarks: Chai Bagan (bungalow, factory, line houses, tea rows) | landmarks/ChaiBagan | NO | same, far east |
| Landmarks: Kayal Village (stilt houses, walkways, jetty, houseboat) | landmarks/KayalVillage | NO | same, on the water (south); boat spawn here |
| Landmarks: Pathar Gaon (stone village, well, chai stall) | landmarks/PatharGaon | NO (a code-built village of the same name exists) | import as its own spot "Pathar Gaon (Pahadi)" at the foot of Vindhya |
| Trees (neem/mango/palm/khejri/gulmohar/banana/lantana) | landmarks/trees | YES (loot job) | keep |
| Grass clumps (9 kinds, 22 variants) | landmarks/grass | YES (block 1b) | 21 variants as terrain detail meshes, biome-based |
| Vehicles ×7 (Shere Jeep, Auto Rickshaw, Chhota Haathi pickup, Dhoom monster truck, Toofan buggy, Phatphati bike, Naav speedboat) + 118 skins | vehicles/ | NO | import, WheelCollider physics, enter/exit, networked, spawns near villages, boat buoyancy, skins from manifest |
| Guns ×6 base models | weapons/<GUN>/<GUN>.fbx | YES (same v2 FBX, albedo JPG only) | add normal + metal/smooth maps |
| Gun premium skins (20 hand-made: Rare/Epic/Legendary/Mythic Lv1-3 + mythic attachments) + 120 generated variants per gun | weapons/<GUN>/skins, skins_manifest.json | NO | import premium skins, skin picker hooked to the 6 loot gun types, applied in hand + on ground |
| Heroes ×10 face v2 (unique heads, 33 blendshapes, Eye bones, hair springs) | characters_face_v2/ | NO (game used characters_anime/Meera only) | import read-only; hero select in lobby; networked hero id in match; AgniFace.cs drives blendshapes; hook for characters_hair_v3 FBX |
| Approved Meera body (+ AgniYoddha / RaatKiRani outfits) | characters_body/Meera | NO | import; Meera uses approved body where it rigs (see notes) |
| Hair v3 (running in another worker) | characters_hair_v3/ | NO | NOT edited; drop-in hook only |
| Emotes ×32 (v2 movement) | emotes/<Name>/Emote_<Name>.fbx | NO | import as legacy clips on the hero rig, emote wheel in lobby (+ in match) |
| Combat anims (19 sets) | anims_combat/ | PARTLY (10 legacy clips) | keep; add Walk/Jump/Punch/Hit if time |
| Pets real (Bagha_Real tiger, White_Tiger_Real) | pets_real/ | NO | lobby companion at HIGH/VERY HIGH |
| Pets cute (Bagha cub, Bagha_White cub, Sheru lion cub, Gajju, Mor) | pets/ | NO (lobby shows a 2D mascot sprite) | lobby companion at LOW/NORMAL; pet picker |
| Sky panoramas (5 times of day) | sky/ | YES dopahar (block 1b); other 4 imported, not used yet | dopahar in the match |
| Audio (music + UI sfx) | audio/ | YES (lobby theme + UI sfx) | keep |

## Progress log

### Block 1 — MAP LANDMARKS (done 2026-11-01 ~15:25Z)
- All 6 Blender landmarks are in the match (AgniArena): Ghanta Ghar Square (east, on the new river; the steel bridge crosses it), Chai Bagan (north-east),
  Vindhya Shikhar (north; its 240 m hill rises ~30 m out of the island), Pathar Gaon (the stone village at the foot of the peak), Bambai Mill (west coast),
  Kayal Village (south, standing in a new backwater canal that opens to the sea).
- The island grew from 310 m to 440 m radius (+ beach) so the landmarks fit around the 7 code villages; ArenaRadius 230 -> 400 (zone, bots, loot scatter all scale);
  trees/bushes/compound walls/props spread over the bigger island and keep off water and landmarks; +10 lone farm huts.
- Ground is now a generated mesh (AgniIslandMesh + AgniIsland.Height): flat play area, river channel under the bridge, Kayal canal, sloping beach. New water shader (AgniRoyale/Water).
- Import pipeline: Editor/AgniImportLandmarks.cs — CC0 textures (Color + NormalGL, 1K, ASTC 6x6 on Android), materials rebuilt from the Blender matdefs on our
  AgniRoyale/Landmark shader (albedo x tint x vertex-colour variation/grime/AO + normal map, URP shadows + fog, no emission/bloom), one LODGroup per part
  (LOD0/1/2; LOW/NORMAL/HIGH/VERY HIGH follow the existing AgniSettings LOD bias/maxLOD), 1,340 UCX hulls -> convex colliders, a mesh collider for the hill.
- Ghanta Ghar: the old finial is cut off the tower top (150 tris) and replaced by our weathervane (rod, ball, N/E/S/W cut-metal letters, arrow that swings with the wind);
  the 8 clock hands run on the phone's local time. No religious symbols anywhere.
- Loot: 186 new loot spots generated by raycasting the kit colliders (rooms with headroom, walled rooms, usable roofs); the mill roof stays gun-free (fist-fight roof).
  Smoke test: 549 stacks on 504 spots, 50 players / 13 squads, no errors. Squads can now be dropped next to landmarks (their footprints are drop spots).
- The code-built ring village "Pathar Gaon" was renamed "Kankar Gaon" so the Blender Pathar Gaon keeps its name.
- Weak spots: the coast's grass/sand edge is a 3 m staircase seen from very high up; the Vindhya hill surface is plain brown; Kayal's water glints get bright toward the sun.

### Block 1b — INDIA MAP + Blender trees / grass / sky (Sujon 9:10 PM: "India map", "tower ghar grass ped sab show") — done 2026-11-01 ~17:30Z (worker 653e812b, continuing 3ecf9325)
- The match map is the INDIA outline (Natural Earth 1:10m, India point-of-view: all of J&K and Ladakh), 4,096 m, 16 terrain tiles at a 2 m height grid,
  rebuilt from Resources/AgniImport/India (TerrainSource heights/splats/zones/roads/bridges) on the server and every client at load, so all see the same ground. The r440 circle island is retired.
  Sea all round; the Siliguri corridor is widened to a ~70 m land bridge so the north-east (Chai Bagan) is reachable.
- 6 Blender landmarks at their MAP_DESIGN zones (Ghanta Ghar Square, Chai Bagan, Vindhya Shikhar on the terrain peak, Pathar Gaon, Bambai Mill, Kayal Village in a carved backwater);
  10 code villages at the other zones + 5 small spots. Pads are flattened into the terrain with smooth falloff.
- Ghanta Ghar plaza is flush with the ground (kit lowered 26 cm): no slab edge; worn-earth splat in the gaps and a 7 m ring round every kit, so paving ends in trampled earth then grass.
- Weathervane is now arrow-only (rod + ball + arrow with a tail fin). The old N/E/S/W cross arms read as a cross from a distance and were removed.
- TREES: 24 Blender tree variants (neem, mango, banyan, gulmohar, ashoka, palm, banana, bamboo, khejri, deodar + snow deodar, lantana, hedge) as terrain trees by biome
  (desert/mountain/coast/north-east/plains by lat-lon-height-splat), 16,840 trees, LOD0-2 + impostor card, trunk colliders on server and clients. Hedge prefab crash fixed.
- GRASS: 21 Blender grass variants as instanced terrain detail on a 2 m grid (meadow, lawn, dry golden, alpine, wildflower/sarson, tall hiding grass, reeds by water only, kaans, fern).
  LOW none; NORMAL 0.35 density, 45 m, LOD1 clumps; HIGH 0.6, 65 m, LOD1; VERY HIGH 0.85, 85 m, full LOD0. Short lawn only round landmarks; no grass on paving or roads.
  (Bug fixed: Unity 6 terrain defaults to coverage scatter, so the old counts drew almost nothing.)
- SKY: the Blender dopahar panorama (Skybox/Panoramic) with its own sun/ambient/fog numbers from sky_settings.json. Subah/shaam/raat/monsoon panoramas are imported but not switched on yet.
- Roads: kachcha roads textured (earth, tyre ruts, grassy verges), highways asphalt with a dashed centre line; bridges fixed (were magenta: missing material).
- Water: ripples fade out with distance (no white pixel noise on the sea from the plane).
- Play-mode test (editor, offline host, AgniShot -agniShotMode india): 50 players / 13 squads, ~880-910 loot stacks on 871 spots, 6 landmarks, 16 tiles, 16,840 trees, 21 grass kinds, dopahar sky.
- Proofs: proof/import_v1/11_india_map_top_labelled.jpg, 12_ghanta_ghar_ground_grass_trees.jpg.
- Weak spots: from the plane the inland terrain is soft (base map); the landscape HUD in the editor proof is laid out for another screen size (shots are HUD-free);
  the terrain is built 2-3 times while the arena loads (load time on phones to check); grass is not rebuilt if GRAPHICS is changed mid-match (applies next match).

## Block 2 — VEHICLES (in, networked, driven in play mode)
- 7 Blender vehicles -> network prefabs (Assets/Agni/Import/Resources/AgniNet/Vehicles, auto-registered with the NetworkManager at start):
  Shera Jeep (4 seats, 4WD), Phatphati bike (2), Toofan Buggy (2), Chhota Haathi pickup (4), Auto-Rickshaw (4, 3 wheels), Dhoom Monster (4, 4WD), Naav speedboat (4, buoyancy).
  Built by Agni.Import.EditorTools.AgniImportVehicles.Batch: fixes the FBX's double axis bake on wheel/steering/motor meshes, URP materials (body paint / shared trim / lights),
  LOD0-2, UCX hulls -> convex colliders, WheelColliders at the hub empties with the README numbers (spring, damper, torque, brakes, steer, anti-roll), seats/exits/buoys wired.
- Server-authoritative: the server runs the physics; clients see an interpolated NetworkTransform. Driver = seat 0 (stick = gas/steer, sprint = handbrake); passengers may shoot, the driver may not;
  knocked/dead riders are ejected at the nearest clear exit. GAADI button on the HUD (BAITHO near a free vehicle / UTRO when seated), B key on PC; driver sees km/h.
- Skins: 3-4 colour variants per vehicle + the shared skin set (vehicle_skins.json, 49 skins) via AgniVehicleSkins; picks are stored per phone (garage UI not built yet).
- Spawns: ~40 per match near villages/landmarks (boats on water). Last run: 7 jeep / 10 bike / 7 buggy / 4 pickup / 5 rickshaw / 1 monster / 5 boats.
- Play-mode test (AgniShot -agniShotMode drive): enter jeep OK; gas 3.5 s -> 39 km/h, 20 m; steer right 2 s -> 41 km/h and turned 80 deg; brake 2.5 s -> 1.3 km/h; exit lands on the ground beside it, seat freed.
- Proofs: 13_vehicle_lineup_rann.jpg (6 land vehicles on the Rann), 14_hero_driving_jeep.jpg, 15_jeep_follow_cam.jpg, 16_naav_boat_river.jpg.
- Weak spots: seated pose is procedural (legs bent, hands to the wheel), not an authored sit clip; no engine sound or exhaust/dust FX yet; bike balance is a code assist;
  no vehicle damage/explosions; not tested with two real phones yet (host only in the editor).
