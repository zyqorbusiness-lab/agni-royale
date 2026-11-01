# Agni Royale — Intro mascot ("Agni golu")

Game opens → our own orange blob mascot with a flame tuft hops in from the left → stops centre → looks at you and WINKS (with a sparkle + blush) → blows a KISS (lips pucker, hand to mouth, throws a heart that beats twice and floats away) → waves → runs off right → "AGNI ROYALE" pops in letter-by-letter, "Zyqor Studio" under it → fade to dark → next scene (loading/lobby). ~5 s, tap/click/any key skips to the fade.

Everything is drawn in code at runtime — no image files: blob body = wobbling mesh, eyes/pupils/hands/feet/blush/bokeh/heart/flame/sparkle = textures generated in `IntroMascot.cs`. Own design (style inspired by Sujon's reference: round orange body, big googly eyes, thin legs, soft shadow, cream background with soft orange bokeh). No Garena / Free Fire anything.

## Files
- `IntroMascot.cs` — the whole intro (namespace `Agni.Intro`): builds its own ortho camera + world + UI overlay, plays the timeline, preloads the next scene with `LoadSceneAsync(allowSceneActivation=false)` and activates it at the end. Also contains `VerticalGradient` (UI mesh effect for the title gradient).
- `Editor/AgniIntroSetup.cs` — menu **Agni Royale ▸ Intro ▸ Create Intro Scene + Put First In Build** (also `-executeMethod Agni.Intro.EditorTools.AgniIntroSetup.CreateBatch`). Creates `Assets/Agni/Intro/Intro.unity` with one `AgniIntro` GameObject and inserts it at build index 0 (keeps all other scenes after it).
- `Resources/AgniIntro/LilitaOne-Regular.ttf` (title), `Baloo2.ttf` (subtitle), `intro_sfx.wav` (pops/ting/heartbeat/whoosh/chime — synthesized in code with numpy, no samples). If the fonts are missing the script falls back to Unity's built-in font; if the wav is missing it runs silent.
- `Licenses/OFL_LilitaOne.txt`, `Licenses/OFL_Baloo2.txt`.

Both scripts were compile-checked outside Unity against the project's Unity 6000.3 engine/UI/InputSystem assemblies (0 errors). They have NOT yet been run inside the Unity editor/player (Unity was busy with the map build) — first run: look at it in Play mode once.

## Wire it in (one time)
1. Open the project (or batchmode): run **Agni Royale ▸ Intro ▸ Create Intro Scene + Put First In Build**.
   Batchmode: `timeout -k 15 300 /home/user/unity/6000.3.27f2/Editor/Unity -batchmode -quit -nographics -projectPath /home/user/tab/files/zyqor_game/AtollZero -executeMethod Agni.Intro.EditorTools.AgniIntroSetup.CreateBatch -logFile /tmp/agni/intro_setup.log`
2. That makes build order: `Intro` (0) → whatever was first before (e.g. `AgniMap`, later the Loading/Lobby scene) (1).
3. Optional: on the `AgniIntro` object set `nextSceneName` (e.g. "Loading") instead of "next in build order"; `allowSkip`, `playSound`, `duration`.
4. Build the APK as usual. Unity splash stays OFF, so this intro is the first thing the player sees.

Notes: input uses the new Input System (`ENABLE_INPUT_SYSTEM`; legacy input also handled if enabled). Uses the pipeline's default sprite material for meshes/lines/sprites, so it works with URP (Universal or 2D renderer). Runs at targetFrameRate 60; layout is designed at 1280×720 and centred, so wider phones (20:9) just show more background.

## Timeline (seconds)
| t | what happens |
|---|---|
| 0.0–1.0 | 3 hops in from the left, squash & stretch, leg walk cycle, arms swing, flame trails |
| 1.0–1.4 | lands centre, springy settle, looks at camera, quick blink |
| 1.4–2.0 | cheeky head tilt, right eye WINK (closed ◡ + lash flick), gold sparkle, blush |
| 2.0–2.9 | kiss: lean back, lips pucker, hand to mouth → throws, heart pops out of the hand, beats twice, floats up-right growing + 3 mini hearts |
| 2.9–3.8 | waves, looks right, anticipation squash, hops off to the right |
| 3.8–5.0 | AGNI ROYALE pops letter-by-letter (elastic), "Zyqor Studio" slides up, fade to dark → next scene |

## Preview + source
- Preview video (rendered from code, same timeline): `design/intro_mascot_preview.mp4` (1280×720, 60 fps, H.264 + AAC).
- Renderer: `design/intro_src/render_intro.py` (skia-python) + `sfx.py`; re-render: `python3 render_intro.py raw | ffmpeg -f rawvideo -pix_fmt rgba -s 1280x720 -r 60 -i - -i intro_sfx.wav … out.mp4`.

## CREDITS (add to CREDITS.md)
- Lilita One — © 2011 Juan Montoreano, SIL Open Font License 1.1 (Google Fonts).
- Baloo 2 — © 2019 The Baloo 2 Project Authors (Ek Type), SIL Open Font License 1.1 (Google Fonts).
- Mascot, animation and sound effects — original, made in code by Zyqor Studio for Agni Royale.

## Status (Nov 1, step 2a)
Wired: Intro.unity is build index 0 and `nextSceneName` = "Loading" (set by Agni.UI.EditorTools.AgniUISetup.CreateBatch). Credits lines added to CREDITS.md. Runs in the APK built 05:55Z.
