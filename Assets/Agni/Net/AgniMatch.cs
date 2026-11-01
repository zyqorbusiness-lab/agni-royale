// Agni Royale — match director (server): warm-up countdown, squads of 4, bots fill to 50, drop spots, loot, zone,
// kill feed, knock/team-wipe rules, winner. Also AgniCombat (explosions/flash/smoke, server maths + client FX RPCs).
using System.Collections.Generic;
using Agni.Social;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum MatchPhase : byte { Warmup = 0, Playing = 1, Ended = 2 }

    public class AgniMatch : NetworkBehaviour
    {
        public static AgniMatch Instance;
        public NetworkVariable<byte> Phase = new NetworkVariable<byte>();
        public NetworkVariable<double> StartAt = new NetworkVariable<double>();
        public NetworkVariable<int> AliveCount = new NetworkVariable<int>();
        public NetworkVariable<int> TeamsAlive = new NetworkVariable<int>();
        public NetworkVariable<int> WinnerTeam = new NetworkVariable<int>(-1);
        public NetworkVariable<int> Humans = new NetworkVariable<int>();
        public static System.Action<string, string, string, int> OnFeed; // killer, victim, weapon, 0 kill / 1 knock / 2 revive
        public static System.Action<int> OnEnded;

        public static float ArenaRadius = 1750f; // the playable circle (set by AgniArenaBootstrap)
        bool started; double endAt;
        static readonly string[] BotNames = { "Shera_x", "Babloo.ff", "Guddu007", "Raju_Rocket", "Tinku.OP", "MunnaBhai", "Chintu_pro", "Lucky.gg", "Pappu99", "Golu_Gun", "Bunty.x", "Monu_King",
            "Sonu.Sniper", "Kaalia", "Bittu_77", "Jaggu_Dada", "Dabbu.yt", "Lallan", "Pintu_Fire", "Sweety.ff", "Rinku_Raja", "Ballu", "Chikoo_x", "Titu.op", "Nanhe_Ustad",
            "Kallu.gg", "Bhola_007", "Mithu", "Pinky.op", "Dolly_x", "Rocky.Bhai", "Simba.ff", "Tuktuk", "Jalebi99", "Kaju_Katli", "Laddoo.x", "Rasgulla", "Samosa_King",
            "Chutki", "Bijli.gg", "Toofani", "Bhaukal", "Dhakkan_x", "Tiger.ff", "Sher_Khan", "Jhakaas", "Bindaas.x", "Raja_Babu", "Chulbul", "Gabbar.gg" };

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            bool dedicated = !NetworkManager.IsHost;
            StartAt.Value = NetworkManager.ServerTime.Time + AgniNetConfig.Countdown(dedicated);
            Phase.Value = (byte)MatchPhase.Warmup;
            foreach (var kv in AgniNet.Clients) SpawnHuman(kv.Key);
            NetworkManager.OnClientConnectedCallback += OnConnected;
            NetworkManager.OnClientDisconnectCallback += OnLeft;
            if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnSynchronizeComplete += OnConnected;
            var zp = Resources.Load<GameObject>("AgniNet/AgniZone"); var z = Instantiate(zp); z.GetComponent<NetworkObject>().Spawn(true);
            var cp = Resources.Load<GameObject>("AgniNet/AgniCombat"); var c = Instantiate(cp); c.GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[AgniMatch] warm-up, start in " + AgniNetConfig.Countdown(dedicated) + " s, target players " + AgniNetConfig.TargetPlayers);
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) { NetworkManager.OnClientConnectedCallback -= OnConnected; NetworkManager.OnClientDisconnectCallback -= OnLeft; if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnSynchronizeComplete -= OnConnected; }
            if (Instance == this) Instance = null; AgniNet.MatchInProgress = false;
        }
        void OnConnected(ulong id) { if (Phase.Value == (byte)MatchPhase.Warmup) SpawnHuman(id); }
        void OnLeft(ulong id) { foreach (var p in AgniPlayer.All.ToArray()) if (!p.IsBot.Value && p.OwnerClientId == id && !p.Dead) { p.Die(null, "chala gaya"); } }

        void SpawnHuman(ulong id)
        {
            if (!AgniNet.Clients.ContainsKey(id)) return;
            foreach (var p in AgniPlayer.All) if (!p.IsBot.Value && p.OwnerClientId == id) return;
            var info = AgniNet.Clients[id];
            var go = Instantiate(Resources.Load<GameObject>("AgniNet/AgniPlayer"), WarmupSpot(AgniPlayer.All.Count), Quaternion.identity);
            var pl = go.GetComponent<AgniPlayer>(); pl.PlayerName.Value = info.name; pl.IsBot.Value = false;
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            Humans.Value = AgniNet.Clients.Count;
            Debug.Log("[AgniMatch] human spawned " + info.name + " (client " + id + ")");
        }
        static Vector3 WarmupSpot(int i) { float a = i * 0.9f; return Ground(new Vector3(Mathf.Cos(a) * (6 + i * 0.4f), 0, -ArenaRadius * 0.15f + Mathf.Sin(a) * (6 + i * 0.4f))); }
        public static Vector3 Ground(Vector3 p)
        {
            // cast from 60 m: the arena has nothing taller than a two-floor house, and players/props parked in the sky must not count as ground
            var hits = Physics.RaycastAll(new Vector3(p.x, 260f, p.z), Vector3.down, 320f, ~(1 << 4), QueryTriggerInteraction.Ignore); // 260: the Himalaya on the India map (IMPORT v1); water layer ignored
            float best = -1e9f; foreach (var h in hits) if (h.collider.GetComponentInParent<AgniPlayer>() == null && h.collider.GetComponentInParent<AgniPickup>() == null && h.collider.GetComponentInParent<AgniCrate>() == null && h.point.y > best) best = h.point.y;
            if (best > -1e8f) { if (best > 220f) Debug.LogWarning("[AgniMatch] tall ground at " + p + " y=" + best); return new Vector3(p.x, best + 0.05f, p.z); }
            return new Vector3(p.x, 0.05f, p.z);
        }
        public static Vector3 SafeSpot(Vector3 near) { var z = AgniZone.Instance; var c = z != null ? z.CenterNow() : Vector3.zero; return Ground(Vector3.Lerp(near, c, 0.3f)); }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            double now = NetworkManager.ServerTime.Time;
            if (Phase.Value == (byte)MatchPhase.Warmup)
            {
                bool dedicated = !NetworkManager.IsHost;
                if (dedicated && AgniNet.Clients.Count == 0) { StartAt.Value = now + AgniNetConfig.Countdown(true); return; } // wait for someone
                if (now >= StartAt.Value) BeginMatch();
            }
            else if (Phase.Value == (byte)MatchPhase.Playing) AgniLoot.ServerTick(now);
            else if (Phase.Value == (byte)MatchPhase.Ended && endAt > 0 && now > endAt) { endAt = 0; BackToLobby(); }
        }

        // ---------------- start: squads, bots, drop spots, loot, zone
        void BeginMatch()
        {
            if (started) return; started = true; AgniNet.MatchInProgress = true;
            var humans = new List<AgniPlayer>(); foreach (var p in AgniPlayer.All) if (!p.IsBot.Value) humans.Add(p);
            // group humans by party code (no code = alone)
            var groups = new Dictionary<string, List<AgniPlayer>>();
            foreach (var h in humans)
            {
                string code = AgniNet.Clients.TryGetValue(h.OwnerClientId, out var ci) && !string.IsNullOrEmpty(ci.party) ? ci.party : "solo" + h.OwnerClientId;
                if (h.OwnerClientId == NetworkManager.ServerClientId && NetworkManager.IsHost && !string.IsNullOrEmpty(AgniMatchPrefs.HostParty)) code = AgniMatchPrefs.HostParty;
                if (!groups.TryGetValue(code, out var l)) groups[code] = l = new List<AgniPlayer>(); l.Add(h);
            }
            int team = 0; var teams = new List<List<AgniPlayer>>();
            foreach (var g in groups.Values) for (int i = 0; i < g.Count; i += AgniNetConfig.SquadSize) { var t = g.GetRange(i, Mathf.Min(AgniNetConfig.SquadSize, g.Count - i)); foreach (var p in t) p.Team.Value = team; teams.Add(t); team++; }
            // bots: first top up human squads (demo friends who "accepted" an invite get their own names), then whole bot squads
            int target = Mathf.Max(AgniNetConfig.TargetPlayers, humans.Count); int total = humans.Count; int nameI = Random.Range(0, BotNames.Length);
            var friendNames = new Queue<string>(AgniMatchPrefs.SquadFriendNames);
            for (int ti = 0; ti < teams.Count && total < target; ti++)
                while (teams[ti].Count < AgniNetConfig.SquadSize && total < target) { teams[ti].Add(SpawnBot(ti, friendNames.Count > 0 && ti == 0 ? friendNames.Dequeue() : BotNames[nameI++ % BotNames.Length])); total++; }
            while (total < target)
            {
                var t = new List<AgniPlayer>();
                while (t.Count < AgniNetConfig.SquadSize && total < target) { t.Add(SpawnBot(team, BotNames[nameI++ % BotNames.Length])); total++; }
                teams.Add(t); team++;
            }
            // drop spots: each squad lands together at its own point around the island
            var rnd = new System.Random(System.Environment.TickCount);
            // classic: every squad lands next to a house it can loot (each building used once while there are enough)
            var fps = new List<(Vector2 c, float r)>(AgniVillages.Footprints); for (int i = fps.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = fps[i]; fps[i] = fps[j]; fps[j] = t; }
            if (AgniNetConfig.Has("-agniDropVillage")) { string vn = AgniNetConfig.Arg("-agniDropVillage"); foreach (var v in AgniVillages.Villages) if (v.name.Replace(" ", "").ToLower().StartsWith(vn.ToLower())) { int k0 = fps.FindIndex(f => (f.c - v.c).magnitude < v.r); if (k0 > 0) { var t = fps[0]; fps[0] = fps[k0]; fps[k0] = t; } } }
            for (int ti = 0; ti < teams.Count; ti++)
            {
                float a = (ti + (float)rnd.NextDouble() * 0.6f) / teams.Count * Mathf.PI * 2f; float r = ArenaRadius * (0.35f + 0.5f * (float)rnd.NextDouble());
                var c = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (fps.Count > 0) { var f = fps[ti % fps.Count]; var dir = (Vector2.zero - f.c).sqrMagnitude > 1 ? (Vector2.zero - f.c).normalized : Vector2.up; var q = f.c + dir * (f.r + 2.5f); c = new Vector3(q.x, 0, q.y); }
                for (int k = 0; k < teams[ti].Count; k++)
                {
                    var off = new Vector3(Mathf.Cos(k * 1.57f), 0, Mathf.Sin(k * 1.57f)) * 2.2f;
                    var pos = Ground(c + off); float yaw = Mathf.Atan2(-c.x, -c.z) * Mathf.Rad2Deg;
                    var p = teams[ti][k]; p.ServerResetForMatch(pos, yaw);
                    if (!p.IsBot.Value) Debug.Log("[AgniMatch] drop " + p.Nick + " team " + ti + " at " + pos + " (r " + new Vector2(pos.x, pos.z).magnitude.ToString("0") + ")");
                    if (AgniNetConfig.Has("-agniKit")) { foreach (var kk in ThrowMath.All) p.ServerAddThrowable(kk, 1); }
                }
            }
            AgniLoot.ServerSpawnAll(NetworkManager.ServerTime.Time);
            AgniVehicles.ServerSpawnAll(); // the 7 Blender vehicles near villages/landmarks, boats on the water
            Phase.Value = (byte)MatchPhase.Playing; Recount();
            AgniZone.Instance?.ServerBegin();
            Debug.Log("[AgniMatch] GO: " + total + " players in " + teams.Count + " squads (" + humans.Count + " human)");
        }

        AgniPlayer SpawnBot(int team, string name)
        {
            var go = Instantiate(Resources.Load<GameObject>("AgniNet/AgniPlayer"), Vector3.up * 500, Quaternion.identity);
            var pl = go.GetComponent<AgniPlayer>(); pl.PlayerName.Value = name; pl.IsBot.Value = true; pl.Team.Value = team;
            pl.brain = new AgniBotBrain(); go.GetComponent<NetworkObject>().Spawn(true);
            return pl;
        }

        // ---------------- deaths, team wipes, winner
        public void Feed(string killer, string victim, string weapon, int type) => FeedRpc(killer, victim, weapon, (byte)type);
        [Rpc(SendTo.ClientsAndHost)] void FeedRpc(FixedString32Bytes killer, FixedString32Bytes victim, FixedString32Bytes weapon, byte type) { OnFeed?.Invoke(killer.ToString(), victim.ToString(), weapon.ToString(), type); }

        public void CheckTeam(int team)
        {
            bool standing = false; foreach (var p in AgniPlayer.All) if (p.Team.Value == team && p.Alive) standing = true;
            if (!standing) foreach (var p in AgniPlayer.All.ToArray()) if (p.Team.Value == team && p.Knocked) p.Die(null, "squad khatam");
        }
        public void OnDeath(AgniPlayer p)
        {
            p.Placement.Value = Mathf.Max(1, CountAlive());
            CheckTeam(p.Team.Value); Recount();
            if (Phase.Value == (byte)MatchPhase.Playing && TeamsAlive.Value <= 1)
            {
                int win = -1; foreach (var q in AgniPlayer.All) if (!q.Dead) { win = q.Team.Value; q.Placement.Value = 1; }
                WinnerTeam.Value = win; Phase.Value = (byte)MatchPhase.Ended; endAt = NetworkManager.ServerTime.Time + 12;
                EndedRpc(win); Debug.Log("[AgniMatch] winner squad " + win);
            }
        }
        int CountAlive() { int n = 0; foreach (var q in AgniPlayer.All) if (!q.Dead) n++; return n; }
        void Recount()
        {
            AliveCount.Value = CountAlive(); var t = new HashSet<int>(); foreach (var q in AgniPlayer.All) if (!q.Dead) t.Add(q.Team.Value); TeamsAlive.Value = t.Count;
        }
        [Rpc(SendTo.ClientsAndHost)] void EndedRpc(int win) { OnEnded?.Invoke(win); }
        public static System.Action<Vector3, bool> OnAirdrop; // where, landed
        [Rpc(SendTo.ClientsAndHost)] public void AirdropRpc(Vector3 at) { OnAirdrop?.Invoke(at, false); }
        [Rpc(SendTo.ClientsAndHost)] public void AirdropLandedRpc(Vector3 at) { OnAirdrop?.Invoke(at, true); }

        void BackToLobby()
        {
            AgniNet.MatchInProgress = false;
            if (NetworkManager.IsHost) AgniNet.ServerLoadScene(AgniNetConfig.LobbyScene);       // party stays together in the lobby
            else { foreach (var id in new List<ulong>(NetworkManager.ConnectedClientsIds)) NetworkManager.DisconnectClient(id, "Match khatam — naya match shuru ho raha hai"); AgniNet.ServerLoadScene(AgniNetConfig.MatchScene); }
        }
    }
}
