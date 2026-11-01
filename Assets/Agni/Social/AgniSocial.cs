// Agni Royale — friends + party data layer. Everything the UI touches goes through these interfaces, so the
// local/mock implementations here can be swapped for a real backend later without touching the lobby UI.
//   IFriendsService   — friend list, requests, add by player ID, presence           (MockFriendsService now)
//   IPartyService     — squad party: create/join by room code, invite, ready, start   (LanPartyService now: LAN / same Wi-Fi)
//   IOnlineBackend    — what an internet backend must provide (see design/BACKLOG.md "Needs from Sujon")
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Social
{
    public enum Presence { Offline = 0, Online = 1, InLobby = 2, InSquad = 3, InMatch = 4 }

    [Serializable]
    public class FriendInfo
    {
        public string playerId, name; public int level; public Presence presence; public long lastSeenUnix;
        public string rank = "KAANSA";
        public bool mock; // true = demo data from MockFriendsService (no backend yet)
    }

    [Serializable]
    public class FriendRequest { public string playerId, name; public int level; public bool incoming; public long sentUnix; }

    public interface IFriendsService
    {
        string MyPlayerId { get; }
        /// "DEMO" (local mock) or "ONLINE" (Unity Gaming Services)
        string Backend { get; }
        string LastError { get; }
        IReadOnlyList<FriendInfo> Friends { get; }
        IReadOnlyList<FriendRequest> Requests { get; }
        event Action Changed;
        /// returns null on success, else a Hinglish error for the UI
        string SendRequest(string playerId);
        void Accept(string playerId);
        void Decline(string playerId);
        void Remove(string playerId);
        /// a friend accepted our squad invite (mock: simulated) — the party layer listens to this
        event Action<FriendInfo> InviteAccepted;
        string InviteToSquad(string playerId);
        void Tick(float dt);
    }

    public class PartyMember { public string playerId, name; public bool ready, leader, isLocal, remoteFriend; public ulong clientId; }

    public interface IPartyService
    {
        bool InParty { get; }
        bool IsLeader { get; }
        string Code { get; }
        IReadOnlyList<PartyMember> Members { get; }
        string Status { get; }
        event Action Changed;
        void Create();
        void JoinByCode(string code);
        void Leave();
        void SetReady(bool ready);
        bool AllReady { get; }
        /// leader only: take the whole squad into a match
        void StartMatch();
        void Tick(float dt);
    }

    /// Internet backend contract (NOT implemented — needs a server/account from Sujon; see BACKLOG "Needs from Sujon").
    public interface IOnlineBackend
    {
        void SignIn(Action<bool, string> done);                          // Google / guest -> playerId + token
        void ResolveRoomCode(string code, Action<string, ushort> done);    // room code -> game server address
        void RequestMatch(string partyCode, int squadSize, Action<string, ushort, string> done); // matchmaking -> server ip, port, ticket
        void PushPresence(Presence p);
        void SendInvite(string toPlayerId, string partyCode);              // delivered as a push notification / in-game toast
    }

    public static class AgniSocial
    {
        const string KId = "agni.playerId";
        /// what friends type to add you: "Name#1234" when online (UGS), the local 10-digit demo ID otherwise
        public static string MyPlayerId => Friends.MyPlayerId;
        public static string LocalId
        {
            get
            {
                var id = PlayerPrefs.GetString(KId, "");
                if (string.IsNullOrEmpty(id))
                {
                    var r = new System.Random(Guid.NewGuid().GetHashCode());
                    id = (1 + r.Next(9)).ToString(); for (int i = 0; i < 9; i++) id += r.Next(10).ToString(); // 10 digits, never starts with 0
                    PlayerPrefs.SetString(KId, id); PlayerPrefs.Save();
                }
                return id;
            }
        }
        public static string Pretty(string id) => id != null && id.Length == 10 ? id.Substring(0, 3) + " " + id.Substring(3, 3) + " " + id.Substring(6) : id;
        public static string Digits(string s) { var o = ""; foreach (var c in s ?? "") if (char.IsDigit(c)) o += c; return o; }

        static IFriendsService friends; static IPartyService party;
        public static IFriendsService Friends => friends ??= new MockFriendsService();
        public static IPartyService Party => party ??= new LanPartyService();
        public static bool IsOnline => Friends.Backend == "ONLINE";
        /// swap the data layer (AgniOnline does this after a successful UGS sign-in; the mock/LAN stay as offline fallback)
        public static event Action ServicesChanged;
        public static void Use(IFriendsService f, IPartyService p) { if (f != null) friends = f; if (p != null) party = p; ServicesChanged?.Invoke(); }
        /// a friend invited us into their squad (online: UGS Friends message) — the lobby shows a JUDO popup
        public static event Action<string, string> InviteReceived;
        public static void RaiseInvite(string fromName, string code) { UnityEngine.Debug.Log("[AgniSocial] invite from " + fromName + " code " + code); InviteReceived?.Invoke(fromName, code); }

        // room codes: 6 chars, no 0/O/1/I confusion
        public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static string NewCode() { var r = new System.Random(Guid.NewGuid().GetHashCode()); var s = ""; for (int i = 0; i < 6; i++) s += CodeAlphabet[r.Next(CodeAlphabet.Length)]; return s; }
        public static string CleanCode(string s) { var o = ""; foreach (var c in (s ?? "").ToUpperInvariant()) if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) o += c; return o.Length > 6 ? o.Substring(0, 6) : o; } // LAN codes use CodeAlphabet; online (Lobby) codes are any A-Z/0-9
        public static string DeepLink(string code) => "agniroyale://join?code=" + code;
        public static string ShareText(string code) => "Agni Royale me meri squad join karo! Code: " + code + "\n" + DeepLink(code);
    }

    /// Local demo friends (PlayerPrefs). Presence changes slowly, requests you send are "accepted" after a few seconds,
    /// squad invites to online friends are accepted after ~3 s — so the whole UI flow can be tested without a server.
    public class MockFriendsService : IFriendsService
    {
        const string KFriends = "agni.mock.friends", KReq = "agni.mock.requests";
        [Serializable] class Store { public List<FriendInfo> f = new List<FriendInfo>(); public List<FriendRequest> r = new List<FriendRequest>(); }
        // demo directory: IDs you can add (+ the seeded friends)
        static readonly string[,] Directory = {
            { "4810275531", "RohitBhai07" }, { "5293018846", "Piya_XD" }, { "3378104419", "Kabir.op" }, { "6602917735", "ChotuDon" },
            { "7149920083", "Meher.ff" }, { "2283746610", "Arjun_Raja" }, { "8016653427", "Sana_Sniper" }, { "9124408852", "VeerBaba" },
            { "1357924680", "Toofan99" }, { "2468013579", "Laila.gg" }, { "3141592653", "Pinky_Pro" }, { "2718281828", "Bholu_Bhai" } };
        Store s; float t, nextPresence = 6;
        readonly List<(string id, float at)> pendingAccept = new List<(string, float)>();
        readonly List<(string id, float at)> pendingInvite = new List<(string, float)>();
        public event Action Changed; public event Action<FriendInfo> InviteAccepted;
        public string MyPlayerId => AgniSocial.LocalId;
        public string Backend => "DEMO"; public string LastError => null;
        public IReadOnlyList<FriendInfo> Friends { get { Load(); return s.f; } }
        public IReadOnlyList<FriendRequest> Requests { get { Load(); return s.r; } }

        void Load()
        {
            if (s != null) return;
            var j = PlayerPrefs.GetString(KFriends, "");
            if (!string.IsNullOrEmpty(j)) { try { s = JsonUtility.FromJson<Store>(j); } catch { s = null; } }
            if (s != null) foreach (var f in s.f) if (f.presence == Presence.InSquad) f.presence = Presence.Online; // a squad never survives a relaunch
            if (s == null)
            {
                s = new Store();
                Add(0, Presence.Online, 34); Add(1, Presence.InLobby, 21); Add(2, Presence.InMatch, 47); Add(3, Presence.Offline, 12); Add(4, Presence.Online, 29);
                s.r.Add(new FriendRequest { playerId = Directory[5, 0], name = Directory[5, 1], level = 18, incoming = true, sentUnix = Now() - 3600 });
                s.r.Add(new FriendRequest { playerId = Directory[6, 0], name = Directory[6, 1], level = 40, incoming = true, sentUnix = Now() - 600 });
                Save();
            }
        }
        void Add(int i, Presence p, int lvl) => s.f.Add(new FriendInfo { playerId = Directory[i, 0], name = Directory[i, 1], level = lvl, presence = p, lastSeenUnix = Now() - (p == Presence.Offline ? 7200 : 0), mock = true });
        static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        void Save() { PlayerPrefs.SetString(KFriends, JsonUtility.ToJson(s)); PlayerPrefs.Save(); Changed?.Invoke(); }
        string NameOf(string id) { for (int i = 0; i < Directory.GetLength(0); i++) if (Directory[i, 0] == id) return Directory[i, 1]; return null; }

        public string SendRequest(string raw)
        {
            Load(); var id = AgniSocial.Digits(raw);
            if (id.Length != 10) return "Player ID 10 number ka hota hai";
            if (id == MyPlayerId) return "Ye to tumhara apna ID hai 🙂";
            if (s.f.Exists(f => f.playerId == id)) return "Pehle se dost hai";
            if (s.r.Exists(r => r.playerId == id && !r.incoming)) return "Request pehle hi bhej di";
            var inc = s.r.Find(r => r.playerId == id && r.incoming); if (inc != null) { Accept(id); return null; }
            var nm = NameOf(id); if (nm == null) return "Is ID ka koi player nahi mila";
            s.r.Add(new FriendRequest { playerId = id, name = nm, level = 10 + (int)(long.Parse(id) % 50), incoming = false, sentUnix = Now() });
            pendingAccept.Add((id, t + 4f)); Save(); return null;
        }
        public void Accept(string id)
        {
            Load(); var r = s.r.Find(x => x.playerId == id); if (r == null) return; s.r.Remove(r);
            if (!s.f.Exists(f => f.playerId == id)) s.f.Add(new FriendInfo { playerId = id, name = r.name, level = r.level, presence = Presence.Online, lastSeenUnix = Now(), mock = true });
            Save();
        }
        public void Decline(string id) { Load(); s.r.RemoveAll(x => x.playerId == id); Save(); }
        public void Remove(string id) { Load(); s.f.RemoveAll(x => x.playerId == id); Save(); }
        public string InviteToSquad(string id)
        {
            Load(); var f = s.f.Find(x => x.playerId == id); if (f == null) return "Dost nahi mila";
            if (f.presence == Presence.Offline) return f.name + " abhi offline hai";
            if (f.presence == Presence.InMatch) return f.name + " match me hai";
            if (pendingInvite.Exists(p => p.id == id)) return "Invite bhej diya — ruko";
            pendingInvite.Add((id, t + 3f)); return null;
        }
        public void Tick(float dt)
        {
            Load(); t += dt;
            for (int i = pendingAccept.Count - 1; i >= 0; i--) if (t >= pendingAccept[i].at)
                {
                    var id = pendingAccept[i].id; pendingAccept.RemoveAt(i); var r = s.r.Find(x => x.playerId == id && !x.incoming);
                    if (r != null) { s.r.Remove(r); s.f.Add(new FriendInfo { playerId = id, name = r.name, level = r.level, presence = Presence.Online, lastSeenUnix = Now(), mock = true }); Save(); }
                }
            for (int i = pendingInvite.Count - 1; i >= 0; i--) if (t >= pendingInvite[i].at)
                {
                    var id = pendingInvite[i].id; pendingInvite.RemoveAt(i); var f = s.f.Find(x => x.playerId == id);
                    if (f != null && f.presence != Presence.Offline) { f.presence = Presence.InSquad; Save(); InviteAccepted?.Invoke(f); }
                }
            if (t > nextPresence)
            {   // gentle presence drift so the list feels alive
                nextPresence = t + 25f; var rnd = new System.Random((int)(t * 1000));
                var f = s.f.Count > 0 ? s.f[rnd.Next(s.f.Count)] : null;
                if (f != null && f.presence != Presence.InSquad) { f.presence = (Presence)new[] { 0, 1, 2, 4 }[rnd.Next(4)]; if (f.presence == Presence.Offline) f.lastSeenUnix = Now(); Save(); }
            }
        }
        public static string PresenceText(FriendInfo f)
        {
            switch (f.presence)
            {
                case Presence.Online: return "Online";
                case Presence.InLobby: return "Lobby me";
                case Presence.InSquad: return "Tumhari squad me";
                case Presence.InMatch: return "Match me";
                default:
                    long m = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - f.lastSeenUnix) / 60;
                    return m < 60 ? "Offline · " + Mathf.Max(1, m) + " min pehle" : "Offline · " + (m / 60) + " ghante pehle";
            }
        }
    }
}
