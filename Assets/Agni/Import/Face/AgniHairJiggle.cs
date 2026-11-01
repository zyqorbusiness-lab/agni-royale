// Agni Royale — cheap hair sway for the hair-v3 heroes (spring chains "jiggle_hair_<chain>_<i>" from characters_hair_v3).
// Verlet particles per bone tail, pulled back to the animated pose (stiffness), gravity + damping, fixed bone lengths,
// a head sphere so locks don't go through the skull, and an angle limit so a lock never flips. ~20 bones per hero, no allocations per frame.
// Runs only while the hero is visible and on LOD0; off on LOW graphics (AgniMahal.Quality 0) — set by AgniHeroes.SetupInMatch / the lobby.
using System.Collections.Generic;
using UnityEngine;

public class AgniHairJiggle : MonoBehaviour
{
    [Range(0.01f, 1f)] public float stiffness = 0.12f;
    [Range(0f, 1f)] public float damping = 0.18f;
    public float gravity = 2.2f;
    public float maxAngle = 40f;
    public float headRadius = 0.105f;
    public Vector3 headCenterLocal = new Vector3(0, 0.09f, 0.01f);
    public Transform head;
    public Renderer lod0;

    class Chain { public Transform[] b; public Quaternion[] rest; public Vector3[] p, q; public float[] len; }
    readonly List<Chain> chains = new List<Chain>();
    bool init;

    void Init()
    {
        init = true;
        var groups = new SortedDictionary<string, SortedDictionary<int, Transform>>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            var n = t.name; int c = n.IndexOf("jiggle_hair_"); if (c < 0) continue; n = n.Substring(c);
            int u = n.LastIndexOf('_'); if (u < 0 || !int.TryParse(n.Substring(u + 1), out int idx)) continue;
            var key = n.Substring(0, u); if (!groups.TryGetValue(key, out var g)) groups[key] = g = new SortedDictionary<int, Transform>();
            g[idx] = t;
        }
        foreach (var g in groups.Values)
        {
            var b = new List<Transform>(g.Values); if (b.Count < 2) continue;
            var ch = new Chain { b = b.ToArray(), rest = new Quaternion[b.Count], p = new Vector3[b.Count], q = new Vector3[b.Count], len = new float[b.Count] };
            for (int i = 0; i < b.Count; i++) { ch.rest[i] = b[i].localRotation; ch.p[i] = ch.q[i] = b[i].position; if (i > 0) ch.len[i] = Vector3.Distance(b[i].position, b[i - 1].position); }
            chains.Add(ch);
        }
        if (head == null) foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith(":Head") || t.name == "Head") { head = t; break; }
    }

    static bool Fin(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    public int BoneCount { get { if (!init) Init(); int n = 0; foreach (var c in chains) n += c.b.Length; return n; } }

    void OnEnable() { if (init) Reset(); }
    public void Reset() { foreach (var c in chains) for (int i = 0; i < c.b.Length; i++) { c.b[i].localRotation = c.rest[i]; c.p[i] = c.q[i] = c.b[i].position; } }

    public static bool GloballyOff;   // proof/debug switch
    public static bool Trace;         // proof/debug: log the state once a second
    float traceT;
    static int resets;
    void LateUpdate()
    {
        if (GloballyOff) return;
        if (!init) Init();
        if (chains.Count == 0) return;
        if (lod0 != null && !lod0.isVisible) return;
        float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
        Vector3 hc = head != null ? head.TransformPoint(headCenterLocal) : Vector3.zero; float hr = headRadius * (head != null ? head.lossyScale.y : 1f);
        Vector3 g = Vector3.down * gravity * dt * dt;
        float sc = transform.lossyScale.y;
        if (Trace && Time.realtimeSinceStartup > traceT) { traceT = Time.realtimeSinceStartup + 1f; var c0 = chains[0]; Debug.Log("[AgniHairJiggle] " + name + " dt=" + dt.ToString("F3") + " sc=" + sc + " head=" + (head != null ? head.name + " ls=" + head.lossyScale.y.ToString("F3") : "none") + " hr=" + hr.ToString("F3") + " chains=" + chains.Count + " c0 b0=" + c0.b[0].name + " parentOf1=" + (c0.b[1].parent != null ? c0.b[1].parent.name : "-") + " len1=" + c0.len[1].ToString("F3") + " p1-b1=" + (c0.p[1] - c0.b[1].position).magnitude.ToString("F3") + " bounds=" + (lod0 != null ? lod0.bounds.size.ToString() : "-")); }
        foreach (var c in chains)
        {
            for (int i = 0; i < c.b.Length; i++) c.b[i].localRotation = c.rest[i];   // animated/rest pose first
            c.p[0] = c.q[0] = c.b[0].position;
            for (int i = 1; i < c.b.Length; i++)
            {
                Vector3 target = c.b[i].position;                                    // where the lock wants to be (parents still at rest here)
                Vector3 v = (c.p[i] - c.q[i]) * (1f - damping); c.q[i] = c.p[i];
                Vector3 np = c.p[i] + v + g;
                np += (target - np) * stiffness;
                if (head != null) { var d = np - hc; float m = d.magnitude; if (m < hr && m > 1e-5f) np = hc + d / m * hr; }
                Vector3 par = c.p[i - 1]; Vector3 dir = np - par; float L = c.len[i] * sc;
                np = par + (dir.sqrMagnitude > 1e-10f ? dir.normalized : Vector3.down) * L;
                // angle limit against the animated direction
                Vector3 restDir = target - c.b[i - 1].position;
                float ang = Vector3.Angle(restDir, np - par);
                if (ang > maxAngle) np = par + Vector3.Slerp(restDir.normalized, (np - par).normalized, maxAngle / ang) * L;
                if (!(np.x - np.x == 0f) || !(np.y - np.y == 0f) || !(np.z - np.z == 0f) || (np - target).sqrMagnitude > 4f) { if (resets++ < 5) Debug.Log("[AgniHairJiggle] reset (runaway) on " + name); Reset(); return; }   // NaN / runaway guard
                c.p[i] = np;
                // rotate the parent bone so its child lands on the particle (children follow)
                Vector3 cur = c.b[i].position - c.b[i - 1].position, want = np - c.b[i - 1].position;
                if (cur.sqrMagnitude > 1e-8f && want.sqrMagnitude > 1e-8f && Vector3.Angle(cur, want) > 0.01f)
                {
                    var qr = Quaternion.FromToRotation(cur, want) * c.b[i - 1].rotation;
                    if (Fin(qr.x) && Fin(qr.y) && Fin(qr.z) && Fin(qr.w) && Mathf.Abs(qr.x * qr.x + qr.y * qr.y + qr.z * qr.z + qr.w * qr.w - 1f) < 0.01f) c.b[i - 1].rotation = qr;
                }
            }
        }
    }
}
