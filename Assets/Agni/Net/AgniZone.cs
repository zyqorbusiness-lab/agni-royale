// Agni Royale — "AGNI REKHA" shrinking safe zone (server decides, every client draws the same circle).
// Phase plan (GAME_FLOW_SPEC §8): wait / shrink / dps — P1 120/60/1 · P2 90/50/2 · P3 75/40/4 · P4 60/35/6 · P5 45/30/8 · P6 30/25/12, then closes.
// -agniZoneScale 0.2 runs the same plan 5× faster for tests.
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public class AgniZone : NetworkBehaviour
    {
        public static AgniZone Instance;
        static readonly float[,] Plan = { { 120, 60, 1, 0.70f }, { 90, 50, 2, 0.48f }, { 75, 40, 4, 0.30f }, { 60, 35, 6, 0.17f }, { 45, 30, 8, 0.08f }, { 30, 25, 12, 0.025f }, { 20, 20, 16, 0f } };
        public NetworkVariable<Vector3> FromC = new NetworkVariable<Vector3>(), ToC = new NetworkVariable<Vector3>();
        public NetworkVariable<float> FromR = new NetworkVariable<float>(), ToR = new NetworkVariable<float>();
        public NetworkVariable<double> ShrinkStart = new NetworkVariable<double>(), ShrinkEnd = new NetworkVariable<double>();
        public NetworkVariable<int> PhaseIndex = new NetworkVariable<int>(-1);
        public NetworkVariable<float> Dps = new NetworkVariable<float>();
        double tickAt; AgniZoneView view;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) { float R = AgniMatch.ArenaRadius * 1.15f; FromC.Value = ToC.Value = Vector3.zero; FromR.Value = ToR.Value = R; ShrinkStart.Value = ShrinkEnd.Value = 0; }
            if (NetworkManager.IsClient) view = AgniZoneView.Create(this);
        }
        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; if (view != null) Destroy(view.gameObject); }

        double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0;
        public float T01 { get { double a = ShrinkStart.Value, b = ShrinkEnd.Value; return b <= a ? (Now >= b ? 1f : 0f) : Mathf.Clamp01((float)((Now - a) / (b - a))); } }
        public Vector3 CenterNow() => Vector3.Lerp(FromC.Value, ToC.Value, T01);
        public float RadiusNow() => Mathf.Lerp(FromR.Value, ToR.Value, T01);
        public bool Inside(Vector3 p) { var c = CenterNow(); p.y = c.y = 0; return (p - c).sqrMagnitude <= RadiusNow() * RadiusNow(); }
        /// seconds until the next wait/shrink boundary and whether it is currently shrinking
        public float SecondsToNext(out bool shrinking) { double n = Now; shrinking = n >= ShrinkStart.Value && n < ShrinkEnd.Value; return Mathf.Max(0, (float)((shrinking ? ShrinkEnd.Value : ShrinkStart.Value) - n)); }

        public void ServerBegin() { NextPhase(0); }
        void NextPhase(int i)
        {
            if (i >= Plan.GetLength(0)) return;
            float s = AgniNetConfig.ZoneScale; var c0 = CenterNow(); float r0 = RadiusNow();
            float r1 = AgniMatch.ArenaRadius * Plan[i, 3];
            var off = Random.insideUnitCircle * Mathf.Max(0, r0 - r1) * 0.85f; var c1 = new Vector3(c0.x + off.x, 0, c0.z + off.y);
            FromC.Value = c0; FromR.Value = r0; ToC.Value = c1; ToR.Value = r1;
            ShrinkStart.Value = Now + Plan[i, 0] * s; ShrinkEnd.Value = ShrinkStart.Value + Plan[i, 1] * s;
            PhaseIndex.Value = i; Dps.Value = Plan[i, 2];
        }
        void Update()
        {
            if (!IsServer || !IsSpawned || PhaseIndex.Value < 0) return;
            if (Now >= ShrinkEnd.Value && PhaseIndex.Value < Plan.GetLength(0) - 1) NextPhase(PhaseIndex.Value + 1);
            if (Now < tickAt) return; tickAt = Now + 1.0;
            var m = AgniMatch.Instance; if (m == null || m.Phase.Value != (byte)MatchPhase.Playing) return;
            // damage outside grows the longer the phase has run (+ a little each phase)
            foreach (var p in AgniPlayer.All.ToArray()) if (!p.Dead && !Inside(p.transform.position)) p.ApplyDamage(Dps.Value * (p.Knocked ? 1.5f : 1f), null, "Agni Rekha", false, CenterNow());
        }
    }

    /// Client visual: a tall heat-haze curtain at the edge (saffron at the base, fading to clear upward) + a thin white
    /// ring on the ground for the next circle. Natural, not a neon wall.
    public class AgniZoneView : MonoBehaviour
    {
        AgniZone z; Transform curtain; LineRenderer next; Material mat; Vector3 lastNC = Vector3.one * 1e9f; float lastNR = -1;
        public static AgniZoneView Create(AgniZone z)
        {
            var go = new GameObject("AgniRekha"); var v = go.AddComponent<AgniZoneView>(); v.z = z;
            var c = new GameObject("Curtain"); c.transform.SetParent(go.transform, false); v.curtain = c.transform;
            c.AddComponent<MeshFilter>().sharedMesh = AgniFx.CylinderMesh(96, 1f, 1f);
            var mr = c.AddComponent<MeshRenderer>(); v.mat = AgniFx.ZoneMaterial(); mr.sharedMaterial = v.mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var n = new GameObject("NextRing"); n.transform.SetParent(go.transform, false); v.next = n.AddComponent<LineRenderer>();
            v.next.loop = true; v.next.positionCount = 128; v.next.widthMultiplier = 0.35f; v.next.useWorldSpace = true; v.next.material = AgniFx.LineMaterial(); v.next.startColor = v.next.endColor = new Color(1, 1, 1, 0.75f);
            return v;
        }
        void LateUpdate()
        {
            if (z == null) { Destroy(gameObject); return; }
            var c = z.CenterNow(); float r = Mathf.Max(0.5f, z.RadiusNow());
            curtain.position = new Vector3(c.x, -20f, c.z); curtain.localScale = new Vector3(r, 140f, r);
            mat.SetFloat("_Scroll", Time.time * 0.03f); mat.SetFloat("_Tiling", Mathf.Max(4f, r / 6f));
            bool show = z.PhaseIndex.Value >= 0; next.enabled = show;
            if (show && (z.ToC.Value != lastNC || z.ToR.Value != lastNR)) { var nc = lastNC = z.ToC.Value; float nr = lastNR = z.ToR.Value; next.widthMultiplier = Mathf.Clamp(nr / 150f, 0.15f, 0.35f); for (int i = 0; i < 128; i++) { float a = i / 128f * Mathf.PI * 2; var p = new Vector3(nc.x + Mathf.Cos(a) * nr, 0, nc.z + Mathf.Sin(a) * nr); p.y = AgniFx.GroundY(p) + 0.25f; next.SetPosition(i, p); } }
        }
    }
}
