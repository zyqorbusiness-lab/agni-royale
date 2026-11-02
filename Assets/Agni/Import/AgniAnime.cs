// Agni Royale v0.2.2 — the anime map look at runtime: painted anime sky, bright natural ambient + fog that match it, a warm sun,
// and the per-tier switches (the light outline on houses / props / landmarks is HIGH and VERY HIGH only).
// The materials themselves were moved onto the anime shaders in the editor (Editor/AgniAnimeSetup.cs).
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import
{
    public static class AgniAnime
    {
        public static readonly Color Horizon = new Color(0.80f, 0.89f, 0.97f);
        /// called by AgniArenaBootstrap when the match scene wakes (clients and host; headless strips it again)
        public static void ApplyLook()
        {
            var sky = Resources.Load<Material>("AgniImport/Sky/M_Sky_Anime"); if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.70f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.84f, 0.90f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.56f, 0.44f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = Horizon; RenderSettings.fogDensity = 0.00085f;
            Light sun = RenderSettings.sun;
            if (sun == null) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) { sun.color = new Color(1f, 0.95f, 0.86f); sun.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(58f, 205f, 0f); sun.shadowStrength = 0.85f; RenderSettings.sun = sun; } // the painted sky's sun sits at azimuth 25, elevation 58
            DynamicGI.UpdateEnvironment();
            ApplyTier(Agni.UI.AgniMahal.Quality);
        }

        static int lastTier = -1;
        /// outline passes on only for HIGH / VERY HIGH (they double the draw calls of every house)
        public static void ApplyTier(int q)
        {
            bool outline = q >= 2; if (q == lastTier && !Application.isEditor) return; lastTier = q;
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null) continue; var sn = m.shader.name;
                if (sn == "AgniRoyale/WorldToon" || sn == "AgniRoyale/Landmark") m.SetShaderPassEnabled("SRPDefaultUnlit", outline);
            }
        }
    }
}
