# Agni Royale — Realistic pets: credits & licences

## Base mesh (both tigers)
**"White Tiger (RIGGED ANIMATED)"** by **MotionStreamStudios** (the licence file in the download names the author as **CGJAY1111**, https://sketchfab.com/CGJAY1111, the same account under an older name)
- Source: https://sketchfab.com/3d-models/white-tiger-rigged-animated-9dd099d283e54f99b7cbd40b531b1a29
- Licence: **CC BY 4.0** (http://creativecommons.org/licenses/by/4.0/). Commercial use is allowed. Credit is REQUIRED.
- Downloaded 2026-11-01 through the Sketchfab API (licence re-checked by the API right before download). The original licence.txt and credit.json are in `_src/base_licence/`.
- **Required in-game / store credit line:**
  > Tiger model based on "White Tiger (RIGGED ANIMATED)" by MotionStreamStudios / CGJAY1111 (https://sketchfab.com/3d-models/white-tiger-rigged-animated-9dd099d283e54f99b7cbd40b531b1a29), licensed under CC BY 4.0. Modified by Agni Royale: rescaled, decimated to LODs, re-rigged, new animations, Bengal recolour.

### Changes we made (CC BY needs these listed)
- Scaled to 1 m at the shoulder and 1.22 m to the ear tips. Turned to face −Y in Blender (+Z in Unity) with the ground at 0.
- Decimated from 33,074 to 24k / 12k / 4k tris (LOD0/1/2).
- Removed the original 121-joint rig. Re-skinned onto our own 39-bone quadruped rig: the original skin weights were merged onto our bones. Jaw, tongue and lip weights go to Jaw.
- The original single 937-frame Idle clip is not used. We keyframed 5 new clips ourselves in Blender (code in `_src/`).
- **Bagha_Real**: the albedo was recoloured to Bengal orange in code. Dorsal shade is deeper, the belly, inner legs, chin, muzzle, cheeks and eye spots stay white, the eyes are amber, and the stripes are the original pattern. **White_Tiger_Real** keeps the original albedo.
- Normal and roughness maps are the original ones. The Unity MetallicSmoothness map is derived from the original roughness.

## Our own work (100% ours)
- The rig, all 5 animations, recolour code, render/proof scripts (`_src/*.py`).
- `_src/tiger_sdf.py`, `tiger_paint.py`, `s1/s2*.py`: the earlier from-scratch procedural tiger attempt. Not shipped. Kept for reference only.
- Proof render scene: procedural sky, grass blades generated in code. No external assets.

## Not used / rejected
- "Tiger rebuilt" (kenchoo, CC-BY): a mix of the base above and a second model that is no longer on Sketchfab, so its licence can't be verified. Rejected.
- Low-poly "Tiger"/"Running Tiger" uploads with identical 4,650-face counts by different users: likely re-uploads of unknown origin. Rejected.
- No AI-generated meshes or textures. No Garena/PUBG/Free Fire assets. No Sketchfab NC/ND/editorial licences.

## Note for the legal check
The base albedo is clearly made from photographic fur. A small unused corner of that 2K atlas even shows a photo crop of a tiger face. We rely on the uploader's CC BY 4.0 grant for the whole model, textures included. Before a store launch, two options: (a) keep it, with the credit above; or (b) swap the albedo for our procedural one (pipeline in `_src/`) for zero third-party texture risk.
