// AgniShot — automated proof/test driver for the gameplay systems (runs in the editor AND in player builds).
// Active only with "-agniShot <dir>". Modes (-agniShotMode):
//   friends : Lobby scene — DOST panel, invite, requests, add-by-ID, room code, filled squad slots
//   grenade : AgniArena (offline host) — Gola arc + cook, explosion, Dhuan smoke, Patakha flash, Agni Deewar
//   ugs     : 2-process REAL online test (UGS sign-in, room code via Lobby, friend request, Relay match)
//   net     : 2-process host/client test (-agniHost / -agniClient <ip>), each side screenshots + logs what it sees
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Agni.Social;
using Agni.UI;
using UnityEngine;

namespace Agni.Net
{
    public class AgniShot : MonoBehaviour
    {
        string dir, mode, tagName; RenderTexture rt, rtUI; int W = 2400, H = 1080;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var d = AgniNetConfig.Arg("-agniShot"); if (string.IsNullOrEmpty(d)) return;
            var go = new GameObject("AgniShot"); DontDestroyOnLoad(go); var s = go.AddComponent<AgniShot>(); s.dir = d;
        }

        void Log(string m) => Debug.Log("[AgniShot] " + m);

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir); mode = AgniNetConfig.Arg("-agniShotMode") ?? "grenade"; tagName = AgniNetConfig.Arg("-agniShotName") ?? mode;
            SetSize(int.Parse(AgniNetConfig.Arg("-agniShotW") ?? "2400"), int.Parse(AgniNetConfig.Arg("-agniShotH") ?? "1080"));
            Log("mode " + mode + " dir " + dir);
            float limit = AgniNetConfig.ArgF("-agniShotTimeout", 240);
            StartCoroutine(Watchdog(limit));
            if (mode == "friends") yield return Friends();
            else if (mode == "net") yield return NetTest();
            else if (mode == "ugs") yield return UgsTest();
            else yield return Grenade();
            Log("done"); Quit(0);
        }
        IEnumerator Watchdog(float s) { yield return new WaitForSecondsRealtime(s); Log("TIMEOUT"); Quit(3); }

        // ------------------------------------------------------------------ friends
        IEnumerator Friends()
        {
            if (string.IsNullOrEmpty(AgniProfile.Name)) AgniProfile.Name = "AgniVeer";
            AgniLobby lobby = null; float t0 = Time.realtimeSinceStartup;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.realtimeSinceStartup - t0 < 60) yield return null;
            if (lobby == null) { Log("no lobby"); Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("10_lobby_solo");
            var panel = lobby.OpenFriends(AgniFriendsPanel.Tab.Dost); yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("11_dost_list");
            FriendInfo pick = null; foreach (var f in AgniSocial.Friends.Friends) if (f.presence == Presence.Online || f.presence == Presence.InLobby) { pick = f; break; }
            if (pick != null)
            {
                var err = AgniSocial.Friends.InviteToSquad(pick.playerId); if (err == null && !AgniSocial.Party.InParty) AgniSocial.Party.Create();
                panel.Toast(err ?? "Invite bheja: " + pick.name); Log("invited " + pick.name + " err=" + err);
                yield return new WaitForSecondsRealtime(0.6f); yield return Shot("12_invite_sent");
                yield return new WaitForSecondsRealtime(3.4f);
            }
            FriendInfo pick2 = null; foreach (var f in AgniSocial.Friends.Friends) if (f != pick && (f.presence == Presence.Online || f.presence == Presence.InLobby)) { pick2 = f; break; }
            if (pick2 != null) { AgniSocial.Friends.InviteToSquad(pick2.playerId); yield return new WaitForSecondsRealtime(3.6f); }
            yield return Shot("13_dost_in_squad");
            panel.Show(AgniFriendsPanel.Tab.Requests); yield return new WaitForSecondsRealtime(1.1f); yield return Shot("14_requests");
            panel.DemoAdd("9124408852"); yield return new WaitForSecondsRealtime(0.8f); yield return Shot("15_add_id");
            panel.Show(AgniFriendsPanel.Tab.Room); yield return new WaitForSecondsRealtime(1.1f); yield return Shot("16_room_code");
            Log("party code=" + AgniSocial.Party.Code + " members=" + AgniSocial.Party.Members.Count + " link=" + AgniSocial.DeepLink(AgniSocial.Party.Code) + " parsed=" + AgniDeepLink.Parse(AgniSocial.DeepLink(AgniSocial.Party.Code)));
            panel.Close(); yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("17_lobby_squad");
            AgniSocial.Party.Leave();
        }

        // ------------------------------------------------------------------ grenade
        IEnumerator WaitPlaying(float extra)
        {
            float t0 = Time.realtimeSinceStartup;
            while ((AgniLocalPlayer.Instance == null || AgniMatch.Instance == null || AgniMatch.Instance.Phase.Value != (byte)MatchPhase.Playing) && Time.realtimeSinceStartup - t0 < 120) yield return null;
            if (AgniLocalPlayer.Instance == null) { Log("no local player"); Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(extra);
        }
        AgniPlayer Nearest(float min, float max, bool humansOnly = false)
        {
            var me = AgniPlayer.Local; AgniPlayer best = null; float bd = 1e9f;
            foreach (var p in AgniPlayer.All)
            {
                if (p == me || p.Dead || (humansOnly && p.IsBot.Value)) continue;
                float d = Vector3.Distance(p.transform.position, me.transform.position);
                if (d >= min && d <= max && d < bd) { bd = d; best = p; }
            }
            return best;
        }
        /// pitch whose simulated landing point is closest to `dist` metres ahead (same maths as the arc preview / server)
        float BestPitch(ThrowKind k, float dist)
        {
            var me = AgniPlayer.Local; var lp = AgniLocalPlayer.Instance; float best = 0, bErr = 1e9f; var pts = new List<Vector3>();
            for (float p = -30; p <= 45; p += 1f)
            {
                ThrowMath.Launch(me.transform.position, lp.Yaw, p, k, 1f, out var o, out var v);
                ThrowMath.Simulate(o, v, k, pts, out var land, out var n, 4f, ~0, me.GetComponent<Collider>());
                var dd = land - me.transform.position; dd.y = 0; float e = Mathf.Abs(dd.magnitude - dist);
                if (e < bErr) { bErr = e; best = p; }
            }
            return best;
        }
        void Face(Vector3 at, float pitch)
        {
            var lp = AgniLocalPlayer.Instance; var d = at - lp.P.transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) lp.Yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; lp.Pitch = pitch;
        }
        IEnumerator Throw(ThrowKind k, float cook, float pitch, string arcShot)
        {
            var lp = AgniLocalPlayer.Instance; var me = lp.P;
            me.SelectThrowRpc((byte)k); yield return new WaitForSecondsRealtime(0.4f);
            lp.Pitch = pitch; lp.script = new AgniLocalPlayer.Scripted { on = true, fire = true };
            yield return new WaitForSecondsRealtime(cook);
            if (arcShot != null)
            {
                var arcLr = lp.transform.Find("ThrowArc")?.GetComponent<LineRenderer>();
                Log("throw " + k + " yaw=" + lp.Yaw.ToString("0") + " body=" + me.transform.eulerAngles.y.ToString("0") + " cam=" + lp.Cam.transform.eulerAngles.y.ToString("0") + " inputYaw=" + me.input.yaw.ToString("0") + (arcLr != null && arcLr.positionCount > 1 ? " arcEnd=" + (arcLr.GetPosition(arcLr.positionCount - 1) - me.transform.position) : " noarc"));
                yield return Shot(arcShot);
            }
            lp.script = new AgniLocalPlayer.Scripted { on = true, fire = false };
            yield return new WaitForSecondsRealtime(0.15f);
        }
        IEnumerator WaitDetonation(float max)
        {
            float t0 = Time.realtimeSinceStartup;
            while (FindObjectsByType<AgniThrowable>(FindObjectsSortMode.None).Length == 0 && Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
            while (FindObjectsByType<AgniThrowable>(FindObjectsSortMode.None).Length > 0 && Time.realtimeSinceStartup - t0 < max) yield return null;
        }

        IEnumerator Grenade()
        {
            yield return WaitPlaying(0f);
            yield return new WaitForSecondsRealtime(2.5f);
            var lp = AgniLocalPlayer.Instance; var me = lp.P;
            { var z = AgniZone.Instance; string g = Physics.Raycast(me.transform.position + Vector3.up * 0.2f, Vector3.down, out var gh, 50, ~0, QueryTriggerInteraction.Ignore) ? gh.collider.name + "@" + gh.point.y.ToString("0.00") : "none";
              Log("diag pos=" + me.transform.position + " arenaR=" + AgniMatch.ArenaRadius + " zoneC=" + (z ? z.CenterNow().ToString() : "-") + " zoneR=" + (z ? z.RadiusNow().ToString("0") : "-") + " inside=" + (z ? z.Inside(me.transform.position) : false) + " ground=" + g); }
            Log("local " + me.Nick + " team " + me.Team.Value + " players " + AgniPlayer.All.Count + " gola=" + me.Count(ThrowKind.Gola));
            // an ENEMY ~12 m in front (passive bot moved there server-side) so the blast lands on someone
            AgniPlayer target = null; float bd = 1e9f;
            foreach (var p in AgniPlayer.All) if (p != me && p.IsBot.Value && p.Team.Value != me.Team.Value && p.Alive) { float dd = Vector3.Distance(p.transform.position, me.transform.position); if (dd < bd) { bd = dd; target = p; } }
            if (target != null && AgniNet.IsServer)
            {
                var fwd = Quaternion.Euler(0, lp.Yaw, 0) * Vector3.forward; var at = AgniMatch.Ground(me.transform.position + fwd * 12f);
                target.ServerResetForMatch(at, lp.Yaw + 180f); yield return new WaitForSecondsRealtime(0.3f);
            }
            if (target != null) { Face(target.transform.position, -4); Log("target " + target.Nick + " d=" + Vector3.Distance(target.transform.position, me.transform.position).ToString("0.0") + " hp=" + target.Hp.Value); }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("20_arena_ready");
            // 1) Gola: cook 1.4 s with the arc preview, throw, catch the blast
            float gd = target != null ? Vector3.Distance(target.transform.position, me.transform.position) : 12f;
            float gp = BestPitch(ThrowKind.Gola, gd); Log("gola pitch " + gp + " for " + gd.ToString("0.0") + " m");
            yield return Throw(ThrowKind.Gola, 1.4f, gp, "21_gola_arc_cook");
            yield return WaitDetonation(5);
            yield return Shot("22_gola_blast");
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled && r.bounds.size.magnitude > 18f && !r.isPartOfStaticBatch && r.GetComponentInParent<AgniPlayer>() == null && r.gameObject.scene.name != "AgniArena") Log("big renderer " + r.name + " " + r.GetType().Name + " size=" + r.bounds.size + " at " + r.bounds.center);
            yield return new WaitForSecondsRealtime(0.25f); yield return Shot("23_gola_blast_b");
            if (target != null) Log("after gola: target hp=" + target.Hp.Value + " knock=" + target.Knock.Value + " life=" + target.Life.Value);
            yield return new WaitForSecondsRealtime(1.5f);
            // 2) Dhuan
            yield return Throw(ThrowKind.Dhuan, 0.5f, BestPitch(ThrowKind.Dhuan, 14), null);
            yield return WaitDetonation(4); yield return new WaitForSecondsRealtime(2.2f); yield return Shot("24_dhuan_smoke");
            // 3) Agni Deewar: turn 90° so the wall is seen side-on
            lp.Yaw += 70; yield return Throw(ThrowKind.Deewar, 0.6f, BestPitch(ThrowKind.Deewar, 7), "25_deewar_aim");
            yield return WaitDetonation(3); yield return new WaitForSecondsRealtime(0.55f); yield return Shot("26_deewar_wall");
            // 4) Patakha at our own feet-ish, looking at it
            lp.Yaw -= 160; yield return Throw(ThrowKind.Patakha, 0.3f, BestPitch(ThrowKind.Patakha, 9), null);
            yield return WaitDetonation(4); yield return Frames(3); yield return Shot("27_patakha_flash");
            yield return new WaitForSecondsRealtime(1.6f); yield return Shot("28_patakha_recover");
            lp.script = default;
            Log("end: hp=" + me.Hp.Value + " gola=" + me.Count(ThrowKind.Gola) + " walls=" + FindObjectsByType<AgniWall>(FindObjectsSortMode.None).Length);
        }

        // ------------------------------------------------------------------ net (2 processes)
        IEnumerator NetTest()
        {
            yield return WaitPlaying(1.0f);
            var lp = AgniLocalPlayer.Instance; var me = lp.P;
            AgniPlayer other = null; float t0 = Time.realtimeSinceStartup;
            while (other == null && Time.realtimeSinceStartup - t0 < 10) { foreach (var p in AgniPlayer.All) if (p != me && !p.IsBot.Value) other = p; if (other == null) yield return null; }
            int humans = 0; foreach (var p in AgniPlayer.All) if (!p.IsBot.Value) humans++;
            Log(tagName + " role=" + AgniNet.Role + " clientId=" + AgniNet.NM.LocalClientId + " me=" + me.Nick + " team=" + me.Team.Value + " players=" + AgniPlayer.All.Count + " humans=" + humans + " rtt=" + AgniNet.Ping());
            if (other == null) { Log(tagName + " NO OTHER HUMAN"); yield break; }
            if (AgniNet.IsServer)
            {   // squads land far apart; the server brings the client 14 m in front of the host so both cameras see each other
                var fwd = Quaternion.Euler(0, lp.Yaw, 0) * Vector3.forward;
                other.ServerResetForMatch(AgniMatch.Ground(me.transform.position + fwd * 14f), lp.Yaw + 180f);
                Log(tagName + " moved " + other.Nick + " (team " + other.Team.Value + ") 14 m in front");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            Face(other.transform.position, 4);
            yield return new WaitForSecondsRealtime(0.6f);
            Log(tagName + " sees " + other.Nick + " team=" + other.Team.Value + " d=" + Vector3.Distance(other.transform.position, me.transform.position).ToString("0.0") + " hp=" + other.Hp.Value);
            yield return Shot(tagName + "_a_sees_other");
            if (AgniNet.Role == NetRole.Client)
            {   // client throws a GOLA at the host: input + throw go to the server, the server flies it and applies damage
                float dd = Vector3.Distance(other.transform.position, me.transform.position);
                yield return Throw(ThrowKind.Gola, 0.8f, BestPitch(ThrowKind.Gola, dd), tagName + "_b_arc");
                yield return WaitDetonation(6); yield return Shot(tagName + "_c_blast");
                yield return new WaitForSecondsRealtime(0.8f);
                Log(tagName + " host hp seen by client = " + other.Hp.Value + " life=" + other.Life.Value);
            }
            else
            {
                float hp0 = me.Hp.Value; float tw = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tw < 15f && FindObjectsByType<AgniThrowable>(FindObjectsSortMode.None).Length == 0) { Face(other.transform.position, 4); yield return null; }
                Log(tagName + " thrown GOLA from client in the air: " + (FindObjectsByType<AgniThrowable>(FindObjectsSortMode.None).Length > 0));
                yield return Shot(tagName + "_b_incoming");
                yield return WaitDetonation(6); yield return Shot(tagName + "_c_blast");
                yield return new WaitForSecondsRealtime(0.8f);
                Log(tagName + " my hp " + hp0 + " -> " + me.Hp.Value + " knock=" + me.Knock.Value + " life=" + (LifeState)me.Life.Value);
            }
            Log(tagName + " zone r=" + (AgniZone.Instance != null ? AgniZone.Instance.RadiusNow().ToString("0.0") : "-") + " rtt=" + AgniNet.Ping());
            yield return new WaitForSecondsRealtime(0.5f); yield return Shot(tagName + "_d");
            if (AgniNet.Role == NetRole.Host) yield return new WaitForSecondsRealtime(4f); // let the client finish first
        }


        // ------------------------------------------------------------------ ugs (REAL online: Auth + Friends + Lobby/Relay)
        // Two separate copies: -agniUgsRole host|join, -agniCodeFile <path> (the host writes the room code there; the joiner
        // writes its friend ID to <path>.join). Different -agniProfile per copy = two different anonymous UGS players.
        IEnumerator UgsTest()
        {
            string role = AgniNetConfig.Arg("-agniUgsRole") ?? "host"; string codeFile = AgniNetConfig.Arg("-agniCodeFile") ?? "/tmp/agni_ugs_code.txt";
            AgniProfile.Name = AgniNetConfig.Arg("-agniName") ?? (role == "host" ? "SujonHost" : "RaniJoin");
            AgniLobby lobby = null; float t0 = Time.realtimeSinceStartup;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.realtimeSinceStartup - t0 < 60) yield return null;
            t0 = Time.realtimeSinceStartup;
            while (AgniOnline.State != OnlineState.Online && AgniOnline.State != OnlineState.Failed && AgniOnline.State != OnlineState.NoProject && Time.realtimeSinceStartup - t0 < 45) yield return null;
            Log(role + " online=" + AgniOnline.State + " player=" + AgniOnline.PlayerId + " friendId=" + AgniOnline.FriendCode + " err=" + AgniOnline.Error + " backend=" + AgniSocial.Friends.Backend);
            if (AgniOnline.State != OnlineState.Online) { yield return Shot("ugs_" + role + "_offline"); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            var party = AgniSocial.Party;
            if (role == "host")
            {
                if (File.Exists(codeFile)) File.Delete(codeFile); if (File.Exists(codeFile + ".join")) File.Delete(codeFile + ".join");
                party.Create(); t0 = Time.realtimeSinceStartup;
                while ((!party.InParty || string.IsNullOrEmpty(party.Code)) && Time.realtimeSinceStartup - t0 < 40) yield return null;
                Log("host room code=" + party.Code + " status=" + party.Status + " netRole=" + AgniNet.Role);
                if (!party.InParty) { yield return Shot("ugs_host_noroom"); yield break; }
                File.WriteAllText(codeFile, party.Code);
                var panel = lobby.OpenFriends(AgniFriendsPanel.Tab.Room); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("ugs_host_1_room");
                t0 = Time.realtimeSinceStartup; while (party.Members.Count < 2 && Time.realtimeSinceStartup - t0 < 90) yield return null;
                Log("host members=" + party.Members.Count + " : " + string.Join(", ", System.Linq.Enumerable.Select(party.Members, m => m.name + (m.leader ? "(leader)" : "") + (m.ready ? "[R]" : ""))));
                // friend request to the joiner, by its real UGS friend ID
                t0 = Time.realtimeSinceStartup; while (!File.Exists(codeFile + ".join") && Time.realtimeSinceStartup - t0 < 40) yield return null;
                if (File.Exists(codeFile + ".join")) { var fid = File.ReadAllText(codeFile + ".join").Trim(); var e = AgniSocial.Friends.SendRequest(fid); Log("host friend request -> " + fid + " : " + (e ?? "sent")); panel.Toast(e ?? "Request bheji: " + fid); }
                yield return new WaitForSecondsRealtime(1.5f); yield return Shot("ugs_host_2_squad_request");
                t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < 40) { bool ok = AgniSocial.Friends.Friends.Count > 0; foreach (var m in party.Members) if (!m.leader && !m.ready) ok = false; if (ok && party.Members.Count >= 2) break; yield return null; }
                Log("host friends=" + AgniSocial.Friends.Friends.Count + (AgniSocial.Friends.Friends.Count > 0 ? " first=" + AgniSocial.Friends.Friends[0].name + " " + AgniSocial.Friends.Friends[0].presence : "") + " allReady=" + party.AllReady);
                panel.Show(AgniFriendsPanel.Tab.Dost); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("ugs_host_3_friend_online");
                panel.Close(); yield return new WaitForSecondsRealtime(0.8f); yield return Shot("ugs_host_4_lobby_ready");
                party.StartMatch(); Log("host START status=" + party.Status);
            }
            else
            {
                t0 = Time.realtimeSinceStartup; while (!File.Exists(codeFile) && Time.realtimeSinceStartup - t0 < 90) yield return null;
                var code = File.Exists(codeFile) ? File.ReadAllText(codeFile).Trim() : ""; Log("join code=" + code);
                var panel = lobby.OpenFriends(AgniFriendsPanel.Tab.Room);
                party.JoinByCode(code); t0 = Time.realtimeSinceStartup;
                while ((!party.InParty || party.Members.Count < 2) && Time.realtimeSinceStartup - t0 < 40) yield return null;
                Log("join inParty=" + party.InParty + " members=" + party.Members.Count + " status=" + party.Status + " netRole=" + AgniNet.Role + " connected=" + (AgniNet.NM != null && AgniNet.NM.IsConnectedClient));
                yield return new WaitForSecondsRealtime(1.5f); yield return Shot("ugs_join_1_in_squad");
                File.WriteAllText(codeFile + ".join", AgniOnline.FriendCode);
                t0 = Time.realtimeSinceStartup; while (AgniSocial.Friends.Requests.Count == 0 && Time.realtimeSinceStartup - t0 < 40) yield return null;
                panel.Show(AgniFriendsPanel.Tab.Requests); yield return new WaitForSecondsRealtime(1.2f); yield return Shot("ugs_join_2_request");
                FriendRequest req = null; foreach (var q in AgniSocial.Friends.Requests) if (q.incoming) req = q;
                Log("join requests=" + AgniSocial.Friends.Requests.Count + (req != null ? " from " + req.name : ""));
                if (req != null) AgniSocial.Friends.Accept(req.playerId);
                yield return new WaitForSecondsRealtime(3f);
                party.SetReady(true); panel.Show(AgniFriendsPanel.Tab.Dost); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("ugs_join_3_friends");
                panel.Close();
            }
            // both: the leader's START takes the squad into the arena over Relay
            yield return WaitPlaying(1.5f);
            var lp = AgniLocalPlayer.Instance; if (lp == null) { Log(role + " never reached the match"); yield return Shot("ugs_" + role + "_nomatch"); yield break; }
            var me = lp.P; AgniPlayer other = null; foreach (var p in AgniPlayer.All) if (p != me && !p.IsBot.Value) other = p;
            int humans = 0; foreach (var p in AgniPlayer.All) if (!p.IsBot.Value) humans++;
            Log(role + " MATCH over relay: netRole=" + AgniNet.Role + " me=" + me.Nick + " team=" + me.Team.Value + " players=" + AgniPlayer.All.Count + " humans=" + humans + " rtt=" + AgniNet.Ping() + (other != null ? " mate=" + other.Nick + " team=" + other.Team.Value + " d=" + Vector3.Distance(other.transform.position, me.transform.position).ToString("0.0") : ""));
            if (other != null) Face(other.transform.position, 3);
            yield return new WaitForSecondsRealtime(1.0f); yield return Shot("ugs_" + role + "_5_match");
            yield return new WaitForSecondsRealtime(role == "host" ? 6f : 2f);
        }

        // ------------------------------------------------------------------ capture
        void SetSize(int w, int h)
        {
            W = w; H = h;
            if (rt != null) { foreach (var c in Camera.allCameras) if (c.targetTexture == rt) c.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        }
        static bool Overlay(Camera c) { var d = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); return d != null && d.renderType == UnityEngine.Rendering.Universal.CameraRenderType.Overlay; }
        Texture2D RenderAll()
        {
            var cams = Camera.allCameras; System.Array.Sort(cams, (a, b) => a.depth.CompareTo(b.depth));
            var overlays = new List<Camera>(); var restore = new List<(Camera, RenderTexture)>();
            foreach (var c in cams) { if (Overlay(c)) { overlays.Add(c); continue; } restore.Add((c, c.targetTexture)); c.targetTexture = rt; c.Render(); }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); RenderTexture.active = null;
            if (overlays.Count > 0)
            {
                if (rtUI == null) rtUI = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                var world = tex.GetPixels32(); var ui = new Texture2D(W, H, TextureFormat.RGBA32, false);
                foreach (var c in overlays)
                {
                    var d = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    Camera baseCam = null; foreach (var b in cams) { var bd = b.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (bd != null && bd.cameraStack != null && bd.cameraStack.Contains(c)) { baseCam = b; bd.cameraStack.Remove(c); } }
                    var cf = c.clearFlags; var bg = c.backgroundColor; var tt = c.targetTexture;
                    d.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0, 0, 0, 0);
                    c.targetTexture = rtUI; c.Render(); c.targetTexture = tt;
                    d.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay; c.clearFlags = cf; c.backgroundColor = bg;
                    if (baseCam != null) baseCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().cameraStack.Add(c);
                    RenderTexture.active = rtUI; ui.ReadPixels(new Rect(0, 0, W, H), 0, 0); RenderTexture.active = null;
                    var u = ui.GetPixels32();
                    for (int i = 0; i < world.Length; i++)
                    {
                        int a = u[i].a; if (a == 0) continue; int ia = 255 - a;
                        world[i].r = (byte)Mathf.Min(255, u[i].r + world[i].r * ia / 255);
                        world[i].g = (byte)Mathf.Min(255, u[i].g + world[i].g * ia / 255);
                        world[i].b = (byte)Mathf.Min(255, u[i].b + world[i].b * ia / 255);
                    }
                }
                Destroy(ui); tex.SetPixels32(world);
            }
            foreach (var (c, t) in restore) if (c != null) c.targetTexture = t;
            tex.Apply(); return tex;
        }
        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        IEnumerator Shot(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var tex = RenderAll(); var p = Path.Combine(dir, name + ".png"); File.WriteAllBytes(p, tex.EncodeToPNG()); Destroy(tex);
            Log("wrote " + p);
        }
        static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
