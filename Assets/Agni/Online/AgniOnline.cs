// Agni Royale — REAL online layer on Unity Gaming Services (free tier):
//   Authentication (anonymous now, Google later) · Friends (add by Name#1234, requests, presence, invite messages)
//   · Multiplayer Sessions = Lobby (6-char join code) + Relay (phones on different internet, no LAN / no port forwarding) → NGO.
// The only missing piece is the Unity Cloud Project ID (Sujon's Unity account): put it in
// Assets/Agni/Online/Resources/AgniOnline/AgniOnlineConfig.json and run  Agni Royale ▸ Online ▸ Link Cloud Project
// (or link the project in Edit ▸ Project Settings ▸ Services). Without it everything falls back to the demo friends + LAN party.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agni.Net;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using Unity.Services.Multiplayer;
using UnityEngine;
using FPresence = Unity.Services.Friends.Models.Presence;

namespace Agni.Social
{
    [Serializable]
    public class AgniOnlineConfig
    {
        public string cloudProjectId = "";      // from cloud.unity.com (Sujon's org) — the single value to fill in
        public string environment = "production";
        public bool online = true;              // false = always demo/LAN
        public string relayRegion = "";         // "" = auto (QoS picks the closest, e.g. asia-south1 Mumbai)
        static AgniOnlineConfig cached;
        public static AgniOnlineConfig Load()
        {
            if (cached != null) return cached;
            var ta = Resources.Load<TextAsset>("AgniOnline/AgniOnlineConfig");
            try { cached = ta != null ? JsonUtility.FromJson<AgniOnlineConfig>(ta.text) : new AgniOnlineConfig(); } catch { cached = new AgniOnlineConfig(); }
            return cached;
        }
        /// project id the build actually carries (the Services link writes it into ProjectSettings)
        public static string LinkedProjectId => Application.cloudProjectId;
    }

    public enum OnlineState { Off, NoProject, SigningIn, Online, Failed }

    public static class AgniOnline
    {
        public static OnlineState State { get; private set; } = OnlineState.Off;
        public static string Error { get; private set; } = "";
        public static string PlayerId => State == OnlineState.Online ? AuthenticationService.Instance.PlayerId : "";
        /// what a friend types to add you, e.g. "AgniVeer#4821"
        public static string FriendCode { get; private set; } = "";
        public static event Action StateChanged;
        static Task running;

        public static string StatusText
        {
            get
            {
                switch (State)
                {
                    case OnlineState.Online: return "ONLINE";
                    case OnlineState.SigningIn: return "Jud rahe hain…";
                    case OnlineState.NoProject: return "OFFLINE DEMO (Cloud Project ID nahi hai)";
                    case OnlineState.Failed: return "OFFLINE (" + Error + ")";
                    default: return "OFFLINE";
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (AgniNetConfig.HeadlessServer || AgniNetConfig.Has("-agniOffline")) return;
            _ = EnsureSignedIn();
        }

        public static Task EnsureSignedIn() { if (running == null || running.IsCompleted && State != OnlineState.Online && State != OnlineState.NoProject) running = SignIn(); return running; }

        static void Set(OnlineState s, string err = "") { State = s; Error = err; Debug.Log("[AgniOnline] " + s + (string.IsNullOrEmpty(err) ? "" : " — " + err)); StateChanged?.Invoke(); }

        static async Task SignIn()
        {
            var cfg = AgniOnlineConfig.Load();
            if (!cfg.online) { Set(OnlineState.Off); return; }
            if (string.IsNullOrEmpty(AgniOnlineConfig.LinkedProjectId)) { Set(OnlineState.NoProject); return; }
            Set(OnlineState.SigningIn);
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var opt = new InitializationOptions();
                    if (!string.IsNullOrEmpty(cfg.environment)) opt.SetEnvironmentName(cfg.environment);
                    var profile = AgniNetConfig.Arg("-agniProfile"); // lets two editor/desktop copies on one PC be two different players
                    if (!string.IsNullOrEmpty(profile)) opt.SetProfile(profile);
                    await UnityServices.InitializeAsync(opt);
                }
                var auth = AuthenticationService.Instance;
                if (!auth.IsSignedIn) await auth.SignInAnonymouslyAsync(); // session token is cached: same player id next launch
                // player name = lobby name (UGS adds the #1234 discriminator); spaces are not allowed
                var want = (Agni.UI.AgniProfile.HasName ? Agni.UI.AgniProfile.Name : "AgniVeer").Replace(" ", "_");
                string name = auth.PlayerName;
                if (string.IsNullOrEmpty(name)) name = await auth.GetPlayerNameAsync(false);
                if (string.IsNullOrEmpty(name) || !name.StartsWith(want + "#")) { try { name = await auth.UpdatePlayerNameAsync(want); } catch (Exception e) { Debug.LogWarning("[AgniOnline] name: " + e.Message); if (string.IsNullOrEmpty(name)) name = await auth.GetPlayerNameAsync(true); } }
                FriendCode = name ?? "";
                var friends = new UgsFriendsService(); await friends.Init();
                AgniSocial.Use(friends, new UgsPartyService());
                Set(OnlineState.Online);
            }
            catch (Exception e) { Set(OnlineState.Failed, Short(e)); }
        }

