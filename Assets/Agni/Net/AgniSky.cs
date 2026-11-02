// Agni Royale — THE DROP. Every match starts on board the "AGNI AIR" plane, which flies a random straight line across the
// India map. KUDO (or JUMP / Space) jumps whenever you want; the freefall and the parachute are steered with the joystick to
// any spot (server-authoritative, like walking). The chute opens by itself low over the ground. Bot squads pick their own
// spots and jump at different points along the line; bots in a human's squad jump with that human and land beside them.
// -agniNoPlane keeps the old "squads start on the ground next to a house" start (proof scripts that need a fixed spot).
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum AirState : byte { Ground = 0, Plane = 1, Freefall = 2, Chute = 3 }

    public static class AgniSkyCfg
    {
        public const float PlaneAlt = 470f;      // m above sea level (v0.2.2: smaller map, lower plane, faster drop; the Himalaya top out near 140)
        public const float PlaneSpeed = 90f;     // m/s: about 40 s from coast to coast
        public const float LeadIn = 3f;          // s on board before the door opens
        public const float FallSpeed = 55f, DiveSpeed = 75f, FallSteer = 26f;   // v0.2.1: faster, Free Fire-like drop
        public const float ChuteSpeed = 10f, ChuteSteer = 14f, ChuteOpenAGL = 85f;
    }

    public partial class AgniMatch
    {
        public NetworkVariable<Vector3> PlaneA = new NetworkVariable<Vector3>(), PlaneB = new NetworkVariable<Vector3>();
        public NetworkVariable<double> PlaneT0 = new NetworkVariable<double>(-1);
        public bool HasPlane => PlaneT0.Value >= 0;
        public float PlaneLen => Vector3.Distance(PlaneA.Value, PlaneB.Value);
        public Vector3 PlaneDir { get { var d = PlaneB.Value - PlaneA.Value; return d.sqrMagnitude > 1 ? d.normalized : Vector3.forward; } }
        public double PlaneEndT => PlaneT0.Value + PlaneLen / AgniSkyCfg.PlaneSpeed;
        public Vector3 PlanePos(double t) { float len = Mathf.Max(1f, PlaneLen); float k = (float)((t - PlaneT0.Value) * AgniSkyCfg.PlaneSpeed / len); return Vector3.LerpUnclamped(PlaneA.Value, PlaneB.Value, Mathf.Max(0f, k)); }
        public double NowT => NetworkManager != null ? AgniNet.Now : 0;
        public bool DoorOpen => HasPlane && NowT >= PlaneT0.Value;

        /// server: a random line across the map (through the middle third), the whole lobby on board
        void ServerPlanStart(List<List<AgniPlayer>> teams, List<Vector3> spots, System.Random rnd)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); var perp = new Vector3(-d.z, 0, d.x);
            float off = ((float)rnd.NextDouble() * 2f - 1f) * ArenaRadius * 0.33f, half = ArenaRadius * 1.05f;
            var a = perp * off - d * half + Vector3.up * AgniSkyCfg.PlaneAlt; var b = perp * off + d * half + Vector3.up * AgniSkyCfg.PlaneAlt;
            PlaneA.Value = a; PlaneB.Value = b; PlaneT0.Value = NowT + AgniSkyCfg.LeadIn;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, len = Vector3.Distance(a, b);
            for (int ti = 0; ti < teams.Count; ti++)
            {
                AgniPlayer leader = null; foreach (var p in teams[ti]) if (!p.IsBot.Value) { leader = p; break; }
                // bot squads: their own spot, pulled to within gliding reach of the line; jump a little before passing it
                var spot = ti < spots.Count ? spots[ti] : Vector3.zero; var rel = spot - a; rel.y = 0;
                float along = Vector3.Dot(rel, d), side = Vector3.Dot(rel, perp) - off; float reach = 420f + 160f * (float)rnd.NextDouble();
                if (Mathf.Abs(side) > reach) { spot -= perp * (Mathf.Sign(side) * (Mathf.Abs(side) - reach)); side = Mathf.Sign(side) * reach; }
                along = Mathf.Clamp(along, 120f, len - 120f);
                float jumpS = Mathf.Clamp(along - 90f - 0.35f * Mathf.Abs(side) + ((float)rnd.NextDouble() - 0.5f) * 160f, 40f, len - 60f);
                double jumpAt = PlaneT0.Value + jumpS / AgniSkyCfg.PlaneSpeed;
                for (int k = 0; k < teams[ti].Count; k++)
                {
                    var p = teams[ti][k];
                    p.ServerResetForMatch(PlanePos(PlaneT0.Value - AgniSkyCfg.LeadIn), yaw);
                    p.ServerBoardPlane();
                    p.followLeader = p.IsBot.Value ? leader : null;
                    p.followOffset = new Vector3(Mathf.Cos(k * 1.9f + 0.6f), 0, Mathf.Sin(k * 1.9f + 0.6f)) * (5f + k * 1.5f);
                    p.botTarget = Ground(spot + new Vector3(Mathf.Cos(k * 1.57f), 0, Mathf.Sin(k * 1.57f)) * 3f);
                    p.botJumpAt = p.IsBot.Value && leader == null ? jumpAt + k * 0.45 + rnd.NextDouble() * 0.3 : -1;
                }
            }
            Debug.Log("[AgniSky] plane " + a.ToString("F0") + " -> " + b.ToString("F0") + " (" + (len / AgniSkyCfg.PlaneSpeed).ToString("F0") + " s), " + teams.Count + " squads on board");
        }
    }

    public partial class AgniPlayer
    {
        public NetworkVariable<byte> Air = new NetworkVariable<byte>();
        public bool InPlane => Air.Value == (byte)AirState.Plane;
        public bool Airborne => Air.Value == (byte)AirState.Freefall || Air.Value == (byte)AirState.Chute;
        public bool InAir => Air.Value != (byte)AirState.Ground;
        [System.NonSerialized] public double botJumpAt = -1; [System.NonSerialized] public Vector3 botTarget, followOffset; [System.NonSerialized] public AgniPlayer followLeader;
        float airVy;

        public void ServerBoardPlane() { Air.Value = (byte)AirState.Plane; cc.enabled = false; vy = 0; airVy = 0; }

        public void ServerLeavePlane()
        {
            var m = AgniMatch.Instance; if (!InPlane || m == null || !m.DoorOpen || Dead) return;
            double now = m.NowT; var d = m.PlaneDir;
            var pos = m.PlanePos(now) - d * 7f + Vector3.down * 4f + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
            transform.position = pos; cc.enabled = true; Air.Value = (byte)AirState.Freefall; airVy = -12f;
            if (!IsBot.Value)
            {   // the squad jumps with its human
                int i = 0; foreach (var q in All) if (q.IsBot.Value && q.followLeader == this && q.InPlane) q.botJumpAt = now + 0.35 + 0.3 * i++;
                Debug.Log("[AgniSky] " + Nick + " jumped at " + pos.ToString("F0"));
            }
        }

        /// server tick while in the plane / falling / under the chute. Returns false on the ground.
        bool ServerSky(float dt, AgniInput inp)
        {
            if (Air.Value == (byte)AirState.Ground) return false;
            var m = AgniMatch.Instance;
            if (m == null || !m.HasPlane) { if (InPlane) { cc.enabled = true; Air.Value = 0; } return false; }
            double now = m.NowT;
            if (InPlane)
            {
                transform.position = m.PlanePos(now);
                if (now >= m.PlaneEndT - 1.0) ServerLeavePlane();                       // the line ends: everyone still aboard is pushed out
                else if (IsBot.Value && botJumpAt > 0 && now >= botJumpAt) ServerLeavePlane();
                return true;
            }
            bool chute = Air.Value == (byte)AirState.Chute; float steer = chute ? AgniSkyCfg.ChuteSteer : AgniSkyCfg.FallSteer;
            Vector3 wish;
            if (IsBot.Value)
            {
                var tgt = followLeader != null && !followLeader.Dead ? followLeader.transform.position + followOffset : botTarget;
                var to = tgt - transform.position; to.y = 0; float dist = to.magnitude;
                wish = dist > 0.5f ? to / dist * Mathf.Min(steer, dist * 0.8f) : Vector3.zero;
                if (dist > 2f) transform.rotation = Quaternion.LookRotation(to);
            }
            else
            {
                var mv = Vector2.ClampMagnitude(inp.move, 1f);
                wish = Quaternion.Euler(0, inp.yaw, 0) * new Vector3(mv.x, 0, mv.y) * steer;
            }
            float agl = transform.position.y - AgniMatch.Ground(transform.position).y;
            if (!chute)
            {
                bool dive = !IsBot.Value && inp.move.y > 0.75f;
                airVy = Mathf.MoveTowards(airVy, -(dive ? AgniSkyCfg.DiveSpeed : AgniSkyCfg.FallSpeed), 40f * dt);
                if (agl < AgniSkyCfg.ChuteOpenAGL) { Air.Value = (byte)AirState.Chute; ChuteFxRpc(); }
            }
            else airVy = Mathf.MoveTowards(airVy, -AgniSkyCfg.ChuteSpeed, 45f * dt);
            cc.Move((wish + Vector3.up * airVy) * dt);
            if (cc.isGrounded || agl < 0.35f) { Air.Value = 0; vy = -2f; airVy = 0; LandFxRpc(); }
            return true;
        }
        [Rpc(SendTo.ClientsAndHost)] void ChuteFxRpc() { if (this == Local) Agni.UI.AgniSfx.Play("sfx_popup_open", 0.9f); }
        [Rpc(SendTo.ClientsAndHost)] void LandFxRpc() { if (view != null) view.PlayJump(); }
    }

    /// client: the plane in the sky, everyone hidden while on board, freefall pose and the parachute canopy
    [DefaultExecutionOrder(5000)]
    public class AgniSkyFx : MonoBehaviour
    {
        static AgniSkyFx inst;
        public static void Ensure() { if (inst == null) { inst = new GameObject("AgniSkyFx").AddComponent<AgniSkyFx>(); } }
        Transform plane, prop1, prop2;
        readonly Dictionary<AgniPlayer, Transform> chutes = new Dictionary<AgniPlayer, Transform>();
        readonly Dictionary<AgniPlayer, byte> shown = new Dictionary<AgniPlayer, byte>();

        static Material Mat(Color c) { var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); var m = new Material(sh); m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Smoothness", 0.45f); return m; }
        static Transform Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t); var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localEulerAngles = euler; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g.transform;
        }
        void BuildPlane()
        {
            plane = new GameObject("AgniAir_Plane").transform; plane.SetParent(transform, false);
            Material body = Mat(new Color(0.96f, 0.93f, 0.86f)), saffron = Mat(new Color(1f, 0.42f, 0.12f)), navy = Mat(new Color(0.11f, 0.15f, 0.32f)), glass = Mat(new Color(0.15f, 0.2f, 0.25f));
            Prim(PrimitiveType.Capsule, plane, Vector3.zero, new Vector3(90, 0, 0), new Vector3(4.2f, 15f, 4.2f), body);              // fuselage (30 m)
            Prim(PrimitiveType.Capsule, plane, new Vector3(0, -0.2f, 0.4f), new Vector3(90, 0, 0), new Vector3(4.28f, 9f, 1.4f), saffron).localPosition = new Vector3(0, -0.6f, 0); // belly stripe
            Prim(PrimitiveType.Sphere, plane, new Vector3(0, 0.9f, 13.2f), Vector3.zero, new Vector3(2.4f, 1.1f, 2.2f), glass);       // cockpit
            Prim(PrimitiveType.Cube, plane, new Vector3(0, -0.4f, 1.5f), new Vector3(0, 0, 0), new Vector3(34f, 0.5f, 5.2f), navy);     // wings
            Prim(PrimitiveType.Cube, plane, new Vector3(0, 0.5f, -12.5f), Vector3.zero, new Vector3(11f, 0.4f, 3f), navy);            // tailplane
            Prim(PrimitiveType.Cube, plane, new Vector3(0, 3.2f, -12.8f), new Vector3(-15, 0, 0), new Vector3(0.45f, 5.5f, 3.6f), saffron); // fin
            for (int s = -1; s <= 1; s += 2)
            {
                var eng = Prim(PrimitiveType.Capsule, plane, new Vector3(s * 7.5f, -1.3f, 3.2f), new Vector3(90, 0, 0), new Vector3(1.6f, 2.6f, 1.6f), body);
                var pr = Prim(PrimitiveType.Cube, plane, new Vector3(s * 7.5f, -1.3f, 5.9f), Vector3.zero, new Vector3(5.5f, 0.35f, 0.12f), navy);
                if (s < 0) prop1 = pr; else prop2 = pr;
            }
        }
        Transform BuildChute()
        {
            var root = new GameObject("Chute").transform; root.SetParent(transform, false);
            Material a = Mat(new Color(1f, 0.45f, 0.12f)), b = Mat(new Color(0.98f, 0.95f, 0.88f)), line = Mat(new Color(0.2f, 0.2f, 0.2f));
            Prim(PrimitiveType.Sphere, root, new Vector3(0, 5.4f, 0), Vector3.zero, new Vector3(6.4f, 1.9f, 4.6f), a);
            Prim(PrimitiveType.Sphere, root, new Vector3(0, 5.25f, 0), Vector3.zero, new Vector3(3.2f, 1.9f, 4.65f), b);
            for (int i = 0; i < 4; i++) { float x = (i < 2 ? -1 : 1) * 2.6f, z = (i % 2 == 0 ? -1 : 1) * 1.6f; var l = Prim(PrimitiveType.Cylinder, root, new Vector3(x * 0.5f, 3.4f, z * 0.5f), Vector3.zero, new Vector3(0.04f, 1.95f, 0.04f), line); l.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(x, 3.9f, z)); }
            return root;
        }

        void LateUpdate()
        {
            var m = AgniMatch.Instance; if (m == null) { if (plane != null) plane.gameObject.SetActive(false); return; }
            double now = m.NowT;
            bool fly = m.HasPlane && now < m.PlaneEndT + 6.0;
            if (fly && plane == null) BuildPlane();
            if (plane != null)
            {
                plane.gameObject.SetActive(fly);
                if (fly) { plane.SetPositionAndRotation(m.PlanePos(now), Quaternion.LookRotation(m.PlaneDir)); float spin = Time.time * 1700f; prop1.localRotation = Quaternion.Euler(0, 0, spin); prop2.localRotation = Quaternion.Euler(0, 0, -spin); }
            }
            foreach (var p in AgniPlayer.All)
            {
                if (p == null || p.view == null) continue;
                byte air = p.Air.Value; var v = p.view.transform;
                bool hide = air == (byte)AirState.Plane;
                if (p.view.gameObject.activeSelf == hide) p.view.gameObject.SetActive(!hide);
                if (air == (byte)AirState.Freefall)
                {   // belly-down skydive pose, pivot at the hips
                    var r = Quaternion.Euler(72f, 0, 0); v.localRotation = r; v.localPosition = Vector3.up * 0.9f - r * (Vector3.up * 0.9f);
                }
                else if (shown.TryGetValue(p, out var was) && was != air) { v.localRotation = Quaternion.identity; v.localPosition = Vector3.zero; }
                shown[p] = air;
                chutes.TryGetValue(p, out var ch);
                if (air == (byte)AirState.Chute)
                {
                    if (ch == null) { ch = BuildChute(); chutes[p] = ch; }
                    ch.gameObject.SetActive(true); ch.SetPositionAndRotation(p.transform.position, p.transform.rotation);
                }
                else if (ch != null) ch.gameObject.SetActive(false);
            }
        }
    }
}
