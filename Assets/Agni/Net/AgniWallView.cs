// Agni Royale — AGNI DEEWAR look: a curved wall of hand-pressed, kiln-fired clay slabs (terracotta) with thin ember
// seams, rising out of the ground with a dust burst. Our own design: earthy and solid — not a gel/ice shield.
// As it takes damage the clay darkens and the seams glow hotter, so you can read how close it is to breaking.
using UnityEngine;

namespace Agni.Net
{
    public class AgniWallView : MonoBehaviour
    {
        const float R = 3.4f, Arc = 66f;
        Transform body; float t0; Material mat; float dmg; Color baseCol = Color.white;
        static Texture2D albedo, emis; static Material clayShared;

        static void Segment(int i, out Vector3 pos, out float yaw, out float w)
        {
            float th = Mathf.Lerp(-Arc * 0.5f, Arc * 0.5f, (i + 0.5f) / AgniWall.Segs);
            pos = new Vector3(Mathf.Sin(th * Mathf.Deg2Rad) * R, 0, Mathf.Cos(th * Mathf.Deg2Rad) * R - R);
            yaw = th; w = 2 * R * Mathf.Sin(Arc / AgniWall.Segs * 0.5f * Mathf.Deg2Rad) * 1.06f;
        }
        public static void BuildColliders(Transform root)
        {
            for (int i = 0; i < AgniWall.Segs; i++)
            {
                Segment(i, out var p, out var yaw, out var w);
                var g = new GameObject("Col" + i); g.transform.SetParent(root, false); g.transform.localPosition = p + Vector3.up * AgniWall.Height * 0.5f; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(w, AgniWall.Height, AgniWall.Thick);
            }
        }

        void Start()
        {
            t0 = Time.time; mat = new Material(ClayMaterial()); baseCol = mat.GetColor("_BaseColor");
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            var rnd = new System.Random(GetInstanceID());
            for (int i = 0; i < AgniWall.Segs; i++)
            {
                Segment(i, out var p, out var yaw, out var w);
                float h = AgniWall.Height * (0.94f + 0.1f * (float)rnd.NextDouble());
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>()); g.name = "Slab" + i; g.transform.SetParent(body, false);
                g.transform.localPosition = p + Vector3.up * h * 0.5f; g.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 2 - 1, yaw + ((float)rnd.NextDouble() * 2 - 1), (float)rnd.NextDouble() * 2 - 1);
                g.transform.localScale = new Vector3(w, h, AgniWall.Thick * (0.9f + 0.2f * (float)rnd.NextDouble()));
                var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
                // a rounded lip of darker clay on top (hand-finished edge)
                var lip = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(lip.GetComponent<Collider>()); lip.transform.SetParent(g.transform, false);
                lip.transform.localPosition = new Vector3(0, 0.5f, 0); lip.transform.localRotation = Quaternion.Euler(0, 0, 90); lip.transform.localScale = new Vector3(0.12f / h * 1.0f, 0.52f, 1.12f);
                lip.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            body.localScale = new Vector3(1, 0.02f, 1);
            AgniFx.WallDust(transform.position, transform.rotation);
        }
        void Update()
        {
            float k = Mathf.Clamp01((Time.time - t0) / 0.35f); float e = 1f - Mathf.Pow(1 - k, 3); float over = k < 1 ? Mathf.Sin(k * Mathf.PI) * 0.06f : 0;
            body.localScale = new Vector3(1, Mathf.Max(0.02f, e + over), 1);
        }
        public void SetDamage(float d01, bool fresh)
        {
            dmg = d01; if (mat == null) return;
            mat.SetColor("_BaseColor", Color.Lerp(baseCol, new Color(0.36f, 0.2f, 0.14f), d01 * 0.7f));
            mat.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.12f) * Mathf.Lerp(0.55f, 2.2f, d01));
        }

        public static Material ClayMaterial()
        {
            if (clayShared != null) return clayShared;
            BakeTextures();
            clayShared = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_AgniDeewar_Clay" };
            clayShared.SetTexture("_BaseMap", albedo); clayShared.SetColor("_BaseColor", Color.white);
            clayShared.SetFloat("_Smoothness", 0.18f); clayShared.SetFloat("_Metallic", 0f);
            clayShared.EnableKeyword("_EMISSION"); clayShared.SetTexture("_EmissionMap", emis); clayShared.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.12f) * 0.55f);
            clayShared.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return clayShared;
        }
        static float Fbm(float x, float y) { float s = 0, a = 0.5f, f = 1; for (int i = 0; i < 5; i++) { s += a * Mathf.PerlinNoise(x * f + 3.7f * i, y * f + 9.2f * i); a *= 0.5f; f *= 2.1f; } return s; }
        static void BakeTextures()
        {
            int n = 256; albedo = new Texture2D(n, n, TextureFormat.RGBA32, true); emis = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var a = new Color32[n * n]; var e = new Color32[n * n];
            // Voronoi seeds for the crack network
            var rnd = new System.Random(7); var seeds = new Vector2[28]; for (int i = 0; i < seeds.Length; i++) seeds[i] = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float nz = Fbm(u * 6, v * 6), streak = Mathf.PerlinNoise(u * 40, v * 3) * 0.5f;
                    var c = Color.Lerp(new Color(0.60f, 0.29f, 0.17f), new Color(0.80f, 0.47f, 0.29f), nz * 1.1f);
                    c = Color.Lerp(c, new Color(0.38f, 0.21f, 0.14f), Mathf.Clamp01((0.22f - v) * 3f) * 0.8f);   // kiln-dark foot
                    c *= 0.9f + streak * 0.2f;                                                                 // finger-pressed streaks
                    // crack distance = gap between nearest two seeds (wrapped)
                    float d1 = 9, d2 = 9; foreach (var s in seeds) { var dd = new Vector2(Mathf.Abs(u - s.x), Mathf.Abs(v - s.y)); dd.x = Mathf.Min(dd.x, 1 - dd.x); dd.y = Mathf.Min(dd.y, 1 - dd.y); float d = dd.magnitude; if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d; }
                    float edge = Mathf.Clamp01(1f - (d2 - d1) / 0.012f); edge *= Mathf.Clamp01(Fbm(u * 9 + 4, v * 9) * 1.8f - 0.35f);
                    c = Color.Lerp(c, new Color(0.22f, 0.11f, 0.07f), edge * 0.8f);
                    a[y * n + x] = c; float g = edge * edge; e[y * n + x] = new Color(g, g * 0.5f, g * 0.2f, 1);
                }
            albedo.SetPixels32(a); albedo.Apply(); emis.SetPixels32(e); emis.Apply();
        }
    }
}