        public static string Short(Exception e)
        {
            var m = e.InnerException != null ? e.InnerException.Message : e.Message; if (string.IsNullOrEmpty(m)) m = e.GetType().Name;
            return m.Length > 70 ? m.Substring(0, 70) + "…" : m;
        }
    }

    // ------------------------------------------------------------------ Friends (UGS)
    [Serializable] public class AgniActivity { public string status = "lobby"; public string code = ""; }            // presence payload
    [Serializable] public class AgniInviteMsg { public string kind = "squad"; public string code = ""; public string from = ""; } // friend message

    public class UgsFriendsService : IFriendsService
    {
        readonly List<FriendInfo> friends = new List<FriendInfo>(); readonly List<FriendRequest> requests = new List<FriendRequest>();
        public event Action Changed; public event Action<FriendInfo> InviteAccepted;
        public string MyPlayerId => AgniOnline.FriendCode;
        public string Backend => "ONLINE";
        public string LastError { get; private set; }
        public IReadOnlyList<FriendInfo> Friends => friends;
        public IReadOnlyList<FriendRequest> Requests => requests;
        string myStatus = "lobby", myCode = ""; float presenceAt;

        public async Task Init()
        {
            await FriendsService.Instance.InitializeAsync(new Unity.Services.Friends.Options.InitializeOptions().WithMemberPresence(true).WithMemberProfile(true));
            var fs = FriendsService.Instance;
            fs.RelationshipAdded += _ => Rebuild(); fs.RelationshipDeleted += _ => Rebuild(); fs.PresenceUpdated += _ => Rebuild();
            fs.MessageReceived += OnMessage;
            await fs.SetPresenceAsync(Availability.Online, new AgniActivity { status = "lobby" });
            Rebuild();
        }

        void OnMessage(IMessageReceivedEvent e)
        {
            try
            {
                var msg = e.GetAs<AgniInviteMsg>(); if (msg == null || msg.kind != "squad" || string.IsNullOrEmpty(msg.code)) return;
                var from = msg.from; foreach (var f in friends) if (f.playerId == e.UserId) from = f.name;
                AgniSocial.RaiseInvite(string.IsNullOrEmpty(from) ? "Dost" : from, msg.code);
            }
            catch (Exception ex) { Debug.LogWarning("[AgniOnline] bad message: " + ex.Message); }
        }

        static Presence Map(FPresence p)
        {
            if (p == null) return Presence.Offline;
            switch (p.Availability)
            {
                case Availability.Online: case Availability.Busy: case Availability.Away:
                    AgniActivity a = null; try { a = p.GetActivity<AgniActivity>(); } catch { }
                    if (a == null) return Presence.Online;
                    return a.status == "match" ? Presence.InMatch : a.status == "squad" ? Presence.InSquad : a.status == "lobby" ? Presence.InLobby : Presence.Online;
                default: return Presence.Offline;
            }
        }
        static string Nm(Member m) => m?.Profile?.Name ?? (m != null && m.Id.Length > 6 ? "Player-" + m.Id.Substring(0, 6) : "Player");

        void Rebuild()
        {
            var fs = FriendsService.Instance; friends.Clear(); requests.Clear();
            foreach (var r in fs.Friends)
            {
                var p = r.Member.Presence; long seen = 0; if (p != null) seen = new DateTimeOffset(DateTime.SpecifyKind(p.LastSeen, DateTimeKind.Utc)).ToUnixTimeSeconds();
                friends.Add(new FriendInfo { playerId = r.Member.Id, name = Nm(r.Member), level = 1, presence = Map(p), lastSeenUnix = seen, mock = false });
            }
            foreach (var r in fs.IncomingFriendRequests) requests.Add(new FriendRequest { playerId = r.Member.Id, name = Nm(r.Member), incoming = true });
            foreach (var r in fs.OutgoingFriendRequests) requests.Add(new FriendRequest { playerId = r.Member.Id, name = Nm(r.Member), incoming = false });
            Changed?.Invoke();
        }

        async void Run(Func<Task> a, string what)
        {
            try { LastError = null; await a(); }
            catch (Exception e) { LastError = what + ": " + AgniOnline.Short(e); Debug.LogWarning("[AgniOnline] " + LastError); }
            Rebuild();
        }

