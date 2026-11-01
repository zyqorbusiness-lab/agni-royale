// Agni Royale — IMPORT v1 editor proof renders (no play mode): each landmark prefab alone on a grass plane.
//   DISPLAY=:93 Unity -batchmode -force-glcore -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportProof.Landmarks -proofOut <dir>
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agni.Import.EditorTools
{
    public static class AgniImportProof
    {
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
        public static Camera cam;
        public static void Shot(string dir, string name, Vector3 pos, Vector3 look, float fov = 50, int w = 1080, int h = 1350)
        {
            cam.fieldOfView = fov; cam.transform.position = pos; cam.transform.LookAt(look);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render(); cam.Render();
            RenderTexture.active = rt; var t = new Texture2D(w, h, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            Debug.Log("[AgniImportProof] shot " + name);
        }
        public static void Stage(bool ground = true)
        {
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.95f, 0.86f); sun.intensity = 1.35f; sun.shadows = LightShadows.Soft; sunGo.transform.rotation = Quaternion.Euler(47, -38, 0);
            RenderSettings.sun = sun; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f); RenderSettings.ambientEquatorColor = new Color(0.66f, 0.64f, 0.55f); RenderSettings.ambientGroundColor = new Color(0.33f, 0.29f, 0.22f);
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Agni/Net/Arena/M_Sky.mat"); if (sky) RenderSettings.skybox = sky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 160; RenderSettings.fogEndDistance = 900; RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.84f);
            if (ground)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane); g.transform.localScale = new Vector3(80, 1, 80);
                var gm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Agni/Net/Arena/M_Ground.mat"); if (gm) g.GetComponent<Renderer>().sharedMaterial = gm;
            }
            var camGo = new GameObject("ProofCam"); cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 2000; cam.nearClipPlane = 0.1f;
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }
        /// the real arena scene + villages + landmarks (as AgniArenaBootstrap builds them), shot from the air and from the ground
        public static void Arena()
        {
            int code = 0;
            try
            {
                var dir = Arg("-proofOut", "/tmp/agni_imp_proof"); Directory.CreateDirectory(dir); var only = Arg("-only", "");
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(Agni.Net.EditorTools.AgniNetSetup.ArenaPath, OpenSceneMode.Single);
                Agni.Net.AgniVillages.Root = null; Agni.Net.AgniVillages.Build(null); AgniLandmarks.Root = null; AgniLandmarks.Build(null);
                var camGo = new GameObject("ProofCam"); cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 2500; cam.nearClipPlane = 0.2f;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != cam) c.enabled = false;
                void S(string n, Vector3 p, Vector3 l, float fov = 50, int w = 1080, int h = 1350) { if (only == "" || n.Contains(only)) Shot(dir, n, p, l, fov, w, h); }
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.name == "Sea" || r.name == "Ground") foreach (var m in r.sharedMaterials) Debug.Log("[AgniImportProof] " + r.name + " mat " + m.name + " shader " + m.shader.name + " supported " + m.shader.isSupported + " passes " + m.passCount + " layer " + r.gameObject.layer + " enabled " + r.enabled + " pos " + r.transform.position);
                bool fog = RenderSettings.fog; RenderSettings.fog = false;
                S("arena_overview", new Vector3(0, 520, -640), new Vector3(0, 0, -40), 55, 1350, 1080);
                S("arena_top", new Vector3(0, 1250, -1), Vector3.zero, 46, 1350, 1350);
                RenderSettings.fog = fog;
                foreach (var s in AgniLandmarks.Table)
                {
                    var c = s.pos; var outward = new Vector3(c.x, 0, c.z).normalized; var side = Vector3.Cross(Vector3.up, outward);
                    float R = s.footR;
                    S("lm_" + s.name + "_air", c - outward * R * 1.6f + side * R * 0.5f + Vector3.up * R * 0.9f, c + Vector3.up * 3f, 50);
                    S("lm_" + s.name + "_ground", c - outward * (R + 12f) + side * 6f + Vector3.up * 1.7f + Vector3.up * Mathf.Max(0, Agni.Import.AgniIsland.Height(c.x, c.z)), c + Vector3.up * 6f, 62);
                }
                var gg = AgniLandmarks.Table[0].pos;
                S("gg_tower_top", gg + new Vector3(-10, 31.5f, -14), gg + new Vector3(0, 31.5f, 0), 28);
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
        public static void Landmarks()
        {
            int code = 0;
            try
            {
                var dir = Arg("-proofOut", "/tmp/agni_imp_proof"); Directory.CreateDirectory(dir);
                var only = Arg("-only", "");
                foreach (var n in AgniImportLandmarks.Names)
                {
                    if (only != "" && !n.Contains(only)) continue;
                    Stage();
                    var pf = Resources.Load<GameObject>("AgniImport/Landmarks/" + n); var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                    var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    float R = Mathf.Max(b.extents.x, b.extents.z);
                    var c = b.center; c.y = Mathf.Max(0, b.center.y * 0.5f);
                    Shot(dir, n + "_a_above", c + new Vector3(-R * 1.1f, R * 0.9f, -R * 1.3f), c, 45);
                    Shot(dir, n + "_b_street", c + new Vector3(R * 0.2f, 2.2f, -R * 1.25f), c + Vector3.up * 4f, 60);
                    if (n == "GhantaGharSquare") { Shot(dir, n + "_c_top", new Vector3(6, 31, -12), new Vector3(0, 30.5f, 0), 30); Shot(dir, n + "_d_clock", new Vector3(2, 20, -14), new Vector3(0, 20, 0), 30); }
                    Debug.Log("[AgniImportProof] " + n + " bounds " + b.min.ToString("F1") + " .. " + b.max.ToString("F1"));
                }
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
