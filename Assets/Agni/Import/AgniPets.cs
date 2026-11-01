// Agni Royale — IMPORT v1: lobby pets (tigers first). LOW/NORMAL show the cub, HIGH/VERY HIGH show the real adult tiger for the two tigers.
using UnityEngine;

namespace Agni.Net
{
    public static class AgniPets
    {
        public static readonly string[] Names = { "Bagha", "Bagha_White", "Sheru", "Gajju", "Mor" };
        public static readonly string[] Display = { "BAGHA (BENGAL TIGER)", "SAFED BAGHA (WHITE TIGER)", "SHERU (LION CUB)", "GAJJU (BABY ELEPHANT)", "MOR (PEACOCK)" };
        public static int Pref { get => Mathf.Clamp(PlayerPrefs.GetInt("agni.pet", 0), 0, Names.Length - 1); set { PlayerPrefs.SetInt("agni.pet", Mathf.Clamp(value, 0, Names.Length - 1)); PlayerPrefs.Save(); } }
        public static string RealOf(int i) => i == 0 ? "Bagha_Real" : i == 1 ? "White_Tiger_Real" : null;
        public static bool IsReal(GameObject g) => g != null && g.name.Contains("Real");
        /// the prefab for this graphics tier (real adult tiger at HIGH+ when one exists)
        public static GameObject Prefab(int i, int quality)
        {
            if (i < 0 || i >= Names.Length) return null; var real = quality >= 2 ? RealOf(i) : null;
            GameObject p = real != null ? Resources.Load<GameObject>("AgniImport/Pets/" + real) : null;
            return p != null ? p : Resources.Load<GameObject>("AgniImport/Pets/" + Names[i]);
        }
        /// clip names arrive as "<Name>_Rig|Sit": resolve by suffix
        public static string Resolve(Animation a, string clip)
        {
            if (a == null) return null; if (a[clip] != null) return clip;
            foreach (AnimationState st in a) if (st.name.EndsWith("|" + clip)) return st.name; return null;
        }
        public static void Play(GameObject pet, string clip, bool loop, float fade = 0.25f)
        {
            var a = pet != null ? pet.GetComponent<Animation>() : null; var c = Resolve(a, clip); if (c == null) return;
            a[c].wrapMode = loop ? WrapMode.Loop : WrapMode.Once; a.CrossFade(c, fade);
            if (!loop) { var back = Resolve(a, "Sit") ?? Resolve(a, "Idle"); if (back != null) { a[back].wrapMode = WrapMode.Loop; a.CrossFadeQueued(back, 0.3f, QueueMode.CompleteOthers); } }
        }
    }
}