        /// accepts "Name#1234" (what the panel shows) or a raw UGS player id
        public string SendRequest(string raw)
        {
            raw = (raw ?? "").Trim(); if (raw.Length < 3) return "ID daalo (jaise Rahul#1234)";
            if (raw == MyPlayerId) return "Ye to tumhara apna ID hai";
            if (raw.Contains("#")) Run(() => FriendsService.Instance.AddFriendByNameAsync(raw), "Request");
            else Run(() => FriendsService.Instance.AddFriendAsync(raw), "Request");
            return null;
        }
        public void Accept(string id) => Run(() => FriendsService.Instance.AddFriendAsync(id), "Accept");
        public void Decline(string id) => Run(() => FriendsService.Instance.DeleteIncomingFriendRequestAsync(id), "Decline");
        public void Remove(string id) => Run(() => FriendsService.Instance.DeleteFriendAsync(id), "Remove");
        public string InviteToSquad(string id)
        {
            var f = friends.Find(x => x.playerId == id); if (f == null) return "Dost nahi mila";
            if (f.presence == Presence.Offline) return f.name + " abhi offline hai";
            var party = AgniSocial.Party;
            if (!party.InParty) { party.Create(); pendingInvites.Add(id); return null; } // invite goes out once the room code exists
            Send(id, party.Code); return null;
        }
        readonly List<string> pendingInvites = new List<string>();
        void Send(string id, string code) => Run(() => FriendsService.Instance.MessageAsync(id, new AgniInviteMsg { code = code, from = MyPlayerId }), "Invite");

        public void Tick(float dt)
        {
            var party = AgniSocial.Party;
            if (pendingInvites.Count > 0 && party.InParty && !string.IsNullOrEmpty(party.Code)) { foreach (var id in pendingInvites) Send(id, party.Code); pendingInvites.Clear(); }
            // keep our presence in step with what we are doing (lobby / squad / match), at most every 3 s
            presenceAt -= dt; if (presenceAt > 0) return; presenceAt = 3f;
            string st = AgniNet.MatchInProgress || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == AgniNetConfig.MatchScene ? "match" : party.InParty ? "squad" : "lobby";
            string code = party.InParty ? party.Code : "";
            if (st != myStatus || code != myCode) { myStatus = st; myCode = code; Run(() => FriendsService.Instance.SetPresenceAsync(Availability.Online, new AgniActivity { status = st, code = code }), "Presence"); }
        }
    }

    // ------------------------------------------------------------------ Squad room (UGS Lobby + Relay via Multiplayer Sessions)
    public class UgsPartyService : IPartyService
    {
        ISession session; string status = ""; bool busy; readonly List<PartyMember> members = new List<PartyMember>();
        public event Action Changed;
        public bool InParty => session != null && session.State == SessionState.Connected;
        public bool IsLeader => session != null && session.IsHost;
        public string Code => session != null ? session.Code ?? "" : "";
        public string Status => status;
        public IReadOnlyList<PartyMember> Members => members;
        public bool AllReady { get { foreach (var m in members) if (!m.ready && !m.leader) return false; return true; } }
        const string KReady = "ready", KName = "nick";

        Dictionary<string, PlayerProperty> MyProps(bool ready) => new Dictionary<string, PlayerProperty> {
            { KName, new PlayerProperty(AgniProfileName(), VisibilityPropertyOptions.Member) },
            { KReady, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member) } };
        static string AgniProfileName() => Agni.UI.AgniProfile.HasName ? Agni.UI.AgniProfile.Name : "AgniVeer";

        public async void Create()
        {
            if (busy || InParty) return; busy = true; status = "Online room ban raha hai…"; Changed?.Invoke();
            try
            {
                var cfg = AgniOnlineConfig.Load();
                var opt = new SessionOptions { MaxPlayers = AgniNetConfig.SquadSize, IsPrivate = true, Name = "agni-" + Guid.NewGuid().ToString("N").Substring(0, 8), PlayerProperties = MyProps(false) }
                    .WithRelayNetwork(string.IsNullOrEmpty(cfg.relayRegion) ? null : cfg.relayRegion).WithNetworkHandler(new AgniRelayHandler());
                session = await MultiplayerService.Instance.CreateSessionAsync(opt);
                Hook(); status = "Online squad ready — code " + session.Code + " bhejo";
                SpawnParty();
            }
            catch (Exception e) { status = "Room nahi bana: " + AgniOnline.Short(e); session = null; }
            busy = false; Rebuild();
        }
        void SpawnParty()
        {   // reuse the LAN party NetworkObject so the match code (squad grouping, mock friends) is identical online
            if (!AgniNet.IsServer || AgniPartyNet.Current != null) return;
            var prefab = Resources.Load<GameObject>("AgniNet/AgniParty"); if (prefab == null) return;
            var go = UnityEngine.Object.Instantiate(prefab); var p = go.GetComponent<AgniPartyNet>(); p.Code.Value = session.Code ?? "";
            go.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        }

