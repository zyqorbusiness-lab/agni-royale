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
| Grass cards (10 kinds) | landmarks/grass | NO | later (not in this brief; noted) |
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
| Sky panoramas (5 times of day) | sky/ | ? | not in this brief; noted |
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
