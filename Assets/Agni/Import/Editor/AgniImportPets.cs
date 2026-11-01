// Agni Royale — IMPORT v1: lobby pets. Cute cubs (pets/: Bagha, Bagha_White, Sheru, Gajju, Mor; 100% ours) for LOW/NORMAL and the real tigers
// (pets_real/: Bagha_Real, White_Tiger_Real; CC BY 4.0 base mesh, credit line in Pets/CREDITS_pets_real.md) for HIGH/VERY HIGH.
//   Unity -batchmode -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportPets.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import.EditorTools
{
    public class AgniPetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Agni/Import/Pets/")) return;
            var mi = (ModelImporter)assetImporter; mi.animationType = ModelImporterAnimationType.Legacy; mi.importAnimation = true; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false; mi.meshCompression = ModelImporterMeshCompression.Low; mi.bakeAxisConversion = true;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Agni/Import/Pets/")) return;
            var ti = (TextureImporter)assetImporter; ti.maxTextureSize = 2048;
            if (assetPath.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("MetallicSmoothness")) ti.sRGBTexture = false;
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 1024; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
    }

    public static class AgniImportPets
    {
        const string SRC = "Assets/Agni/Import/Pets/", OUT = "Assets/Agni/Import/Resources/AgniImport/Pets/";
        public static void Batch() { int c = 0; try { Run(); } catch (System.Exception e) { Debug.LogException(e); c = 1; } EditorApplication.Exit(c); }
        static Material Mat(string p, System.Action<Material> f)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p); if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            f(m); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static Texture2D T(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        public static void Run()
        {
            Directory.CreateDirectory(OUT);
            foreach (var name in new[] { "Bagha", "Bagha_White", "Sheru", "Gajju", "Mor", "Bagha_Real", "White_Tiger_Real" })
            {
                var dir = SRC + name + "/"; var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".fbx"); if (fbx == null) { Debug.LogWarning("[AgniPetImport] missing " + name); continue; }
                bool real = name.Contains("Real");
                Material body, eye;
                if (real)
                {
                    body = Mat(dir + "M_" + name + "_Body.mat", m => { m.SetTexture("_BaseMap", T(dir + name + "_Albedo.png")); m.SetTexture("_BumpMap", T(dir + name + "_Normal.png")); m.EnableKeyword("_NORMALMAP");
                        m.SetTexture("_MetallicGlossMap", T(dir + name + "_MetallicSmoothness.png")); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0); });
                    eye = body;
                }
                else
                {
                    body = Mat(dir + "M_" + name + "_Fur.mat", m => { m.SetTexture("_BaseMap", T(dir + name + "_Albedo.png")); m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0f); });
                    eye = Mat(dir + "M_" + name + "_Eye.mat", m => { m.SetTexture("_BaseMap", T(dir + name + "_Albedo.png")); m.SetFloat("_Smoothness", 0.88f); m.SetFloat("_Metallic", 0f); });
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); go.name = name;
                int nr = 0;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var smr = r as SkinnedMeshRenderer; int n = smr != null && smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1;
                    var ms = new Material[n]; for (int i = 0; i < n; i++) ms[i] = i == 0 ? body : eye; r.sharedMaterials = ms; nr++;
                    r.shadowCastingMode = r.name.EndsWith("_LOD2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                // legacy clips arrive as "<Name>_Rig|Idle": re-add them under their short names
                var anim = go.GetComponent<Animation>(); if (anim == null) anim = go.AddComponent<Animation>();
                var clips = AssetDatabase.LoadAllAssetsAtPath(dir + name + ".fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
                AnimationUtility.SetAnimationClips(anim, clips); // serialized; runtime resolves "Sit" -> "<Name>_Rig|Sit" (AgniPets.Play)
                anim.playAutomatically = false; anim.cullingType = AnimationCullingType.BasedOnRenderers;
                var lg = go.GetComponent<LODGroup>(); if (lg == null) lg = go.AddComponent<LODGroup>();
                var lods = Enumerable.Range(0, 3).Select(i => go.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_LOD" + i)).ToArray()).ToArray();
                float[] sh = { 0.3f, 0.1f, 0.02f }; lg.SetLODs(Enumerable.Range(0, 3).Where(i => lods[i].Length > 0).Select(i => new LOD(sh[i], lods[i])).ToArray()); lg.RecalculateBounds();
                var b = new Bounds(go.transform.position, Vector3.zero); foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.name.EndsWith("_LOD0")) b.Encapsulate(r.bounds);
                PrefabUtility.SaveAsPrefabAsset(go, OUT + name + ".prefab");
                Debug.Log("[AgniPetImport] " + name + ": renderers " + nr + " clips [" + string.Join(",", clips.Select(c => c.name + " " + c.length.ToString("F2") + "s legacy=" + c.legacy)) + "] size " + b.size.ToString("F2") + " lods " + string.Join("/", lods.Select(l => l.Length)));
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