        public async void JoinByCode(string raw)
        {
            var code = AgniSocial.CleanCode(raw); if (code.Length < 6) { status = "Code 6 akshar ka hota hai"; Changed?.Invoke(); return; }
            if (busy) return; if (InParty) await LeaveAsync();
            busy = true; status = "Room " + code + " se jud rahe hain…"; Changed?.Invoke();
            try
            {
                var opt = new JoinSessionOptions { PlayerProperties = MyProps(false) }.WithNetworkHandler(new AgniRelayHandler(code));
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, opt);
                Hook(); status = "Squad me aa gaye!";
            }
            catch (Exception e) { status = "Room " + code + " nahi mila: " + AgniOnline.Short(e); session = null; }
            busy = false; Rebuild();
        }

        void Hook()
        {
            if (session == null) return;
            session.Changed += Rebuild; session.PlayerJoined += _ => Rebuild(); session.PlayerHasLeft += _ => Rebuild(); session.PlayerPropertiesChanged += Rebuild;
            session.RemovedFromSession += () => { status = "Squad se hata diye gaye"; session = null; AgniNet.Shutdown(); Rebuild(); };
            session.Deleted += () => { status = "Leader ne squad band kar di"; session = null; AgniNet.Shutdown(); Rebuild(); };
        }

        void Rebuild()
        {
            members.Clear();
            if (session != null)
            {
                foreach (var p in session.Players)
                {
                    string nick = p.Properties != null && p.Properties.TryGetValue(KName, out var n) ? n.Value : "Player";
                    bool ready = p.Properties != null && p.Properties.TryGetValue(KReady, out var r) && r.Value == "1";
                    members.Add(new PartyMember { playerId = p.Id, name = nick, ready = ready, leader = p.Id == session.Host, isLocal = p.Id == AgniOnline.PlayerId });
                }
                members.Sort((a, b) => a.leader == b.leader ? 0 : a.leader ? -1 : 1);
            }
            Changed?.Invoke();
        }

        public async void Leave() { await LeaveAsync(); }
        async Task LeaveAsync()
        {
            var s = session; session = null;
            try { if (s != null) { if (s.IsHost) await s.AsHost().DeleteAsync(); else await s.LeaveAsync(); } } catch (Exception e) { Debug.LogWarning("[AgniOnline] leave: " + e.Message); }
            AgniNet.Shutdown(); status = "Squad chhod di"; Rebuild();
        }

        public async void SetReady(bool ready)
        {
            if (session == null) return;
            try { session.CurrentPlayer.SetProperties(MyProps(ready)); await session.SaveCurrentPlayerDataAsync(); }
            catch (Exception e) { status = "Ready nahi hua: " + AgniOnline.Short(e); }
            Rebuild();
        }

        public void StartMatch()
        {
            if (!IsLeader) return;
            if (!AllReady) { status = "Sab ready nahi hain"; Changed?.Invoke(); return; }
            try { var h = session.AsHost(); h.IsLocked = true; _ = h.SavePropertiesAsync(); } catch { } // nobody new joins mid-match
            AgniMatchPrefs.SquadFriendNames.Clear(); AgniMatchPrefs.HostParty = Code;
            if (AgniPartyNet.Current != null) AgniPartyNet.Current.GoingToMatchRpc();
            AgniNet.ServerLoadScene(AgniNetConfig.MatchScene);   // NGO (over Relay) takes the whole squad into the arena; bots fill to 50
        }
        public void Tick(float dt) { }
    }

    /// Connects NGO through UGS Relay with OUR NetworkManager + approval payload (instead of the package's default handler).
    public class AgniRelayHandler : INetworkHandler
    {
        readonly string code; // joiners know the room code before the session object exists; the server groups squads by it
        public AgniRelayHandler(string roomCode = null) { code = roomCode; }
        public Task StartAsync(NetworkConfiguration c) => StartAsync(c, default);
        public Task StartAsync(NetworkConfiguration c, System.Threading.CancellationToken ct)
        {
            if (c.Type != NetworkType.Relay) throw new Exception("Agni: only Relay networking is wired for online squads");
            bool host = c.Role != NetworkRole.Client;
            var data = c.RelayServerData; // the SDK hands NGO clients their join data here too (RelayClientData is only filled for Netcode for Entities)
            if (!AgniNet.StartRelay(host, data, string.IsNullOrEmpty(code) ? AgniSocial.Party.Code : code)) throw new Exception("NGO start failed");
            return Task.CompletedTask;
        }
        public Task StopAsync() { AgniNet.Shutdown(); return Task.CompletedTask; }
    }
}
