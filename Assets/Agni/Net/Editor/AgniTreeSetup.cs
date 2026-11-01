// Agni Royale — our own procedural Indian trees (landmarks/trees, Blender, CC0 bark) as Unity prefabs:
// bark + alpha-clipped leaf materials, LODGroup from _LOD0.._LOD3 (LOD3 = impostor card), trunk collider. Replaces the cartoon kit trees.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Net.EditorTools
{
    public class AgniTreeImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Agni/Net/Trees/")) return;
            var mi = (ModelImporter)assetImporter; mi.globalScale = 1f; mi.useFileScale = true; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importNormals = ModelImporterNormals.Import; mi.addCollider = false; mi.isReadable = false; mi.importCameras = false; mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Agni/Net/Trees/")) return;
            var ti = (TextureImporter)assetImporter; ti.maxTextureSize = assetPath.Contains("Billboard") ? 256 : 1024; ti.mipmapEnabled = true;
            if (assetPath.EndsWith("_N.png")) ti.textureType = TextureImporterType.NormalMap;
            else { ti.alphaIsTransparency = assetPath.Contains("Leaf") || assetPath.Contains("Billboard"); ti.mipMapsPreserveCoverage = ti.alphaIsTransparency; ti.alphaTestReferenceValue = 0.45f; }
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = ti.maxTextureSize; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
    }

    public static class AgniTreeSetup
    {
        const string D = "Assets/Agni/Net/Trees/";
        public static readonly string[] Species = { "Neem", "Mango", "Palm", "Khejri", "Gulmohar", "Banana", "Lantana" };
        static Material Mat(string name, Texture2D tex, Texture2D nrm, bool clip, float smooth)
        {
            var path = D + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0);
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            if (clip) { m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); m.renderQueue = (int)RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout"); }
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        public static Dictionary<string, GameObject> Build()
        {
            var outp = new Dictionary<string, GameObject>(); Directory.CreateDirectory(D + "Prefabs");
            var bark = Mat("M_TreeBark", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_BarkAtlas_D.jpg"), AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_BarkAtlas_N.png"), false, 0.08f);
            var leaf = Mat("M_TreeLeaf", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_LeafAtlas_D.png"), null, true, 0.12f);
            foreach (var sp in Species)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(D + sp + ".fbx"); if (src == null) { Debug.LogWarning("[AgniTrees] missing " + sp); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var vars = new List<Transform>(); foreach (Transform t in inst.transform) if (t.name.StartsWith(sp + "_")) vars.Add(t);
                if (vars.Count == 0) vars.Add(inst.transform);
                foreach (var v in vars)
                {
                    v.SetParent(null, true); string vn = v.name; v.position = Vector3.zero; v.rotation = Quaternion.identity;
                    var lods = new List<LOD>(); float[] sizes = { 0.32f, 0.11f, 0.035f, 0.006f };
                    var cols = new List<Transform>();
                    for (int li = 0; li < 4; li++)
                    {
                        var rs = new List<Renderer>();
                        foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.name.StartsWith("UCX_")) continue;
                            if (!r.name.Contains("_LOD" + li)) continue;
                            var mats = r.sharedMaterials.Length > 1 ? r.sharedMaterials : new Material[] { null };
                            if (li == 3)
                            {
                                var bb = AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_" + vn + "_Billboard.png");
                                r.sharedMaterial = Mat("M_" + vn + "_Impostor", bb, null, true, 0.05f);
                            }
                            else
                            {   // sub-mesh 0 = bark, 1 = leaves (single-material meshes: leaves if the name says so)
                                var mf = r.GetComponent<MeshFilter>(); int sub = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
                                if (sub >= 2) r.sharedMaterials = new[] { bark, leaf };
                                else r.sharedMaterial = (sp == "Lantana" || r.name.ToLower().Contains("leaf")) ? leaf : bark;
                            }
                            r.shadowCastingMode = li <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off; rs.Add(r);
                        }
                        if (rs.Count > 0) lods.Add(new LOD(sizes[li], rs.ToArray()));
                    }
                    var lg = v.gameObject.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
                    // trunk collider: the UCX hull, else a capsule
                    bool any = false;
                    foreach (var t in v.GetComponentsInChildren<Transform>(true))
                    {
                        if (!t.name.StartsWith("UCX_")) continue; var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                        if (mf != null && mf.sharedMesh != null) { var mc = t.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true; any = true; }
                        if (mr != null) Object.DestroyImmediate(mr); if (mf != null) Object.DestroyImmediate(mf);
                    }
                    if (!any && sp != "Lantana") { var cc = v.gameObject.AddComponent<CapsuleCollider>(); cc.radius = 0.35f; cc.height = 4f; cc.center = Vector3.up * 2f; }
                    var b = new Bounds(); bool first = true; foreach (var r in v.GetComponentsInChildren<Renderer>()) { if (!r.name.Contains("_LOD0")) continue; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                    Debug.Log("[AgniTrees] " + vn + " size " + b.size + " lods=" + lods.Count + " ucx=" + any);
                    foreach (var t in v.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                    var pf = PrefabUtility.SaveAsPrefabAsset(v.gameObject, D + "Prefabs/" + vn + ".prefab"); outp[vn] = pf; Object.DestroyImmediate(v.gameObject);
                }
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets(); return outp;
        }
    }
}
