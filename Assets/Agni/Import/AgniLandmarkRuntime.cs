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

    /// Plain metal weathervane: rod + ball + a big arrow with a tail fin that swings into a slowly wandering wind (no compass cross).
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
            // arrow-only vane (no compass cross: from a distance a rod with cross arms reads as a religious cross)
            P(PrimitiveType.Cylinder, "Rod", root, new Vector3(0, 1.0f, 0), new Vector3(0.09f, 1.0f, 0.09f), iron);
            P(PrimitiveType.Sphere, "Ball", root, new Vector3(0, 0.55f, 0), Vector3.one * 0.34f, brass);
            P(PrimitiveType.Cylinder, "Collar", root, new Vector3(0, 0.12f, 0), new Vector3(0.36f, 0.12f, 0.36f), iron);
            var arrow = new GameObject("Arrow").transform; arrow.SetParent(root, false); arrow.localPosition = new Vector3(0, 1.75f, 0);
            P(PrimitiveType.Cube, "Shaft", arrow, Vector3.zero, new Vector3(0.06f, 0.06f, 2.0f), iron);
            P(PrimitiveType.Cube, "Head", arrow, new Vector3(0, 0, 1.02f), new Vector3(0.035f, 0.3f, 0.3f), brass, new Vector3(45, 0, 0));
            P(PrimitiveType.Cube, "Fin", arrow, new Vector3(0, 0.1f, -0.78f), new Vector3(0.03f, 0.62f, 0.62f), brass);
            P(PrimitiveType.Cube, "Fin2", arrow, new Vector3(0, -0.04f, -0.55f), new Vector3(0.03f, 0.34f, 0.3f), brass);
            P(PrimitiveType.Sphere, "TopBall", root, new Vector3(0, 2.02f, 0), Vector3.one * 0.13f, brass);
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
