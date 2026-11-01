// Agni Royale — IMPORT v1: emotes v2 (emotes/<Name>/Emote_<Name>.fbx, 30 fps, in place) as legacy clips on the heroes' Animation component,
// with the matching AgniFace emote face track. Networked: AgniPlayer.Emote (0 = none, else index+1) + EmoteAt; moving, shooting, a vehicle or a knock cancels it.
using UnityEngine;

namespace Agni.Net
{
    public static class AgniEmotes
    {
        public static readonly string[] Names = { "Ankhon_Ka_Ishara", "Baal_Jhatka", "Bhangra_Beat", "Chandni_Waltz", "Chest_Thump_Flex", "Clap_Cheer", "Dil_Ka_Jhoola", "Dil_Nasha", "Disco_King", "Diva_Pose", "Dupatta_Leher", "Flying_Kiss", "Garba_Spin", "Golden_Pose", "Gulabi_Ada", "Heart_Hands", "Heart_Sway", "Jhatka_Shake", "Mayuri", "Mehfil_Mujra", "Naagin", "Nakhra_Catwalk", "Namaste_Bow", "Raat_Ki_Rani", "Robot_Pop", "Saath_Naach", "Salsa_Step", "Sharmili", "Sitara_Spin", "Slow_Sway", "Thumka", "Victory_Jump", "Wave_Hello" };
        static readonly System.Collections.Generic.HashSet<string> once = new System.Collections.Generic.HashSet<string> { "Ankhon_Ka_Ishara", "Chest_Thump_Flex", "Dil_Ka_Jhoola", "Flying_Kiss", "Mehfil_Mujra", "Namaste_Bow", "Sitara_Spin", "Victory_Jump", "Wave_Hello" };
        public static bool Loops(string n) => !once.Contains(n);
        public static string Display(int i) => i >= 0 && i < Names.Length ? Names[i].Replace('_', ' ').ToUpper() : "";
        static readonly AnimationClip[] cache = new AnimationClip[Names.Length]; static readonly bool[] tried = new bool[Names.Length];
        public static AnimationClip Clip(int i)
        {
            if (i < 0 || i >= Names.Length) return null;
            if (!tried[i]) { tried[i] = true; foreach (var c in Resources.LoadAll<AnimationClip>("AgniImport/Emotes/Emote_" + Names[i])) if (!c.name.StartsWith("__preview__")) { cache[i] = c; break; } }
            return cache[i];
        }
        public static int Pref { get => Mathf.Clamp(PlayerPrefs.GetInt("agni.emote", 0), 0, Names.Length - 1); set { PlayerPrefs.SetInt("agni.emote", Mathf.Clamp(value, 0, Names.Length - 1)); PlayerPrefs.Save(); } }
        /// play emote i on an Animation component (adds the clip once); returns the state name or null
        public static string Play(Animation anim, int i, float fade = 0.2f)
        {
            var c = Clip(i); if (anim == null || c == null) return null; string st = "E_" + Names[i];
            if (anim[st] == null) anim.AddClip(c, st);
            var s = anim[st]; s.layer = 0; s.wrapMode = Loops(Names[i]) ? WrapMode.Loop : WrapMode.ClampForever; s.speed = 1;
            anim.CrossFade(st, fade); return st;
        }
    }

    public partial class AgniPlayer
    {
        public Unity.Netcode.NetworkVariable<byte> Emote = new Unity.Netcode.NetworkVariable<byte>();
        public Unity.Netcode.NetworkVariable<double> EmoteAt = new Unity.Netcode.NetworkVariable<double>();
        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Server)] public void EmoteRpc(byte e, Unity.Netcode.RpcParams p = default) { if (Own(p)) ServerEmote(e); }
        public void ServerEmote(byte e)
        {
            if (!IsServer) return;
            if (e == 0 || e > AgniEmotes.Names.Length || !Alive || InVehicle || Healing.Value) { Emote.Value = 0; return; }
            Emote.Value = e; EmoteAt.Value = NetworkManager.ServerTime.Time;
        }
        /// runs every server tick (from ServerRide): moving, firing, jumping into a vehicle or a knock ends the emote; one-shot emotes end with their clip
        void ServerEmoteTick()
        {
            if (Emote.Value == 0) return;
            bool cancel = !Alive || InVehicle || input.move.sqrMagnitude > 0.04f || input.Has(AgniInput.Fire) || Healing.Value;
            int i = Emote.Value - 1; var c = AgniEmotes.Clip(i);
            if (!cancel && c != null && !AgniEmotes.Loops(AgniEmotes.Names[i]) && NetworkManager.ServerTime.Time - EmoteAt.Value > c.length + 0.3) cancel = true;
            if (cancel) Emote.Value = 0;
        }
    }
}
