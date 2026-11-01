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
        public static GfxPreset Gfx { get => (GfxPreset)PlayerPrefs.GetInt(KGfx, (int)GfxPreset.Standard); set { PlayerPrefs.SetInt(KGfx, (int)value); Save(); } }
        public static int Fps { get => PlayerPrefs.GetInt(KFps, 60); set { PlayerPrefs.SetInt(KFps, value); Save(); } }
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
                switch (Gfx)
                {
                    case GfxPreset.Smooth: urp.renderScale = 0.75f; urp.msaaSampleCount = 1; urp.shadowDistance = 0f; break;
                    case GfxPreset.Standard: urp.renderScale = 1.0f; urp.msaaSampleCount = 1; urp.shadowDistance = 40f; break;
                    case GfxPreset.Ultra: urp.renderScale = 1.0f; urp.msaaSampleCount = 2; urp.shadowDistance = 60f; break;
                    case GfxPreset.VeryHigh: urp.renderScale = 1.0f; urp.msaaSampleCount = 4; urp.shadowDistance = 90f; break;
                }
            }
            // Sound: SFX sources read AgniSettings.Sfx, music sources read AgniSettings.Music (see AgniLobby.music).
        }
    }
}
