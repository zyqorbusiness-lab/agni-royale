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
                var camGo = new GameObject("ProofCam"); cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 9000; cam.nearClipPlane = 0.3f;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != cam) c.enabled = false;
                bool Want(string n) => only == "" || only.Split('|').Any(o => o.Length > 0 && n.Contains(o));
                void S(string n, Vector3 p, Vector3 l, float fov = 50, int w = 1080, int h = 1350) { if (Want(n)) Shot(dir, n, p, l, fov, w, h); }
                float Y(Vector3 p) => AgniIndia.H(p.x, p.z);
                bool fog = RenderSettings.fog;
                if (Want("india_top"))
                {   // top-down of the whole map (orthographic), plus where every place lands in the picture (labels are drawn on afterwards)
                    RenderSettings.fog = false; cam.orthographic = true; cam.orthographicSize = 2080; cam.farClipPlane = 9000;
                    var center = new Vector3(448, 0, 60);
                    var labels = new System.Text.StringBuilder("[");
                    cam.transform.SetPositionAndRotation(center + Vector3.up * 3000, Quaternion.Euler(90, 0, 0));
                    var rt = new RenderTexture(1350, 1350, 24); cam.targetTexture = rt; cam.aspect = 1;
                    void Lab(string n, Vector3 w, string kind) { var sp = cam.WorldToViewportPoint(w); labels.Append("{\"n\":\"" + n + "\",\"k\":\"" + kind + "\",\"x\":" + (sp.x * 1350).ToString("F0") + ",\"y\":" + ((1 - sp.y) * 1350).ToString("F0") + "},"); }
                    foreach (var s in AgniLandmarks.Table) Lab(s.display, s.pos, "landmark");
                    foreach (var v in Agni.Net.AgniVillages.Villages) Lab(v.name, new Vector3(v.c.x, 0, v.c.y), v.r >= 20 ? "village" : "spot");
                    File.WriteAllText(Path.Combine(dir, "india_top_labels.json"), labels.ToString().TrimEnd(',') + "]");
                    cam.Render(); RenderTexture.active = rt; var tex = new Texture2D(1350, 1350, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1350, 1350), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, "india_top.png"), tex.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; cam.orthographic = false; RenderSettings.fog = fog;
                    Debug.Log("[AgniImportProof] shot india_top");
                }
                var gg = AgniLandmarks.Table[0].pos;
                S("gg_establish", gg + new Vector3(-118f, 24f, -62f), gg + new Vector3(0, 9f, 0), 56);
                S("gg_playerA", gg + new Vector3(-24f, 1.75f, 12f), gg + new Vector3(0, 14f, 0), 66);
                S("gg_playerC", gg + new Vector3(-40f, 1.75f, 4f), gg + new Vector3(0, 13f, 0), 62);
                S("gg_tower_top", gg + new Vector3(-10, 31.5f, -14), gg + new Vector3(0, 31.5f, 0), 28);
                foreach (int ang in new[] { 0, 45, 135, 180, 225, 315 }) foreach (float dist in new[] { 48f, 66f })
                    {   if (dist > 50f && ang % 180 != 0) continue;   // ground level from the grass around the square, looking at the clock tower
                        var dv = Quaternion.Euler(0, ang, 0) * Vector3.forward; var cp = gg + dv * dist; cp.y = Mathf.Max(Y(cp), 0.2f) + 1.65f;
                        S("gg_lawn_" + ang + "_" + (int)dist, cp, gg + new Vector3(0, 13f, 0), 60);
                    }
                foreach (var s2 in AgniLandmarks.Table)
                {
                    if (s2.name == "GhantaGharSquare") continue;
                    var c2 = s2.pos; var o2 = new Vector3(c2.x, 0, c2.z).normalized; if (o2.sqrMagnitude < 0.5f) o2 = Vector3.forward; var sd = Vector3.Cross(Vector3.up, o2);
                    var cp = c2 - o2 * (s2.footR * 2.0f + 25f) + sd * s2.footR * 0.6f; cp.y = Mathf.Max(Y(cp), 0) + 22f;
                    S("est_" + s2.name, cp, c2 + Vector3.up * 5f, 56);
                }
                foreach (var v in Agni.Net.AgniVillages.Villages) { if (v.r < 25) continue; var c3 = new Vector3(v.c.x, 0, v.c.y); var cp = c3 + new Vector3(-v.r * 1.6f, 0, -v.r * 1.6f); cp.y = Y(cp) + 22f; S("vil_" + v.name.Replace(" ", ""), cp, c3 + Vector3.up * (Y(c3) + 2f), 58); }
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
