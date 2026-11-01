# ATOLL ZERO

A mobile battle-royale vertical slice by **Zyqor Studio**, built in **Unity 6.3 LTS (6000.3.27f2)** with the **Universal Render Pipeline** (Universal 3D cross-platform template: separate Mobile and PC renderer assets) and the **Input System** package.

Status: project skeleton (phase 1 — toolchain set up). Gameplay comes next.

## Open it
1. Install Unity Hub, then Unity **6000.3.27f2** (any 6000.3.x LTS opens it) with *Android Build Support* if you want phone builds.
2. Unity Hub → **Add → Add project from disk** → pick this folder.
3. Open `Assets/Scenes/SampleScene.unity`.

## Android build
Already set up: build target Android, company **Zyqor Studio**, product **Atoll Zero**, package `com.zyqorstudio.atollzero`, landscape only, Unity splash/logo **off**, **IL2CPP + ARM64** (runs on every modern phone; Play-store ready), target API auto (36), min API 25.

- In the editor: menu **Atoll Zero → Build test APK** (or File → Build Profiles → Android → Build).
- From the command line:
  `Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod AtollBuild.BuildAndroidApk -out ../builds/AtollZero.apk -quit -logFile build.log`
  (add `-mono` for a quick Mono/ARMv7 test build).

Original game; not affiliated with any existing battle-royale title.
