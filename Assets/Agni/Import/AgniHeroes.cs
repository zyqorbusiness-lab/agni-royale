// Agni Royale — IMPORT v1: the 10 heroes with hair v3 (characters_hair_v3/<Name>/<Name>_HairV3.fbx, read-only source) + the AgniFace rig.
// Prefabs are built by Agni.Import.EditorTools.AgniImportHeroes into Resources/AgniImport/Heroes/<Name>.prefab (toon materials, LOD0-2, AgniFace).
// Every fighter carries AgniPlayer.Hero (index into Names); the phone's pick (PlayerPrefs agni.hero) is sent on spawn, bots get a random hero.
using UnityEngine;

namespace Agni.Net
{
    public static class AgniHeroes
    {
        public static readonly string[] Names = { "Meera", "Laila", "Zoya", "Rani", "Arjun", "Kabir", "DJ_Taal", "Dr_Sameer", "Chotu", "Bheem" };
        public static readonly string[] Display = { "Meera", "Laila", "Zoya", "Rani", "Arjun", "Kabir", "DJ Taal", "Dr Sameer", "Chotu", "Bheem" };
        /// one uniform scale for all heroes (Meera v3 is 1.89 m tall in the FBX, the fighter capsule is built for ~1.72 m): men stay taller than women
        public const float MatchScale = 0.91f;
        static readonly GameObject[] cache = new GameObject[Names.Length]; static readonly bool[] tried = new bool[Names.Length];
        public static GameObject Prefab(int i)
        {
            if (i < 0 || i >= Names.Length) return null;
            if (!tried[i]) { tried[i] = true; cache[i] = Resources.Load<GameObject>("AgniImport/Heroes/" + Names[i]); }
            return cache[i];
        }
        public static int Pref { get => Mathf.Clamp(PlayerPrefs.GetInt("agni.hero", 0), 0, Names.Length - 1); set { PlayerPrefs.SetInt("agni.hero", Mathf.Clamp(value, 0, Names.Length - 1)); PlayerPrefs.Save(); } }
        /// in a match: LOW = LOD2 only and no face; NORMAL = face on LOD0 only (close range); HIGH+ = full face. The LODGroup does the rest.
        public static void SetupInMatch(GameObject ch, int quality, bool local)
        {
            var lg = ch.GetComponentInChildren<LODGroup>(); var face = ch.GetComponentInChildren<AgniFace>();
            if (quality <= 0) { if (lg != null) lg.ForceLOD(2); if (face != null) face.enabled = false; }
            else if (face != null) face.tier = quality >= 2 ? AgniFace.Tier.High : AgniFace.Tier.Normal;
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = r.name.EndsWith("_LOD2") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    public partial class AgniPlayer
    {
        public Unity.Netcode.NetworkVariable<byte> Hero = new Unity.Netcode.NetworkVariable<byte>();
        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Server)] public void HeroRpc(byte hero, Unity.Netcode.RpcParams p = default) { if (Own(p) && hero < AgniHeroes.Names.Length) Hero.Value = hero; }
        void HeroOnSpawn()
        {
            if (IsServer && IsBot.Value) Hero.Value = (byte)((NetworkObjectId * 2654435761UL >> 7) % (ulong)AgniHeroes.Names.Length);
            if (IsOwner && !IsBot.Value) HeroRpc((byte)AgniHeroes.Pref);
        }
    }
}
