# CGTrader heroes v1 — Blender prep

`prep_cgt_hero.py` turns one downloaded CGTrader character into the files under `Assets/Agni/Import/HeroesCgt/<Name>/`:
relink textures, drop hidden junk (teeth/tongue/props), bake the current shape-key mix, fix scale/axis (height / 0.91 match scale),
weld, decimate per part with the face and hands protected (eyes never collapse), one 2K colour atlas (`_opaque`) + one alpha-tested
atlas for hair cards / lashes (`_cut`), teen-safe cover paint where the spec asks for it, LOD0 <= 38k, LOD1 15k, LOD2 5k tris,
4 weights per vertex, FBX export (`<Name>.fbx` = rig + LOD0, `<Name>_LOD12.fbx` = LOD1/LOD2 on the same rig).

    nice -n 10 timeout -k 20 900 blender -b <source.blend> --python prep_cgt_hero.py -- spec_<Name>.json

The specs here carry the source paths of the box this ran on (`zyqor_game/cgtrader/`, read-only; extracted to a scratch dir).
Unity side: `Agni.Import.EditorTools.AgniImportCgtHeroes.Batch` (prefabs + humanoid avatars), proof: `AgniCgtProof.Lineup`,
in-match: AgniShot mode `cgtrun`. Credits: CREDITS.md.
