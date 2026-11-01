// Agni Royale — IMPORT v1 runtime helpers for the Blender landmarks: running clock hands, our weathervane (Ghanta Ghar top), landmark tag.
using UnityEngine;

namespace Agni.Import
{
    /// Tag on every imported landmark prefab (name + world footprint helpers live in AgniLandmarks).
    public class AgniLandmarkInfo : MonoBehaviour { public string landmark; }

    /// Ghanta Ghar's 8 hands (hour + minute on 4 faces) show the device's local time. Hands are pivoted at the clock centre;
    /// Blender posed them at 10:10 (hour -60 deg, minute +95 deg about the face normal), so we rotate by the difference.
    public class AgniClockHands : MonoBehaviour
    {
        struct H { public Transform t; public Quaternion q0; public Vector3 n; public bool hour; }
        H[] hs; float next;
        void Start()
        {
            var list = new System.Collections.Generic.List<H>();
            Vector3 c = Vector3.zero; int k = 0; var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all) if (t.name.StartsWith("GG_ClockHand_")) { c += t.position; k++; }
            if (k == 0) { enabled = false; return; }
            c /= k;
            foreach (var t in all)
                if (t.name.StartsWith("GG_ClockHand_"))
                {
                    var n = t.position - c; n.y = 0; n = n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.forward;
                    list.Add(new H { t = t, q0 = t.rotation, n = n, hour = t.name.Contains("Hour") });
                }
            hs = list.ToArray();
        }
        void Update()
        {
            if (hs == null || Time.unscaledTime < next) return; next = Time.unscaledTime + 1f;
            var now = System.DateTime.Now; float min = now.Minute + now.Second / 60f; float hr = (now.Hour % 12) + min / 60f;
            foreach (var h in hs)
            {
                float want = h.hour ? hr * 30f : min * 6f; float posed = h.hour ? -60f + 360f : 95f;
                float d = want - posed;
                h.t.rotation = Quaternion.AngleAxis(Sign * d, -h.n) * h.q0;
            }
        }
        public static float Sign = 1f;
    }

    /// Plain metal weathervane: rod + ball + compass cross (N/E/S/W plates) + arrow that swings into a slowly wandering wind.
    public class AgniWeathervane : MonoBehaviour
    {
        Transform arrow; float seed;
        public static void BuildGeometry(Transform root, Material iron, Material brass)
        {
            GameObject P(PrimitiveType t, string n, Transform par, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(par, false);
                g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = Quaternion.Euler(euler); g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            P(PrimitiveType.Cylinder, "Rod", root, new Vector3(0, 1.15f, 0), new Vector3(0.09f, 1.15f, 0.09f), iron);
            P(PrimitiveType.Sphere, "Ball", root, new Vector3(0, 0.55f, 0), Vector3.one * 0.34f, brass);
            P(PrimitiveType.Cylinder, "Collar", root, new Vector3(0, 0.12f, 0), new Vector3(0.36f, 0.12f, 0.36f), iron);
            P(PrimitiveType.Cube, "ArmNS", root, new Vector3(0, 1.25f, 0), new Vector3(0.05f, 0.05f, 1.6f), iron);
            P(PrimitiveType.Cube, "ArmEW", root, new Vector3(0, 1.25f, 0), new Vector3(1.6f, 0.05f, 0.05f), iron);
            // N/E/S/W as small cut-metal letter plates built from bars (no font needed)
            Vector3[] D = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < 4; i++)
            {
                var g = new GameObject("Letter_" + "NESW"[i]).transform; g.SetParent(root, false); g.localPosition = D[i] * 0.9f + Vector3.up * 1.25f; g.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, D[i]));
                float w = 0.22f, h = 0.3f, b = 0.045f; Vector3 S(float sx, float sy) => new Vector3(sx, sy, 0.03f);
                switch (i)
                {
                    case 0: P(PrimitiveType.Cube, "a", g, new Vector3(-w / 2, 0, 0), S(b, h), brass); P(PrimitiveType.Cube, "b", g, new Vector3(w / 2, 0, 0), S(b, h), brass); P(PrimitiveType.Cube, "c", g, Vector3.zero, S(b, h * 1.15f), brass, new Vector3(0, 0, 36)); break;
                    case 1: P(PrimitiveType.Cube, "a", g, new Vector3(-w / 2, 0, 0), S(b, h), brass); for (int k = -1; k <= 1; k++) P(PrimitiveType.Cube, "b" + k, g, new Vector3(0, k * h / 2, 0), S(w, b), brass); break;
                    case 2: for (int k = -1; k <= 1; k++) P(PrimitiveType.Cube, "h" + k, g, new Vector3(0, k * h / 2, 0), S(w, b), brass); P(PrimitiveType.Cube, "u", g, new Vector3(-w / 2, h / 4, 0), S(b, h / 2), brass); P(PrimitiveType.Cube, "d", g, new Vector3(w / 2, -h / 4, 0), S(b, h / 2), brass); break;
                    default: P(PrimitiveType.Cube, "a", g, new Vector3(-w * 0.4f, 0, 0), S(b, h), brass, new Vector3(0, 0, -12)); P(PrimitiveType.Cube, "b", g, new Vector3(-w * 0.13f, 0, 0), S(b, h), brass, new Vector3(0, 0, 12)); P(PrimitiveType.Cube, "c", g, new Vector3(w * 0.13f, 0, 0), S(b, h), brass, new Vector3(0, 0, -12)); P(PrimitiveType.Cube, "d", g, new Vector3(w * 0.4f, 0, 0), S(b, h), brass, new Vector3(0, 0, 12)); break;
                }
            }
            var arrow = new GameObject("Arrow").transform; arrow.SetParent(root, false); arrow.localPosition = new Vector3(0, 1.95f, 0);
            P(PrimitiveType.Cube, "Shaft", arrow, Vector3.zero, new Vector3(0.05f, 0.05f, 1.8f), iron);
            P(PrimitiveType.Cube, "Head", arrow, new Vector3(0, 0, 0.92f), new Vector3(0.03f, 0.24f, 0.24f), brass, new Vector3(45, 0, 0));
            P(PrimitiveType.Cube, "Fin", arrow, new Vector3(0, 0.06f, -0.8f), new Vector3(0.025f, 0.4f, 0.5f), brass);
            P(PrimitiveType.Sphere, "TopBall", root, new Vector3(0, 2.36f, 0), Vector3.one * 0.12f, brass);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        void Start()
        {
            seed = Random.value * 100f; arrow = transform.Find("Arrow");
            if (arrow == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var iron = new Material(lit); iron.SetColor("_BaseColor", new Color(0.13f, 0.14f, 0.15f)); iron.SetFloat("_Metallic", 0.75f); iron.SetFloat("_Smoothness", 0.45f);
                var brass = new Material(lit); brass.SetColor("_BaseColor", new Color(0.72f, 0.56f, 0.28f)); brass.SetFloat("_Metallic", 0.85f); brass.SetFloat("_Smoothness", 0.55f);
                BuildGeometry(transform, iron, brass); arrow = transform.Find("Arrow");
            }
        }
        void Update()
        {
            if (arrow == null) return;
            float t = Time.time * 0.05f + seed;
            float wind = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 220f + 35f + Mathf.Sin(Time.time * 1.7f) * 3f;
            arrow.localRotation = Quaternion.Slerp(arrow.localRotation, Quaternion.Euler(0, wind, 0), Time.deltaTime * 1.5f);
        }
    }
}
