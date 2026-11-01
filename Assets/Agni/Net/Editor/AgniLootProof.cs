// Agni Royale — editor-only proof renders for LOOT v1 (no play mode): villages from above, inside a house, street level.
//   Unity -batchmode -force-glcore -projectPath <proj> -executeMethod Agni.Net.EditorTools.AgniLootProof.VillageShots -proofOut <dir> -logFile <log>
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agni.Net.EditorTools
{
    public static class AgniLootProof
    {
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }

        public static void VillageShots()
        {
            int code = 0;
            try
            {
                var dir = Arg("-proofOut", "/tmp/agni_proof"); Directory.CreateDirectory(dir);
                EditorSceneManager.OpenScene(AgniNetSetup.ArenaPath, OpenSceneMode.Single);
                AgniVillages.Root = null; AgniVillages.Build(null);
                var camGo = new GameObject("ProofCam"); var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 1500; cam.nearClipPlane = 0.1f;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != cam) c.enabled = false;
                var only = Arg("-only", "");
                var V = AgniVillages.Villages;
                Vector3 C(int i) => new Vector3(V[i].c.x, 0, V[i].c.y);
                void Shot(string name, Vector3 pos, Vector3 look, float fov = 55, int w = 1080, int h = 1350)
                {
                    if (only != "" && !name.Contains(only)) return;
                    cam.fieldOfView = fov; camGo.transform.position = pos; camGo.transform.LookAt(look);
                    var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render(); cam.Render();
                    RenderTexture.active = rt; var t = new Texture2D(w, h, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
                    File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; rt.Release();
                    Debug.Log("[AgniLootProof] shot " + name);
                }
                // 1. Ghaas Gaon (grass-hut village) from above
                var g = C(1); var outDir = g.normalized;
                Shot("v_ghaas_gaon_above", g + outDir * 34 + Vector3.up * 30 + Vector3.Cross(Vector3.up, outDir) * 10, g + Vector3.up * 1);
                Shot("v_bada_chowk_above", C(0) + new Vector3(-30, 34, -36), C(0));
                var p = C(2); Shot("v_pathar_gaon_street", p + (Vector3.zero - p).normalized * 30 + Vector3.up * 2.2f, p + Vector3.up * 2.5f);
                var gd = C(4); Shot("v_godown_yard", gd + (Vector3.zero - gd).normalized * 40 + Vector3.up * 14f, gd);
                // inside: first Do-Manzila and first Gol Jhopdi / Chhat Ghar
                foreach (var t in AgniVillages.Root.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "Do-Manzila" && (only == "" || "v_inside_domanzila".Contains(only)))
                    {
                        Shot("v_inside_domanzila", t.TransformPoint(new Vector3(-4.2f, 1.9f, -3.6f)), t.TransformPoint(new Vector3(2.5f, 0.6f, 3.0f)), 70);
                        Shot("v_domanzila_terrace", t.TransformPoint(new Vector3(-4.3f, 5.6f, -3.9f)), t.TransformPoint(new Vector3(3.0f, 3.8f, 1.0f)), 70);
                        Shot("v_domanzila_outside", t.TransformPoint(new Vector3(-6f, 3.5f, -16f)), t.TransformPoint(new Vector3(0, 3.0f, 0)), 60);
                        only = only == "" ? "" : only; break;
                    }
                }
                foreach (var t in AgniVillages.Root.GetComponentsInChildren<Transform>()) if (t.name == "Gol Jhopdi") { Shot("v_inside_jhopdi", t.TransformPoint(new Vector3(0, 1.7f, -2.3f)), t.TransformPoint(new Vector3(0.2f, 0.4f, 1.0f)), 75); Shot("v_jhopdi_outside", t.TransformPoint(new Vector3(3f, 2.2f, -9f)), t.TransformPoint(new Vector3(0, 2.0f, 0)), 60); break; }
                foreach (var t in AgniVillages.Root.GetComponentsInChildren<Transform>()) if (t.name == "Chhat Ghar") { Shot("v_chhat_ghar", t.TransformPoint(new Vector3(9f, 4f, -12f)), t.TransformPoint(new Vector3(0, 2.5f, 0)), 60); Shot("v_chhat_roof", t.TransformPoint(new Vector3(-4f, 5.4f, -3f)), t.TransformPoint(new Vector3(4.8f, 3.8f, 3f)), 70); break; }
                foreach (var t in AgniVillages.Root.GetComponentsInChildren<Transform>()) if (t.name == "Pakka Ghar") { Shot("v_pakka_ghar", t.TransformPoint(new Vector3(-7f, 2.5f, -11f)), t.TransformPoint(new Vector3(0, 2.2f, 0)), 60); break; }
                foreach (var t in AgniVillages.Root.GetComponentsInChildren<Transform>()) if (t.name == "Kutcha Ghar") { Shot("v_kutcha_ghar", t.TransformPoint(new Vector3(6f, 2.0f, -9f)), t.TransformPoint(new Vector3(0, 1.8f, 0)), 60); break; }
                Shot("v_overview", new Vector3(0, 260, -260), Vector3.zero, 50, 1350, 1080);
                Debug.Log("[AgniLootProof] spots=" + AgniVillages.Spots.Count);
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
