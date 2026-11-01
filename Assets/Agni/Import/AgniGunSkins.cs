// Agni Royale — IMPORT v1: the Blender gun skins (weapons/<GUN>/skins_premium.json -> Resources/AgniImport/Guns/<GUN>/skins.json).
// Skin code per gun: 0 = the new Blender base finish, 1..n = premium skins in the json order (Rare, Epic, Legendary, Mythic Lv1-3).
// Every fighter carries one byte per gun in AgniPlayer.GunSkins (8 bytes in a ulong) so everyone sees the same skin in their hands.
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Net
{
    public static class AgniGunSkins
    {
        public static readonly string[] Guns = { "AGNI_47", "BIJLI_SMG", "SHIKARI_98", "DHAMAKA_12", "BHARI_LMG", "CHOTA" };
        [System.Serializable] public class Tex { public string Albedo, MetallicSmoothness, Emission; }
        [System.Serializable] public class Skin { public string id, name, tier, emission_tint, flow_texture, attachment_fbx, muzzle_flash, kill_effect; public int evo_level; public Tex textures; public float emission_intensity; public float[] uv_scroll; public string[] attachments; }
        [System.Serializable] class File { public string gun; public Skin[] premium_skins; }
        static readonly Dictionary<string, Skin[]> files = new Dictionary<string, Skin[]>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public static int GunIndex(string model) { for (int i = 0; i < Guns.Length; i++) if (Guns[i] == model) return i; return -1; }
        public static Skin[] Of(string gun)
        {
            if (!files.TryGetValue(gun, out var s)) { var t = Resources.Load<TextAsset>("AgniImport/Guns/" + gun + "/skins"); s = t != null ? JsonUtility.FromJson<File>(t.text).premium_skins ?? new Skin[0] : new Skin[0]; files[gun] = s; }
            return s;
        }
        public static int Count(string gun) => Of(gun).Length + 1;
        public static Skin Get(string gun, int code) { var s = Of(gun); return code >= 1 && code <= s.Length ? s[code - 1] : null; }
        public static string Name(string gun, int code) { var s = Get(gun, code); return s != null ? s.name : "Standard"; }
        public static string Tier(string gun, int code) { var s = Get(gun, code); return s != null ? s.tier : "Common"; }
        static Texture2D T(string gun, string path) { if (string.IsNullOrEmpty(path)) return null; var n = System.IO.Path.GetFileNameWithoutExtension(path); return Resources.Load<Texture2D>("AgniImport/Guns/" + gun + "/" + n); }
        /// one shared material per gun+skin (null when the Blender textures are missing -> caller keeps its old look)
        public static Material Mat(string gun, int code)
        {
            var key = gun + "#" + code; if (mats.TryGetValue(key, out var m) && m != null) return m;
            var sk = Get(gun, code);
            var alb = sk != null ? T(gun, sk.textures.Albedo) : Resources.Load<Texture2D>("AgniImport/Guns/" + gun + "/T_" + gun + "_Base_Albedo");
            if (alb == null) return null;
            var ms = sk != null ? T(gun, sk.textures.MetallicSmoothness) : Resources.Load<Texture2D>("AgniImport/Guns/" + gun + "/T_" + gun + "_Base_MetallicSmoothness");
            var nrm = Resources.Load<Texture2D>("AgniImport/Guns/" + gun + "/T_" + gun + "_Normal");
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + gun + "_" + (sk != null ? sk.id : "Base") };
            m.SetTexture("_BaseMap", alb); m.SetColor("_BaseColor", Color.white);
            if (ms != null) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0); }
            else { m.SetFloat("_Metallic", 0.4f); m.SetFloat("_Smoothness", 0.45f); }
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            var em = sk != null ? T(gun, sk.textures.Emission) : null;
            if (em != null && sk.emission_intensity > 0)
            {
                var tint = Color.white; if (!string.IsNullOrEmpty(sk.emission_tint)) ColorUtility.TryParseHtmlString(sk.emission_tint, out tint);
                m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", tint * Mathf.Min(sk.emission_intensity, 3f)); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            mats[key] = m; return m;
        }
        /// Mythic evolution parts (Wings at Lv2, Wings + Head at Lv3): the skin's attachment FBX parented at the gun root (grip origin)
        public static void AddAttachments(GameObject gunGo, string gun, int code, Material mat)
        {
            var sk = Get(gun, code); if (sk == null || sk.attachments == null || sk.attachments.Length == 0 || string.IsNullOrEmpty(sk.attachment_fbx)) return;
            var src = Resources.Load<GameObject>("AgniImport/Guns/" + gun + "/" + System.IO.Path.GetFileNameWithoutExtension(sk.attachment_fbx)); if (src == null) return;
            var a = Object.Instantiate(src, gunGo.transform); a.name = "Evo"; a.transform.localPosition = Vector3.zero; a.transform.localRotation = Quaternion.identity; a.transform.localScale = Vector3.one;
            var lg = a.GetComponent<LODGroup>(); if (lg != null) Object.Destroy(lg);
            foreach (var r in a.GetComponentsInChildren<Renderer>(true))
            {
                bool want = false; foreach (var part in sk.attachments) if (r.name.Contains(part)) want = true;
                bool l0 = !r.name.Contains("LOD1") && !r.name.Contains("LOD2");
                r.enabled = want && l0; if (mat != null) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
            }
        }
        // ---- per gun byte packing
        public static int Unpack(ulong packed, string gun) { int i = GunIndex(gun); return i < 0 ? 0 : (int)((packed >> (i * 8)) & 0xFF); }
        public static ulong Pack(System.Func<int, int> codeOf) { ulong v = 0; for (int i = 0; i < Guns.Length; i++) v |= (ulong)(Mathf.Clamp(codeOf(i), 0, 255) & 0xFF) << (i * 8); return v; }
        /// this phone's picks (armoury UI writes these; default = Standard)
        public static int Pref(string gun) => PlayerPrefs.GetInt("agni.gskin." + gun, 0);
        public static void SetPref(string gun, int code) { PlayerPrefs.SetInt("agni.gskin." + gun, code); PlayerPrefs.Save(); }
        public static ulong PackPrefs() => Pack(i => Mathf.Clamp(Pref(Guns[i]), 0, Count(Guns[i]) - 1));
        public static ulong Random(System.Random rng) => Pack(i => rng.NextDouble() < 0.45 ? 0 : rng.Next(1, Count(Guns[i])));
    }

    public partial class AgniPlayer
    {
        public Unity.Netcode.NetworkVariable<ulong> GunSkins = new Unity.Netcode.NetworkVariable<ulong>();
        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Server)] public void GunSkinsRpc(ulong packed, Unity.Netcode.RpcParams p = default) { if (Own(p)) GunSkins.Value = packed; }
        /// called from OnNetworkSpawn: owners send their picks, the server dresses its bots
        void SkinsOnSpawn()
        {
            if (IsServer && IsBot.Value) GunSkins.Value = AgniGunSkins.Random(new System.Random((int)NetworkObjectId * 7919 + 13));
            if (IsOwner && !IsBot.Value) GunSkinsRpc(AgniGunSkins.PackPrefs());
        }
    }
}
