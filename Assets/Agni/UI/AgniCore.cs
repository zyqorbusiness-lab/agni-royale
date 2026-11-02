// Agni Royale — player profile + settings (PlayerPrefs) + proof-mode flag.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Agni.UI
{
    public static class AgniBoot
    {
        static int proof = -1;
        /// Editor-only screenshot mode: Unity started with "-agniProof <dir>".
        public static bool ProofMode { get { if (proof < 0) proof = (Application.isEditor && ProofDir != null) ? 1 : 0; return proof == 1; } }
        public static string ProofDir
        {
            get
            {
                var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-agniProof");
                return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null;
            }
        }
    }

    public static class AgniProfile
    {
        public const string KName = "agni.playerName", KLevel = "agni.level", KXp = "agni.xp", KCoins = "agni.coins", KGems = "agni.gems";
        public static string Name { get => PlayerPrefs.GetString(KName, ""); set { PlayerPrefs.SetString(KName, value); PlayerPrefs.Save(); } }
        public static bool HasName => !string.IsNullOrEmpty(Name);
        public static int Level => Mathf.Max(1, PlayerPrefs.GetInt(KLevel, 1));
        public static float XpFrac => Mathf.Clamp01(PlayerPrefs.GetFloat(KXp, 0.08f));
        public static int Coins => PlayerPrefs.GetInt(KCoins, 0);
        public static int Gems => PlayerPrefs.GetInt(KGems, 0);
        public const string RankName = "KAANSA", RankTier = "III";

        /// 4–12 characters; letters (any script), digits, space, _ . -   Returns null if OK, else a Hinglish error.
        public static string Validate(string n)
        {
            n = (n ?? "").Trim();
            if (n.Length < 4) return "Kam se kam 4 akshar chahiye";
            if (n.Length > 12) return "Zyada se zyada 12 akshar";
            foreach (char c in n) if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '.' || c == '-')) return "Sirf letters, numbers aur _ . - chalega";
            if (n.Contains("  ")) return "Do space ek saath nahi";
            return null;
        }
    }

    public enum GfxPreset { Smooth = 0, Standard = 1, Ultra = 2, VeryHigh = 3 } // shown as LOW / NORMAL / HIGH / VERY HIGH

    public static class AgniSettings
    {
        const string KGfx = "agni.gfx", KFps = "agni.fps", KSens = "agni.sens", KSfx = "agni.sfx", KMusic = "agni.music", KLang = "agni.lang";
        public static GfxPreset Gfx { get => (GfxPreset)PlayerPrefs.GetInt(KGfx, (int)AutoGfx()); set { PlayerPrefs.SetInt(KGfx, (int)value); Save(); } }
        public static int Fps { get => PlayerPrefs.GetInt(KFps, 30); set { PlayerPrefs.SetInt(KFps, value); Save(); } }   // v0.2.1: 30 FPS by default (cooler, smoother on most phones)

        /// v0.2.1: first-launch default picked from the phone itself. Most Android phones get LOW or NORMAL; only big-RAM
        /// phones with a strong GPU start on HIGH. The player can always change it in ⚙ (saved on the phone).
        public static GfxPreset AutoGfx()
        {
            if (Application.isEditor || !Application.isMobilePlatform) return GfxPreset.Standard;
            int ram = SystemInfo.systemMemorySize, vram = SystemInfo.graphicsMemorySize, cores = SystemInfo.processorCount;
            string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            bool weakGpu = gpu.Contains("mali-g5") || gpu.Contains("mali-t") || gpu.Contains("mali-g3") || gpu.Contains("powervr") || gpu.Contains("adreno (tm) 5") || gpu.Contains("adreno (tm) 4") || gpu.Contains("adreno (tm) 6");
            bool strongGpu = gpu.Contains("adreno (tm) 7") || gpu.Contains("adreno (tm) 8") || gpu.Contains("immortalis") || gpu.Contains("mali-g7") || gpu.Contains("xclipse");
            if (ram < 4500 || cores < 6 || (weakGpu && ram < 6500)) return GfxPreset.Smooth;
            if (strongGpu && ram >= 7500) return GfxPreset.Ultra;
            return GfxPreset.Standard;
        }
        public static float Sensitivity { get => PlayerPrefs.GetFloat(KSens, 1f); set { PlayerPrefs.SetFloat(KSens, value); Save(); } }
        public static float Sfx { get => PlayerPrefs.GetFloat(KSfx, 0.8f); set { PlayerPrefs.SetFloat(KSfx, value); Save(); } }
        public static float Music { get => PlayerPrefs.GetFloat(KMusic, 0.6f); set { PlayerPrefs.SetFloat(KMusic, value); Save(); } }
        public static int Language { get => PlayerPrefs.GetInt(KLang, 1); set { PlayerPrefs.SetInt(KLang, value); Save(); } } // 0 English, 1 Hinglish, 2 Hindi (soon)
        public static event Action Changed;
        static void Save() { PlayerPrefs.Save(); Apply(); Changed?.Invoke(); }

        public static void ResetDefaults()
        {
            foreach (var k in new[] { KGfx, KFps, KSens, KSfx, KMusic, KLang }) PlayerPrefs.DeleteKey(k);
            Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            // FPS
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps >= 60 ? 60 : 30;
            // Graphics: quality level by name (project has "Mobile" and "PC"), plus URP render scale / MSAA / shadows on device.
            string want = Gfx >= GfxPreset.Ultra ? "PC" : "Mobile";
            int idx = Array.IndexOf(QualitySettings.names, want);
            if (idx >= 0 && QualitySettings.GetQualityLevel() != idx) QualitySettings.SetQualityLevel(idx, true);
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // (skipped in the editor so play-testing never rewrites the project's pipeline assets)
                // v0.2.1 phone tuning: render below native resolution on LOW/NORMAL (the biggest single win on phones),
                // no shadows on LOW, short shadows on NORMAL, MSAA only on the top tiers.
                // v0.2.2 (Sujon: "normal graphics ke liye sab ka quality bahut low, high graphics high quality"): a WIDE gap between tiers.
                // LOW / NORMAL: very light (low render scale, no realtime shadows, no MSAA/HDR, no post). HIGH / VERY HIGH: full res, real soft shadows, MSAA.
                switch (Gfx)
                {
                    case GfxPreset.Smooth: urp.renderScale = 0.65f; urp.msaaSampleCount = 1; urp.shadowDistance = 0f; urp.supportsHDR = false; break;
                    case GfxPreset.Standard: urp.renderScale = 0.75f; urp.msaaSampleCount = 1; urp.shadowDistance = 0f; urp.supportsHDR = false; break;
                    case GfxPreset.Ultra: urp.renderScale = 1.0f; urp.msaaSampleCount = 2; urp.shadowDistance = 70f; urp.supportsHDR = false; break;
                    case GfxPreset.VeryHigh: urp.renderScale = 1.0f; urp.msaaSampleCount = 4; urp.shadowDistance = 120f; urp.supportsHDR = true; break;
                }
            }
            // textures: LOW = 1/4 resolution, NORMAL = 1/2, HIGH / VERY HIGH = full
            QualitySettings.globalTextureMipmapLimit = Gfx == GfxPreset.Smooth ? 2 : Gfx == GfxPreset.Standard ? 1 : 0;
            // models: LOW / NORMAL switch to the simpler LODs much earlier; VERY HIGH keeps full detail far out
            QualitySettings.lodBias = Gfx == GfxPreset.Smooth ? 0.45f : Gfx == GfxPreset.Standard ? 0.7f : Gfx == GfxPreset.Ultra ? 1.4f : 2.2f;
            QualitySettings.shadows = Gfx <= GfxPreset.Standard ? UnityEngine.ShadowQuality.Disable : UnityEngine.ShadowQuality.All;
            QualitySettings.softParticles = Gfx >= GfxPreset.Ultra;
            QualitySettings.anisotropicFiltering = Gfx >= GfxPreset.Ultra ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            ApplyWorld();
            // Sound: SFX sources read AgniSettings.Sfx, music sources read AgniSettings.Music (see AgniLobby.music).
        }

        static bool hooked;
        /// What the camera can't see isn't drawn: per-tier view distance, per-layer cull distances, terrain grass/detail
        /// range and density, and terrain basemap distance. Runs at startup, on every scene load and on a settings change.
        public static void ApplyWorld()
        {
            if (!hooked) { hooked = true; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => ApplyWorld(); }
            float view, detailD, detailDens, basemap, treeD; 
            switch (Gfx)
            {
                // v0.2.2: LOW / NORMAL short grass + view range, HIGH / VERY HIGH long view and full grass (the map is 70% of v0.2.1)
                case GfxPreset.Smooth: view = 750f; detailD = 16f; detailDens = 0.2f; basemap = 60f; treeD = 300f; break;
                case GfxPreset.Standard: view = 950f; detailD = 26f; detailDens = 0.35f; basemap = 120f; treeD = 450f; break;
                case GfxPreset.Ultra: view = 1300f; detailD = 80f; detailDens = 0.85f; basemap = 250f; treeD = 1000f; break;
                default: view = 2000f; detailD = 120f; detailDens = 1f; basemap = 400f; treeD = 1600f; break;
            }
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null) continue;
                t.detailObjectDistance = detailD; t.detailObjectDensity = detailDens; t.basemapDistance = 5000f; t.treeDistance = treeD; // v0.2.2: TerrainToon has no basemap shader, always the full (cheap) toon shader
                t.heightmapPixelError = Gfx == GfxPreset.Smooth ? 14f : Gfx == GfxPreset.Standard ? 10f : Gfx == GfxPreset.Ultra ? 5f : 3f;
            }
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.cullingMask == (1 << 5)) continue;   // UI cameras untouched
                var d = new float[32]; for (int i = 0; i < 32; i++) d[i] = 0;   // 0 = use far plane
                d[0] = view;                         // Default layer: props/houses/loot (the terrain's own layer is left alone below)
                foreach (var t in Terrain.activeTerrains) if (t != null) d[t.gameObject.layer] = 0;
                cam.layerCullDistances = d; cam.layerCullSpherical = true;
                if (cam.farClipPlane >= 300f) cam.farClipPlane = Mathf.Max(view, 750f) + 100f; // v0.2.2: the plane flies at 470 m, the far plane follows the tier
                cam.useOcclusionCulling = true;
                // no post-processing on LOW / NORMAL
                var cd = cam.GetComponent<UniversalAdditionalCameraData>(); if (cd != null) { cd.renderPostProcessing = Gfx >= GfxPreset.Ultra && cd.renderPostProcessing; if (Gfx <= GfxPreset.Standard) cd.renderShadows = false; else cd.renderShadows = true; }
            }
            try { Agni.Import.AgniAnime.ApplyTier((int)Gfx); } catch { }
        }
    }
}