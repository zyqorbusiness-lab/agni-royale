// Agni Royale — squad party over the local network (same Wi-Fi / hotspot / same PC).
// Leader = NGO host. A room code is broadcast on UDP 47777 so friends can join by typing the code or tapping
// agniroyale://join?code=XXXXXX. The party lives in the Lobby scene; START takes everyone into the match together.
// For internet play the same IPartyService is implemented on a backend (IOnlineBackend.ResolveRoomCode).
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Agni.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Social
{
    public struct PartyMemberNet : INetworkSerializable, IEquatable<PartyMemberNet>
    {
        public ulong clientId; public FixedString32Bytes name; public FixedString32Bytes playerId; public bool ready, leader;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref clientId); s.SerializeValue(ref name); s.SerializeValue(ref playerId); s.SerializeValue(ref ready); s.SerializeValue(ref leader); }
        public bool Equals(PartyMemberNet o) => clientId == o.clientId && name.Equals(o.name) && playerId.Equals(o.playerId) && ready == o.ready && leader == o.leader;
    }

    public class LanPartyService : IPartyService
    {
        string code = ""; string status = ""; bool joining; float joinUntil; string joinCode;
        readonly List<PartyMember> members = new List<PartyMember>();
        readonly List<FriendInfo> mockSquad = new List<FriendInfo>();
        public event Action Changed;
        public static event Action Going; internal static void RaiseGoing() => Going?.Invoke();

        public LanPartyService()
        {
            AgniPartyNet.Changed += Rebuild;
            AgniSocial.Friends.InviteAccepted += f =>
            {
                if (members.Count + 0 >= AgniNetConfig.SquadSize) return;
                if (!InParty) Create();
                if (!mockSquad.Exists(x => x.playerId == f.playerId)) mockSquad.Add(f);
                PushMock(); status = f.name + " squad me aa gaya"; Rebuild();
            };
            AgniNet.ApprovalGate = () => AgniPartyNet.Current != null && AgniNet.Clients.Count + mockSquad.Count >= AgniNetConfig.SquadSize ? "Squad full hai (4/4)" : null;
        }
        public bool InParty => AgniNet.Running && (AgniPartyNet.Current != null || AgniNet.Role == NetRole.Host);
        public bool IsLeader => AgniNet.Role == NetRole.Host;
        public string Code => code;
        public string Status => status;
        public IReadOnlyList<PartyMember> Members => members;
        public bool AllReady { get { foreach (var m in members) if (!m.ready && !m.leader) return false; return true; } } // the leader readies by pressing START

        void PushMock()
        {
            if (AgniPartyNet.Current == null || !AgniNet.IsServer) return;
            var s = ""; foreach (var f in mockSquad) s += (s.Length > 0 ? "|" : "") + f.name; AgniPartyNet.Current.MockSquad.Value = s;
        }

        void Rebuild()
        {
            members.Clear(); var p = AgniPartyNet.Current;
            if (p != null)
            {
                code = p.Code.Value.ToString();
                ulong me = AgniNet.NM != null ? AgniNet.NM.LocalClientId : 0;
                foreach (var m in p.Members) members.Add(new PartyMember { clientId = m.clientId, name = m.name.ToString(), playerId = m.playerId.ToString(), ready = m.ready, leader = m.leader, isLocal = m.clientId == me });
                var ms = p.MockSquad.Value.ToString();
                if (!string.IsNullOrEmpty(ms)) foreach (var n in ms.Split('|')) members.Add(new PartyMember { name = n, ready = true, remoteFriend = true });
                members.Sort((a, b) => a.leader == b.leader ? 0 : a.leader ? -1 : 1);
            }
            else if (!AgniNet.Running) { members.Clear(); }
            Changed?.Invoke();
        }

        public void Create()
        {
            if (InParty) return;
            code = AgniSocial.NewCode();
            if (!AgniNet.StartHost(code)) { status = "Room nahi ban paya (port busy?)"; Changed?.Invoke(); return; }
            SpawnPartyObject();
            status = "Squad ban gayi — code share karo"; Rebuild();
        }
        /// host: (re)spawn the replicated party object, e.g. after coming back from a match
        public void SpawnPartyObject()
        {
            if (!AgniNet.IsServer || AgniPartyNet.Current != null) return;
            var prefab = Resources.Load<GameObject>("AgniNet/AgniParty"); if (prefab == null) { Debug.LogError("[AgniParty] prefab missing"); return; }
            var go = UnityEngine.Object.Instantiate(prefab); var p = go.GetComponent<AgniPartyNet>();
            p.Code.Value = string.IsNullOrEmpty(code) ? (code = AgniSocial.NewCode()) : code;
            go.GetComponent<NetworkObject>().Spawn(true);
            PushMock();
        }

        public void JoinByCode(string raw)
        {
            if (InParty) Leave();
            raw = (raw ?? "").Trim();
            // dev shortcut: an IP address ("192.168.1.7" or "192.168.1.7:7777") connects directly
            if (raw.Split('.').Length == 4)
            {
                var parts = raw.Split(':'); ushort port = parts.Length > 1 && ushort.TryParse(parts[1], out var pp) ? pp : AgniNetConfig.DefaultPort;
                status = "Jud rahe hain " + parts[0] + "…"; AgniNet.StartClient(parts[0], port, ""); Changed?.Invoke(); return;
            }
            var c = AgniSocial.CleanCode(raw);
            if (c.Length != 6) { status = "Code 6 akshar ka hota hai"; Changed?.Invoke(); return; }
            joining = true; joinCode = c; joinUntil = Time.realtimeSinceStartup + 8f; status = "Room " + c + " dhoondh rahe hain…";
            AgniLanDiscovery.Listen(true); Changed?.Invoke();
        }
        public void Leave()
        {
            joining = false; AgniLanDiscovery.Listen(false); AgniLanDiscovery.Beacon(null);
            foreach (var f in mockSquad) f.presence = Presence.Online; mockSquad.Clear();
            AgniNet.Shutdown(); code = ""; status = "Squad chhod di"; members.Clear(); Changed?.Invoke();
        }
        public void SetReady(bool r) { if (AgniPartyNet.Current != null) AgniPartyNet.Current.SetReadyRpc(r); }
        public void StartMatch()
        {
            if (!IsLeader) return;
            if (!AllReady) { status = "Sab ready nahi hain"; Changed?.Invoke(); return; }
            AgniMatchPrefs.SquadFriendNames.Clear(); foreach (var f in mockSquad) AgniMatchPrefs.SquadFriendNames.Add(f.name);
            AgniMatchPrefs.HostParty = code;
            AgniLanDiscovery.Beacon(null);
            if (AgniPartyNet.Current != null) { AgniPartyNet.Current.GoingToMatchRpc(); }
            AgniNet.ServerLoadScene(AgniNetConfig.MatchScene);
        }
        public void Tick(float dt)
        {
            if (IsLeader && AgniPartyNet.Current != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == AgniNetConfig.LobbyScene)
                AgniLanDiscovery.Beacon(code, AgniNetConfig.Port, AgniNet.Me.name, members.Count);
            if (joining)
            {
                if (AgniLanDiscovery.Found.TryGetValue(joinCode, out var hit))
                {
                    joining = false; AgniLanDiscovery.Listen(false);
                    code = joinCode; status = hit.host + " ki squad me jud rahe hain…";
                    AgniNet.StartClient(hit.address, hit.port, joinCode); Changed?.Invoke();
                }
                else if (Time.realtimeSinceStartup > joinUntil)
                {
                    joining = false; AgniLanDiscovery.Listen(false);
                    status = "Room " + joinCode + " nahi mila (same Wi-Fi par ho?)"; Changed?.Invoke();
                }
            }
        }
    }

    /// Who is taking which mock friends / party into the next match (host side).
    public static class AgniMatchPrefs
    {
        public static readonly List<string> SquadFriendNames = new List<string>();
        public static string HostParty = "";
    }

    /// Tiny LAN beacon: the party leader broadcasts "AGNI1|CODE|port|name|members" once a second on UDP 47777.
    public static class AgniLanDiscovery
    {
        public const int Port = 47777;
        public struct Hit { public string address, host; public ushort port; public int members; public float seen; }
        public static readonly Dictionary<string, Hit> Found = new Dictionary<string, Hit>();
        static UdpClient tx, rx; static float nextBeacon;

        public static void Beacon(string code, ushort port = 0, string host = "", int members = 1)
        {
            if (code == null) { tx?.Close(); tx = null; return; }
            if (Time.realtimeSinceStartup < nextBeacon) return; nextBeacon = Time.realtimeSinceStartup + 1f;
            try
            {
                if (tx == null) { tx = new UdpClient(); tx.EnableBroadcast = true; }
                var b = Encoding.UTF8.GetBytes("AGNI1|" + code + "|" + port + "|" + (host ?? "").Replace("|", "") + "|" + members);
                tx.Send(b, b.Length, new IPEndPoint(IPAddress.Broadcast, Port));
                tx.Send(b, b.Length, new IPEndPoint(IPAddress.Loopback, Port)); // same-device test
            }
            catch (Exception e) { Debug.LogWarning("[AgniLan] beacon: " + e.Message); }
        }
        public static void Listen(bool on)
        {
            if (!on) { rx?.Close(); rx = null; return; }
            if (rx != null) return;
            try
            {
                rx = new UdpClient(); rx.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                rx.Client.Bind(new IPEndPoint(IPAddress.Any, Port)); rx.EnableBroadcast = true; Found.Clear();
            }
            catch (Exception e) { Debug.LogWarning("[AgniLan] listen: " + e.Message); rx = null; }
        }
        public static void Poll()
        {
            if (rx == null) return;
            try
            {
                while (rx.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0); var b = rx.Receive(ref ep); var p = Encoding.UTF8.GetString(b).Split('|');
                    if (p.Length < 5 || p[0] != "AGNI1") continue;
                    Found[p[1]] = new Hit { address = ep.Address.ToString(), port = ushort.Parse(p[2]), host = p[3], members = int.Parse(p[4]), seen = Time.realtimeSinceStartup };
                }
            }
            catch (Exception e) { Debug.LogWarning("[AgniLan] poll: " + e.Message); }
        }
    }

    /// agniroyale://join?code=XXXXXX  (Android intent filter in Assets/Plugins/Android/AndroidManifest.xml)
    public static class AgniDeepLink
    {
        public static string PendingCode;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Application.deepLinkActivated += Handle;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) Handle(Application.absoluteURL);
            var a = AgniNetConfig.Arg("-agniDeepLink"); if (a != null) Handle(a);
        }
        public static void Handle(string url)
        {
            var c = Parse(url); if (c != null) { PendingCode = c; Debug.Log("[AgniDeepLink] join code " + c); }
        }
        public static string Parse(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("agniroyale://", StringComparison.OrdinalIgnoreCase)) return null;
            int q = url.IndexOf("code=", StringComparison.OrdinalIgnoreCase); if (q < 0) return null;
            var c = AgniSocial.CleanCode(url.Substring(q + 5).Split('&')[0]); return c.Length == 6 ? c : null;
        }
    }

    /// Runs the social services every frame (presence drift, mock accepts, LAN beacon polling).
    public class AgniSocialTicker : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (AgniNetConfig.HeadlessServer) return; var go = new GameObject("AgniSocial"); DontDestroyOnLoad(go); go.AddComponent<AgniSocialTicker>(); }
        void Update() { AgniLanDiscovery.Poll(); AgniSocial.Friends.Tick(Time.unscaledDeltaTime); AgniSocial.Party.Tick(Time.unscaledDeltaTime); }
    }
}
