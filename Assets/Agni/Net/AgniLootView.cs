// Agni Royale — LOOT v1 client visuals: every item drawn from code (our own simple models; guns reuse our weapon FBX),
// a crisp rarity-coloured outline (inverted hull, no glow), the death "peti" trunk and the airdrop "Raahat Peti"
// with its striped parachute, red flare and smoke column. Name tags are drawn by AgniLootHud.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Net
{
    public static class AgniLootView
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<Rarity, Material> rims = new Dictionary<Rarity, Material>();
        static Texture2D puff;

        public static Material Lit(Color c, float smooth = 0.3f, float metal = 0f, Texture tex = null)
        {
            string k = c.ToString() + smooth + metal + (tex != null ? tex.name : "");
            if (mats.TryGetValue(k, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (tex != null) { m.SetTexture("_BaseMap", tex); }
            m.enableInstancing = true; mats[k] = m; return m;
        }
        static Material Rim(Rarity r)
        {
            if (rims.TryGetValue(r, out var m) && m != null) return m;
            var sh = Resources.Load<Shader>("AgniNet/AgniOutline") ?? Shader.Find("AgniRoyale/Outline");
            m = new Material(sh); var c = AgniItems.RarityColor(r); m.SetColor("_Color", r == Rarity.Common ? new Color(0.92f, 0.9f, 0.82f) : c); m.SetFloat("_Width", 0.007f); rims[r] = m; return m;
        }
        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t); Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = Quaternion.Euler(euler);
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; return g;
        }
        static Material Brass => Lit(new Color(0.78f, 0.6f, 0.25f), 0.6f, 0.8f);
        static Material Steel => Lit(new Color(0.16f, 0.17f, 0.18f), 0.5f, 0.6f);
        static Color LvCol(int lv) => lv == 1 ? new Color(0.62f, 0.55f, 0.4f) : lv == 2 ? new Color(0.35f, 0.4f, 0.26f) : new Color(0.2f, 0.23f, 0.27f);

        // ------------------------------------------------------------ guns (shared with the fighter's hands)
        static readonly Dictionary<string, Material> gunMats = new Dictionary<string, Material>();
        public static GameObject GunModel(ItemId gun, Transform parent, bool lowPoly)
        {
            var gd = AgniItems.Gun(gun); var src = Resources.Load<GameObject>("AgniNet/Weapons/" + gd.model) ?? Resources.Load<GameObject>("AgniNet/Weapons/AGNI_47");
            if (src == null) return null;
            var go = Object.Instantiate(src, parent); go.name = gd.model; go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            if (!gunMats.TryGetValue(gd.model, out var m) || m == null)
            {
                var tex = Resources.Load<Texture2D>("AgniNet/Weapons/T_" + gd.model + "_Albedo") ?? Resources.Load<Texture2D>("AgniNet/Weapons/T_AGNI_47_Albedo");
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetTexture("_BaseMap", tex); m.SetFloat("_Smoothness", 0.42f); m.SetFloat("_Metallic", 0.35f); gunMats[gd.model] = m;
            }
            go.AddComponent<GunAxes>().Measure();
            var lg = go.GetComponent<LODGroup>(); if (lg != null) Object.Destroy(lg);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.On;
                bool l1 = r.name.Contains("LOD1"), l2 = r.name.Contains("LOD2"), l0 = !l1 && !l2;
                r.enabled = lowPoly ? (l2 || (l0 && !HasLod(go, "LOD2"))) : l0;
            }
            return go;
        }
        /// the FBX importer may rotate the model root: measure the real barrel (grip → Muzzle) and up (→ ScopeMount) once,
        /// so callers can say "barrel along X, top along Y" with  rotation = LookRotation(barrel, up) * fix.
        public class GunAxes : MonoBehaviour
        {
            public Quaternion fix = Quaternion.identity; public Vector3 muzzleLocal;
            public void Measure()
            {
                Transform mz = null, sc = null; foreach (var t in GetComponentsInChildren<Transform>(true)) { if (t.name == "Muzzle") mz = t; else if (t.name == "ScopeMount") sc = t; }
                Vector3 fwd = mz != null ? transform.InverseTransformPoint(mz.position) : Vector3.back; Vector3 up = sc != null ? transform.InverseTransformPoint(sc.position) : Vector3.up;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.back; up = Vector3.ProjectOnPlane(up, fwd.normalized); if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
                muzzleLocal = fwd; fix = Quaternion.Inverse(Quaternion.LookRotation(fwd.normalized, up.normalized));
            }
        }
        static bool HasLod(GameObject g, string tag) { foreach (var r in g.GetComponentsInChildren<Renderer>()) if (r.name.Contains(tag)) return true; return false; }

        // ------------------------------------------------------------ one item on the floor
        public static GameObject Build(ItemId id, Transform parent)
        {
            var v = new GameObject("Loot_" + id); v.transform.SetParent(parent, false);
            var cat = AgniItems.Cat(id); var lv = AgniItems.Def(id).level; float scale = 1.6f; // loot reads a bit larger than life, like the classic games
            switch (cat)
            {
                case ItemCat.Throwable:
                {
                    var t = AgniFx.BuildThrowableView((ThrowKind)(byte)id, v.transform); t.transform.localScale = Vector3.one * 2.2f; t.transform.localPosition = Vector3.up * 0.13f; scale = 1f; break;
                }
                case ItemCat.Gun:
                {
                    var g = GunModel(id, v.transform, true); scale = 1f;
                    if (g != null)
                    {
                        g.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.forward) * g.GetComponent<GunAxes>().fix; // on its side, barrel along +X
                        g.transform.rotation = v.transform.rotation * g.transform.rotation;
                        var b = Bounds(g); g.transform.position += new Vector3(v.transform.position.x - b.center.x, v.transform.position.y + 0.012f - b.min.y, v.transform.position.z - b.center.z);
                    }
                    break;
                }
                case ItemCat.Ammo:
                {
                    Color body = id == ItemId.GoliRifle ? new Color(0.36f, 0.4f, 0.23f) : id == ItemId.GoliSmg ? new Color(0.58f, 0.47f, 0.32f) : id == ItemId.Chharra ? new Color(0.62f, 0.16f, 0.12f) : new Color(0.17f, 0.26f, 0.2f);
                    Color band = id == ItemId.GoliRifle ? new Color(0.95f, 0.78f, 0.2f) : id == ItemId.GoliSmg ? new Color(0.25f, 0.45f, 0.85f) : id == ItemId.Chharra ? new Color(0.85f, 0.7f, 0.35f) : new Color(0.92f, 0.92f, 0.9f);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.07f, 0), new Vector3(0.3f, 0.14f, 0.17f), Lit(body, 0.25f));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.145f, 0.175f), Lit(band, 0.3f));
                    for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cylinder, v.transform, new Vector3(-0.1f + i * 0.045f, 0.165f, 0.03f), new Vector3(0.022f, id == ItemId.GoliSniper ? 0.05f : 0.03f, 0.022f), id == ItemId.Chharra ? Lit(new Color(0.75f, 0.15f, 0.1f), 0.4f) : Brass);
                    break;
                }
                case ItemCat.Vest:
                {
                    var c = Lit(LvCol(lv), 0.15f); var d = Lit(LvCol(lv) * 0.75f, 0.15f);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.045f, -0.02f), new Vector3(0.46f, 0.07f, 0.5f), c);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(-0.13f, 0.06f, 0.2f), new Vector3(0.14f, 0.07f, 0.22f), c);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0.13f, 0.06f, 0.2f), new Vector3(0.14f, 0.07f, 0.22f), c);
                    for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, v.transform, new Vector3(-0.14f + i * 0.14f, 0.095f, -0.14f), new Vector3(0.11f, 0.05f, 0.13f), d);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.085f, 0.06f), new Vector3(0.44f, 0.02f, 0.035f), d);
                    if (lv == 3) Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.1f, 0.0f), new Vector3(0.3f, 0.025f, 0.2f), Lit(new Color(0.12f, 0.13f, 0.15f), 0.4f, 0.3f));
                    break;
                }
                case ItemCat.Helmet:
                {
                    var c = Lit(LvCol(lv), lv == 3 ? 0.45f : 0.2f, lv == 3 ? 0.2f : 0f);
                    Prim(PrimitiveType.Sphere, v.transform, new Vector3(0, 0.0f, 0), new Vector3(0.29f, 0.26f, 0.32f), c);
                    Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.012f, 0), new Vector3(0.33f, 0.012f, 0.36f), Lit(LvCol(lv) * 0.7f, 0.2f));
                    if (lv >= 2) Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.1f, 0.1f), new Vector3(0.05f, 0.05f, 0.04f), Steel); // lamp mount
                    if (lv == 3) Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.07f, 0.15f), new Vector3(0.22f, 0.06f, 0.03f), Lit(new Color(0.05f, 0.08f, 0.1f), 0.85f, 0.2f), new Vector3(-20, 0, 0));
                    break;
                }
                case ItemCat.Bag:
                {
                    var c = Lit(lv == 1 ? new Color(0.6f, 0.48f, 0.33f) : lv == 2 ? new Color(0.36f, 0.42f, 0.28f) : new Color(0.22f, 0.27f, 0.36f), 0.15f); var d = Lit(new Color(0.12f, 0.12f, 0.12f), 0.2f);
                    float s = 0.8f + 0.12f * lv;
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.1f * s, 0), new Vector3(0.38f, 0.2f, 0.46f) * s, c);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.205f * s, 0.12f * s), new Vector3(0.39f, 0.03f, 0.22f) * s, Lit(LvCol(lv) * 0.9f, 0.15f));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.12f * s, -0.25f * s), new Vector3(0.26f, 0.13f, 0.06f) * s, c);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(-0.11f * s, 0.012f, 0), new Vector3(0.05f, 0.02f, 0.42f) * s, d); Prim(PrimitiveType.Cube, v.transform, new Vector3(0.11f * s, 0.012f, 0), new Vector3(0.05f, 0.02f, 0.42f) * s, d);
                    break;
                }
                case ItemCat.Heal:
                {
                    if (id == ItemId.Patti)
                    {
                        var w = Lit(new Color(0.95f, 0.93f, 0.88f), 0.1f);
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(-0.05f, 0.055f, 0), new Vector3(0.11f, 0.06f, 0.11f), w, new Vector3(0, 0, 90));
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0.07f, 0.055f, 0.03f), new Vector3(0.11f, 0.06f, 0.11f), w, new Vector3(0, 30, 90));
                        Prim(PrimitiveType.Cube, v.transform, new Vector3(0.0f, 0.004f, -0.03f), new Vector3(0.22f, 0.006f, 0.05f), w);
                    }
                    else if (id == ItemId.DawaKit)
                    {
                        Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.08f, 0), new Vector3(0.38f, 0.16f, 0.26f), Lit(new Color(0.95f, 0.95f, 0.93f), 0.35f));
                        var gm = Lit(new Color(0.12f, 0.62f, 0.32f), 0.3f);
                        Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.163f, 0), new Vector3(0.14f, 0.01f, 0.045f), gm); Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.163f, 0), new Vector3(0.045f, 0.01f, 0.14f), gm);
                        Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.08f, 0), new Vector3(0.385f, 0.03f, 0.265f), gm);
                    }
                    else
                    {
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.08f, 0), new Vector3(0.085f, 0.08f, 0.085f), Lit(new Color(0.98f, 0.55f, 0.12f), 0.55f, 0.3f));
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.09f, 0), new Vector3(0.087f, 0.025f, 0.087f), Lit(new Color(0.97f, 0.95f, 0.9f), 0.4f));
                        Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.162f, 0), new Vector3(0.07f, 0.004f, 0.07f), Lit(new Color(0.7f, 0.72f, 0.74f), 0.7f, 0.9f));
                    }
                    break;
                }
                case ItemCat.Scope:
                {
                    int z = AgniItems.ScopeZoom(id); float len = z == 2 ? 0.14f : z == 4 ? 0.22f : 0.32f; var dark = Lit(new Color(0.1f, 0.1f, 0.11f), 0.45f, 0.3f);
                    Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.07f, 0), new Vector3(0.05f, len / 2, 0.05f), dark, new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.07f, len / 2), new Vector3(0.075f, 0.03f, 0.075f), dark, new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.07f, len / 2 + 0.031f), new Vector3(0.06f, 0.002f, 0.06f), Lit(new Color(0.2f, 0.45f, 0.6f), 0.95f), new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.025f, 0), new Vector3(0.04f, 0.04f, len * 0.6f), Steel);
                    if (z == 8) Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0.035f, 0.1f, -0.02f), new Vector3(0.035f, 0.015f, 0.035f), dark);
                    break;
                }
                case ItemCat.Muzzle:
                    Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.09f, 0.05f), Steel, new Vector3(90, 0, 0));
                    for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cylinder, v.transform, new Vector3(0, 0.03f, -0.05f + i * 0.05f), new Vector3(0.056f, 0.008f, 0.056f), Lit(new Color(0.3f, 0.3f, 0.32f), 0.5f, 0.6f), new Vector3(90, 0, 0));
                    break;
                case ItemCat.Mag:
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.02f, -0.04f), new Vector3(0.05f, 0.035f, 0.14f), Steel, new Vector3(0, 0, 90));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.02f, 0.09f), new Vector3(0.05f, 0.035f, 0.14f), Steel, new Vector3(0, -18, 90));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.045f, -0.11f), new Vector3(0.03f, 0.01f, 0.025f), Brass);
                    break;
                default: // stock
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.07f, 0.24f), Lit(new Color(0.14f, 0.14f, 0.13f), 0.2f));
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.045f, -0.14f), new Vector3(0.055f, 0.12f, 0.04f), Lit(new Color(0.2f, 0.2f, 0.19f), 0.15f));
                    break;
            }
            if (scale != 1f) v.transform.localScale = Vector3.one * scale;
            AddRims(v, AgniItems.Def(id).rarity);
            // cull loot early on far LODs (phones): the item disappears below ~1.4% of screen height
            var lg = v.AddComponent<LODGroup>(); var rs = v.GetComponentsInChildren<Renderer>(); lg.SetLODs(new[] { new LOD(0.014f, rs) }); lg.RecalculateBounds();
            return v;
        }
        static Bounds Bounds(GameObject g) { Bounds b = default; bool any = false; foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); } return b; }
        static void AddRims(GameObject v, Rarity r)
        {
            var mat = Rim(r);
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
                var o = new GameObject("Rim"); o.transform.SetParent(mf.transform, false); o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var orr = o.AddComponent<MeshRenderer>(); orr.sharedMaterial = mat; orr.shadowCastingMode = ShadowCastingMode.Off; orr.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------ crates
        public static GameObject BuildCrate(AgniCrate c)
        {
            var v = new GameObject("CrateView"); v.transform.SetParent(c.transform, false);
            if (c.Kind.Value == 0)
            {   // death peti: painted tin trunk
                var body = Lit(new Color(0.16f, 0.4f, 0.47f), 0.35f, 0.25f); var trim = Lit(new Color(0.55f, 0.56f, 0.55f), 0.45f, 0.6f);
                Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.22f, 0), new Vector3(0.95f, 0.44f, 0.55f), body);
                Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.49f, 0), new Vector3(0.98f, 0.12f, 0.58f), Lit(new Color(0.13f, 0.34f, 0.4f), 0.35f, 0.25f));
                foreach (var x in new[] { -0.42f, 0.42f }) Prim(PrimitiveType.Cube, v.transform, new Vector3(x, 0.28f, 0), new Vector3(0.05f, 0.56f, 0.6f), trim);
                Prim(PrimitiveType.Cube, v.transform, new Vector3(0, 0.38f, -0.295f), new Vector3(0.1f, 0.12f, 0.03f), Brass);
                foreach (var x in new[] { -0.52f, 0.52f }) Prim(PrimitiveType.Cube, v.transform, new Vector3(x, 0.3f, 0), new Vector3(0.04f, 0.04f, 0.2f), trim);
                AddRims(v, Rarity.Common);
            }
            else
            {   // Raahat Peti: plank crate in a steel frame — corner brackets, two webbing straps, our own stencil, parachute (collapses on top after landing)
                var wood = Resources.Load<Material>("AgniNet/Village/M_V_Wood") ?? Lit(new Color(0.6f, 0.45f, 0.3f), 0.2f);
                var steel = Lit(new Color(0.32f, 0.34f, 0.33f), 0.45f, 0.75f); var red = Lit(new Color(0.72f, 0.16f, 0.09f), 0.35f, 0.25f); var strap = Lit(new Color(0.24f, 0.26f, 0.17f), 0.1f);
                const float S = 1.36f, h = S / 2f;
                Prim(PrimitiveType.Cube, v.transform, new Vector3(0, h, 0), new Vector3(S, S, S), wood);
                // plank grooves (dark thin bands) on the four sides
                var groove = Lit(new Color(0.18f, 0.12f, 0.08f), 0.1f);
                for (int g = 1; g < 4; g++) { float y = g * S / 4f; Prim(PrimitiveType.Cube, v.transform, new Vector3(0, y, 0), new Vector3(S + 0.008f, 0.02f, S + 0.008f), groove); }
                // steel frame: 12 edge bars + 8 corner brackets
                float e = 0.07f, o = h - e / 2 + 0.01f;
                foreach (var a in new[] { -o, o }) foreach (var b in new[] { -o, o })
                {
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(a, h, b), new Vector3(e, S + 0.02f, e), steel);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(a, h + b, 0), new Vector3(e, e, S + 0.02f), steel);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, h + b, a), new Vector3(S + 0.02f, e, e), steel);
                }
                foreach (var a in new[] { -1, 1 }) foreach (var b in new[] { -1, 1 }) foreach (var cc in new[] { -1, 1 })
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(a * (h - 0.06f), h + cc * (h - 0.06f), b * (h - 0.06f)), new Vector3(0.2f, 0.2f, 0.2f), red);
                // two webbing straps around the body with buckles
                foreach (var z in new[] { -0.3f, 0.3f })
                {
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(0, h, z), new Vector3(S + 0.04f, S + 0.04f, 0.12f), strap);
                    Prim(PrimitiveType.Cube, v.transform, new Vector3(h + 0.03f, h + 0.1f, z), new Vector3(0.03f, 0.1f, 0.16f), steel);
                }
                // stencil on two sides (our own words)
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); var tsh = Resources.Load<Shader>("AgniNet/AgniWorldText");
                foreach (var side in new[] { -1, 1 })
                {
                    var tg = new GameObject("Stencil"); tg.transform.SetParent(v.transform, false); tg.transform.localPosition = new Vector3(side * (h + 0.012f), h - 0.05f, 0); tg.transform.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                    var tm = tg.AddComponent<TextMesh>(); tm.text = "RAAHAT\n<size=40>AGNI-01</size>"; tm.richText = true; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.022f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold; tm.color = new Color(0.95f, 0.92f, 0.85f, 0.92f);
                    var mr = tg.GetComponent<MeshRenderer>(); if (tsh != null) { var m = new Material(tsh) { mainTexture = font.material.mainTexture }; mr.sharedMaterial = m; } else mr.sharedMaterial = font.material;
                }
                AddRims(v, Rarity.Epic);
                var chute = BuildChute(v.transform); chute.AddComponent<ChuteFollow>().crate = c;
                var fx = new GameObject("Flare"); fx.transform.SetParent(v.transform, false); fx.transform.localPosition = new Vector3(0, 1.45f, 0); fx.AddComponent<FlareSmoke>().crate = c;
            }
            return v;
        }
        static GameObject BuildChute(Transform parent)
        {
            var go = new GameObject("Chute"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 6.2f, 0);
            int seg = 16, rings = 5; float R = 3.4f; var m = new Mesh(); var V = new List<Vector3>(); var N = new List<Vector3>(); var A = new List<int>(); var B = new List<int>();
            for (int s = 0; s < seg; s++)
            {
                var tri = (s % 2 == 0) ? A : B;
                for (int r = 0; r < rings; r++)
                {
                    float p0 = r / (float)rings, p1 = (r + 1) / (float)rings;
                    Vector3 P(float a, float p) { float ang = p * Mathf.PI * 0.42f; return new Vector3(Mathf.Cos(a) * Mathf.Sin(ang) * R, Mathf.Cos(ang) * R * 0.55f - R * 0.55f, Mathf.Sin(a) * Mathf.Sin(ang) * R); }
                    float a0 = s / (float)seg * Mathf.PI * 2, a1 = (s + 1) / (float)seg * Mathf.PI * 2; int i = V.Count;
                    V.Add(P(a0, p0)); V.Add(P(a1, p0)); V.Add(P(a1, p1)); V.Add(P(a0, p1));
                    for (int k = 0; k < 4; k++) N.Add((V[i + k] + Vector3.up * R * 0.6f).normalized);
                    tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2); // outside
                    tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3); // inside (cloth seen from below)
                }
            }
            m.SetVertices(V); m.SetNormals(N); m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1); m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Lit(new Color(0.92f, 0.42f, 0.12f), 0.15f), Lit(new Color(0.95f, 0.94f, 0.9f), 0.15f) };
            var cordM = Lit(new Color(0.85f, 0.85f, 0.8f), 0.1f);
            for (int s = 0; s < 8; s++)
            {   // cords from the crate's top to the canopy rim
                float a = s / 8f * Mathf.PI * 2; float ang = Mathf.PI * 0.42f;
                var rim = new Vector3(Mathf.Cos(a) * Mathf.Sin(ang) * R, Mathf.Cos(ang) * R * 0.55f - R * 0.55f, Mathf.Sin(a) * Mathf.Sin(ang) * R);
                var top = new Vector3(Mathf.Cos(a) * 0.5f, -6.2f + 1.42f, Mathf.Sin(a) * 0.5f); var mid = (rim + top) / 2; var d = rim - top;
                var c = Prim(PrimitiveType.Cylinder, go.transform, mid, new Vector3(0.025f, d.magnitude / 2, 0.025f), cordM); c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            }
            return go;
        }
        class ChuteFollow : MonoBehaviour
        {
            public AgniCrate crate; float t; bool cordsHidden;
            void Update()
            {
                if (crate == null) return; t += Time.deltaTime;
                if (!crate.Landed.Value) { transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 4f, t * 12f, Mathf.Cos(t * 0.7f) * 4f); return; }
                // collapse: the canopy drops and lies spread on the ground beside the crate, cords go slack (stays visible, like the real thing)
                var s = transform.localScale; s = Vector3.MoveTowards(s, new Vector3(0.5f, 0.05f, 0.62f), Time.deltaTime * 0.9f); transform.localScale = s;
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, new Vector3(2.3f, 0.12f, 0.4f), Time.deltaTime * 6f);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.Euler(0, 25, 4), Time.deltaTime * 40f);
                if (!cordsHidden) { cordsHidden = true; foreach (Transform ch in transform) ch.gameObject.SetActive(false); }
            }
        }
        /// red signal flare + a tall smoke column (more smoke once it has landed). Soft alpha particles, no bloom.
        class FlareSmoke : MonoBehaviour
        {
            public AgniCrate crate; ParticleSystem smoke; Renderer spark; float t;
            void Start()
            {
                if (puff == null) puff = SoftPuff(64);
                var go = new GameObject("Smoke"); go.transform.SetParent(transform, false); smoke = go.AddComponent<ParticleSystem>();
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = smoke.main; main.loop = true; main.duration = 5; main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 220; main.gravityModifier = -0.06f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.18f, 0.12f, 0.75f), new Color(0.95f, 0.35f, 0.25f, 0.6f));
                var em = smoke.emission; em.rateOverTime = 3f;
                var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = 0.15f; sh.rotation = new Vector3(-90, 0, 0);
                var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 4.2f)));
                var col = smoke.colorOverLifetime; col.enabled = true; var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.2f, 0.12f), 0), new GradientColorKey(new Color(0.9f, 0.55f, 0.5f), 0.6f), new GradientColorKey(new Color(0.85f, 0.8f, 0.8f), 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.75f, 0.08f), new GradientAlphaKey(0.35f, 0.6f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.4f); vel.z = new ParticleSystem.MinMaxCurve(0.0f, 0.2f);
                var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                var pr = go.GetComponent<ParticleSystemRenderer>(); var sm = new Material(AgniFx.Alpha) { mainTexture = puff }; pr.sharedMaterial = sm; pr.shadowCastingMode = ShadowCastingMode.Off; pr.sortMode = ParticleSystemSortMode.Distance;
                smoke.Play();
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>()); q.transform.SetParent(transform, false); q.transform.localScale = Vector3.one * 0.5f;
                spark = q.GetComponent<Renderer>(); var fm = new Material(AgniFx.Alpha) { mainTexture = puff }; fm.SetColor("_Color", new Color(1f, 0.35f, 0.2f, 0.95f)); spark.sharedMaterial = fm; spark.shadowCastingMode = ShadowCastingMode.Off;
            }
            void Update()
            {
                if (smoke == null) return; t += Time.deltaTime;
                var em = smoke.emission; em.rateOverTime = crate != null && crate.Landed.Value ? 9f : 3.5f;
                var cam = Camera.main; if (spark != null && cam != null) { spark.transform.rotation = Quaternion.LookRotation(spark.transform.position - cam.transform.position); spark.transform.localScale = Vector3.one * (0.42f + 0.12f * Mathf.PerlinNoise(t * 9f, 0)); }
            }
        }
        static Texture2D SoftPuff(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float nz = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.5f + Mathf.PerlinNoise(x * 0.27f + 9, y * 0.27f) * 0.5f;
                    float a = Mathf.Clamp01(1 - r) ; a = a * a * (3 - 2 * a) * Mathf.Lerp(0.6f, 1f, nz);
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply(); return t;
        }
    }
}
