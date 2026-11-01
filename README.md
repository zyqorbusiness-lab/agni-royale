# AGNI ROYALE

A mobile battle royale on an **India-shaped island**, by **Zyqor Studio**. Built in **Unity 6.3 LTS (6000.3.27f2)**, URP (Mobile + PC renderer assets) and the Input System. Distributed as an **APK** from Zyqor Studio's website (Android).

Status: **step 1 - map terrain blockout** (no gameplay yet). The map design is in the design doc (MAP_DESIGN.md, kept outside the repo).

## What's in step 1
- `Assets/Agni/Scenes/AgniMap.unity` is the only build scene.
- **Terrain:** a 4 x 4 grid of 1024 m Terrain tiles, 4096 m square in total. Each tile has a 513^2 heightmap and a 512^2 splat map, and uses only the layers it needs.
  - The island follows **India's official outline**. It is traced from Natural Earth's India point-of-view boundary, so all of J&K (including PoK), Aksai Chin and Ladakh are inside India. The Andaman islets are included.
  - Landforms: a Himalaya/Ladakh snow ridge (up to ~170 m), the flat Thar and the white Rann of Kutch, the Aravalli and Vindhya-Satpura ridges, the Deccan plateau, and the Western and Eastern Ghats. The central **Vindhya Shikhar** hot-drop peak is 96 m. Beaches run all round the coast.
  - Rivers: Ganga (with the Hooghly), Yamuna, Narmada, Godavari, Krishna and Brahmaputra, carved as shallow channels that fill from the sea-level water plane.
- **Terrain layers:** sand, grass, dry grass, rock, snow and mustard-field yellow. All are CC0 textures from Poly Haven at 1k, compressed to ASTC 6x6 on Android.
- **Blockout:** each of the 15 zones (+ Pathar Gaon) has a placeholder building cluster, an orange marker pole and a floating name label. The 5 small spots (Anaj Godown, Radio Mast, Bijli Ghar, Purana Hawai Adda, Khandahar) get the same treatment.
  - Each zone has a simple stand-in for its signature: the arch for Vijay Dwar, the clock tower, the four minarets, the mill with its chimney, the lighthouse, the palace, and so on.
  - Also placed: the Sona Highway loop plus side roads and a rail line (road strips routed around the sea and steep ground), 20 bridges as simple boxes, and ~5,200 Kenney trees placed by region.
  - Lighting: a procedural sky, linear fog and one sun.
- **Viewer camera** (`AgniFlyCam`):
  - Phone: drag on the left side to fly, drag on the right side to look. Buttons: UP / DOWN (hold), NEXT ZONE (jumps zone to zone) and DROP VIEW.
  - PC: WASD, Q/E, right mouse to look, N for the next zone.

## Rebuild the map
1. Data: `Tools/terrain_gen/agni_terrain_gen.py` writes `TerrainSource/` (heights, splats, `map.json`: zones, spots, roads, bridges, rivers).
2. Scene: menu **Agni Royale -> Rebuild map scene**, or
   `Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod AgniWorldBuilder.BuildBatch -logFile build_scene.log`
   It recreates `Assets/Agni/Generated/` (terrain data, layers, materials, road meshes, tree prefabs) and `Assets/Agni/Scenes/AgniMap.unity`. Those files are not stored in git. After a fresh clone, the editor rebuilds them automatically the first time it opens (`AgniAutoRebuild`), and `AgniBuild.BuildAndroidApk` does the same if the scene is missing.
3. Proof images: **Agni Royale -> Render proof images**. In batchmode this needs a GL context, e.g. Xvfb + `-force-glcore`, without `-nographics`.

## Android build
The build is set up for Android: company **Zyqor Studio**, product **Agni Royale**, package `com.zyqorstudio.agniroyale`, landscape only, Unity splash/logo **off**, **IL2CPP + ARM64**, min API 25, target API auto (36), APK (not AAB).
- Editor: menu **Agni Royale -> Build test APK**.
- CLI: `Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod AgniBuild.BuildAndroidApk -out ../builds/AgniRoyale_test.apk -logFile build.log`

Original game by Zyqor Studio; not affiliated with any other battle-royale title. Asset credits: CREDITS.md.
