// Agni Royale — multiplayer core: Netcode for GameObjects + Unity Transport, server-authoritative.
// One runtime-built NetworkManager (DontDestroyOnLoad) is shared by the lobby party, the match and a headless server.
//   Host (LAN / offline practice): AgniNet.StartHost()          — this phone is server + player
//   Client:                       AgniNet.StartClient(ip, port)
//   Dedicated server (Linux):     AgniRoyale.x86_64 -batchmode -nographics -agniServer [-agniPort 7777] [-agniBots 50]
// Command-line test flags: -agniHost, -agniClient <ip>, -agniPort <n>, -agniBots <n>, -agniZoneScale <f>, -agniCountdown <s>, -agniName <name>
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Agni.Net
{
    public static class AgniNetConfig
    {
        public const ushort DefaultPort = 7777;
        public const int MaxPlayers = 50, SquadSize = 4;
        public const string MatchScene = "AgniArena", LobbyScene = "Lobby";
        public const string ProtocolVersion = "agni-net-1";

        public static string Arg(string k) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, k); return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null; }
        public static bool Has(string k) => Array.IndexOf(Environment.GetCommandLineArgs(), k) >= 0;
        public static float ArgF(string k, float d) { var s = Arg(k); return s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : d; }
        public static int ArgI(string k, int d) { var s = Arg(k); return s != null && int.TryParse(s, out var f) ? f : d; }

        public static ushort Port => (ushort)ArgI("-agniPort", DefaultPort);
        /// total players per match (humans + bots). 50 = full match. -agniBots N overrides the TOTAL.
        public static int TargetPlayers => Mathf.Clamp(ArgI("-agniBots", MaxPlayers), 1, MaxPlayers);
        /// zone timing multiplier (0.2 = 5× faster, for tests)
        public static float ZoneScale => Mathf.Clamp(ArgF("-agniZoneScale", 1f), 0.02f, 4f);
        public static float Countdown(bool dedicated) => ArgF("-agniCountdown", dedicated ? 30f : 8f);
        public static bool HeadlessServer => Has("-agniServer");
    }

    /// What a client tells the server when it connects (JSON in the NGO connection payload).
    [Serializable]
    public class AgniJoinPayload
    {
        public string v = AgniNetConfig.ProtocolVersion;
        public string name = "Player";
        public string playerId = "";
        public string party = "";   // party / room code: same code = same squad
        public byte[] Bytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
        public static AgniJoinPayload From(byte[] b)
        {
            try { return b != null && b.Length > 0 && b.Length < 2048 ? JsonUtility.FromJson<AgniJoinPayload>(Encoding.UTF8.GetString(b)) : null; } catch { return null; }
        }
    }

    public class AgniClientInfo { public ulong clientId; public string name, playerId, party; public int team = -1; }

    public enum NetRole { None, Host, Client, Server }

    public class AgniNet : MonoBehaviour
    {
        public static AgniNet Instance { get; private set; }
        public static NetworkManager NM => Instance != null ? Instance.nm : null;
        public static NetRole Role { get; private set; }
        public static bool Running => NM != null && NM.IsListening;
        public static bool IsServer => NM != null && NM.IsServer;
        public static bool MatchInProgress;  // server: set by AgniMatch once the match left warm-up
        /// server-side registry of connected humans (filled by connection approval)
        public static readonly Dictionary<ulong, AgniClientInfo> Clients = new Dictionary<ulong, AgniClientInfo>();
        public static string LastDisconnectReason;
        public static event Action<string> StatusChanged;
        public static AgniJoinPayload Me = new AgniJoinPayload();
        /// extra server-side admission rule (the lobby party caps itself at 4); returns a reason to refuse, or null
        public static Func<string> ApprovalGate;

        NetworkManager nm; UnityTransport utp;

        public static AgniNet Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AgniNet"); go.SetActive(false); DontDestroyOnLoad(go);
            var self = go.AddComponent<AgniNet>(); Instance = self;
            self.utp = go.AddComponent<UnityTransport>();
            self.utp.MaxPacketQueueSize = 1024; // ~320 loot stacks + crates sync in one burst when a client joins (MaxPayloadSize stays default: a bigger one broke the reliable handshake)
            self.nm = go.AddComponent<NetworkManager>();
            if (self.nm.NetworkConfig == null) self.nm.NetworkConfig = new NetworkConfig();
            var cfg = self.nm.NetworkConfig;
            cfg.NetworkTransport = self.utp;
            cfg.ConnectionApproval = true;
            cfg.EnableSceneManagement = true;
            cfg.TickRate = 30;
            cfg.ClientConnectionBufferTimeout = 45; // slow phones / busy hosts: the arena (villages + loot) can take a while to sync
            cfg.ForceSamePrefabs = true;
            if (AgniNetConfig.Has("-agniNetDebug")) { self.nm.LogLevel = LogLevel.Developer; }
            go.SetActive(true);
            int n = 0;
            foreach (var p in Resources.LoadAll<GameObject>("AgniNet"))
                if (p.GetComponent<NetworkObject>() != null) { self.nm.AddNetworkPrefab(p); n++; }
            self.nm.ConnectionApprovalCallback = self.Approve;
            self.nm.OnClientDisconnectCallback += self.OnDisconnect;
            self.nm.OnClientConnectedCallback += id => { if (self.nm.IsServer) Debug.Log("[AgniNet] client connected " + id); };
            Debug.Log("[AgniNet] NetworkManager ready, " + n + " network prefabs; inv serializer " + AgniNetTypes.SelfTest());
            return self;
        }

        static void Say(string s) { Debug.Log("[AgniNet] " + s); StatusChanged?.Invoke(s); }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            res.CreatePlayerObject = false; // AgniMatch spawns player objects itself (warm-up spot, squad drop)
            var p = req.ClientNetworkId == NetworkManager.ServerClientId ? Me : AgniJoinPayload.From(req.Payload);
            string why = null;
            if (p == null) why = "Galat data";
            else if (p.v != AgniNetConfig.ProtocolVersion) why = "Game update karo (version alag hai)";
            else if (Clients.Count >= AgniNetConfig.MaxPlayers) why = "Room full hai";
            else if (MatchInProgress) why = "Match chal raha hai — agle match me aao";
            else if (req.ClientNetworkId != NetworkManager.ServerClientId && ApprovalGate != null) why = ApprovalGate();
            res.Approved = why == null; res.Reason = why;
            if (why != null) { Say("rejected " + req.ClientNetworkId + ": " + why); return; }
            string nm0 = string.IsNullOrWhiteSpace(p.name) ? "Player" : p.name.Trim(); if (nm0.Length > 14) nm0 = nm0.Substring(0, 14);
            Clients[req.ClientNetworkId] = new AgniClientInfo { clientId = req.ClientNetworkId, name = nm0, playerId = p.playerId ?? "", party = (p.party ?? "").ToUpperInvariant() };
            Say("approved " + req.ClientNetworkId + " " + nm0 + " party=" + p.party);
        }

        void OnDisconnect(ulong id)
        {
            if (nm.IsServer) { Clients.Remove(id); Say("client left " + id); return; }
            // we are a client and lost the server
            LastDisconnectReason = string.IsNullOrEmpty(nm.DisconnectReason) ? "Server se connection toot gaya" : nm.DisconnectReason;
            Say("disconnected: " + LastDisconnectReason);
            Role = NetRole.None;
            if (SceneManager.GetActiveScene().name != AgniNetConfig.LobbyScene && Application.CanStreamedLevelBeLoaded(AgniNetConfig.LobbyScene))
                SceneManager.LoadScene(AgniNetConfig.LobbyScene);
        }

        static void FillMe(string party)
        {
            Me.name = AgniNetConfig.Arg("-agniName") ?? (Agni.UI.AgniProfile.HasName ? Agni.UI.AgniProfile.Name : "AgniVeer");
            Me.playerId = Agni.Social.AgniSocial.MyPlayerId; if (Me.playerId.Length > 28) Me.playerId = Me.playerId.Substring(0, 28); // fits FixedString32
            if (Me.name.Length > 24) Me.name = Me.name.Substring(0, 24);
            Me.party = party ?? "";
        }

        public static bool StartHost(string party = "")
        {
            Ensure(); if (Running) return true; FillMe(party); Clients.Clear(); MatchInProgress = false;
            Instance.utp.SetConnectionData("0.0.0.0", AgniNetConfig.Port, "0.0.0.0");
            NM.NetworkConfig.ConnectionData = Me.Bytes();
            bool ok = NM.StartHost(); Role = ok ? NetRole.Host : NetRole.None; Say(ok ? "host started on port " + AgniNetConfig.Port : "host start FAILED"); return ok;
        }
        /// Online (UGS Relay) start: the session hands us relay data; same approval payload as LAN.
        public static bool StartRelay(bool host, Unity.Networking.Transport.Relay.RelayServerData data, string party = "")
        {
            Ensure(); if (Running) return true; FillMe(party); if (host) { Clients.Clear(); MatchInProgress = false; }
            Instance.utp.SetRelayServerData(data);
            NM.NetworkConfig.ConnectionData = Me.Bytes();
            bool ok = host ? NM.StartHost() : NM.StartClient(); Role = ok ? (host ? NetRole.Host : NetRole.Client) : NetRole.None;
            Say(ok ? (host ? "relay host started" : "relay client connecting") : "relay start FAILED"); return ok;
        }
        public static bool StartClient(string address, ushort port, string party = "")
        {
            Ensure(); if (Running) return true; FillMe(party);
            Instance.utp.SetConnectionData(address, port);
            NM.NetworkConfig.ConnectionData = Me.Bytes();
            bool ok = NM.StartClient(); Role = ok ? NetRole.Client : NetRole.None; Say(ok ? "client connecting to " + address + ":" + port : "client start FAILED"); return ok;
        }
        public static bool StartServer()
        {
            Ensure(); if (Running) return true; Clients.Clear(); MatchInProgress = false;
            Instance.utp.SetConnectionData("0.0.0.0", AgniNetConfig.Port, "0.0.0.0");
            bool ok = NM.StartServer(); Role = ok ? NetRole.Server : NetRole.None; Say(ok ? "dedicated server listening on " + AgniNetConfig.Port : "server start FAILED"); return ok;
        }
        public static void Shutdown() { if (NM != null && NM.IsListening) NM.Shutdown(); Role = NetRole.None; Clients.Clear(); MatchInProgress = false; }

        /// Server: switch every connected peer to a scene (NGO scene management).
        public static void ServerLoadScene(string scene)
        {
            if (!IsServer) return;
            var st = NM.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            Say("load scene " + scene + ": " + st);
        }

        public static float RttSeconds()
        {
            if (!Running || NM.IsServer) return 0f;
            try { return Instance.utp.GetCurrentRtt(NetworkManager.ServerClientId) / 1000f; } catch { return 0.1f; }
        }
        public static string Ping()
        {
            if (!Running) return "offline";
            if (NM.IsServer) return Role == NetRole.Server ? "server" : "host";
            try { return Instance.utp.GetCurrentRtt(NetworkManager.ServerClientId) + " ms"; } catch { return "?"; }
        }

        // --------- boot from the command line (test players + headless server)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CommandLineBoot()
        {
            if (AgniNetConfig.HeadlessServer)
            {
                Application.targetFrameRate = 30; // a headless server does not need to spin
                StartServer();
                ServerLoadScene(AgniNetConfig.MatchScene);
            }
            else if (AgniNetConfig.Has("-agniHost") && !AgniNetConfig.Has("-agniParty"))
            {
                if (StartHost()) ServerLoadScene(AgniNetConfig.MatchScene);
            }
            else if (AgniNetConfig.Arg("-agniClient") != null)
            {
                StartClient(AgniNetConfig.Arg("-agniClient"), AgniNetConfig.Port);
            }
        }
    }
}
