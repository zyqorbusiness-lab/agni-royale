using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders proof PNGs of the map from batchmode (needs a GPU/GL context: run under xvfb-run WITHOUT -nographics).
//   xvfb-run -a Unity -batchmode -force-glcore -projectPath <proj> -executeMethod AgniProof.RenderBatch -proofOut <dir> -logFile <log>
public static class AgniProof
{
    static string ArgVal(string a, string def)
    {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, a);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
    }
    public static void RenderBatch()
    {
        try { Render(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    [MenuItem("Agni Royale/Render proof images")]
    public static void Render()
    {
        string outDir = ArgVal("-proofOut", Path.GetFullPath(Path.Combine(Application.dataPath, "../../proof/step1")));
        Directory.CreateDirectory(outDir);
        EditorSceneManager.OpenScene(AgniWorldBuilder.ScenePath);
        var labels = UnityEngine.Object.FindObjectsByType<AgniBillboard>(FindObjectsSortMode.None);
        foreach (var t in Terrain.activeTerrains) { t.heightmapPixelError = 1; t.basemapDistance = 5000; t.treeBillboardDistance = 5000; if (ArgVal("-noInst", "0") == "1") t.drawInstanced = false; }
        var go = new GameObject("ProofCam");
        var cam = go.AddComponent<Camera>();
        go.AddComponent<UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.Skybox;

        // --- top-down whole map
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        cam.orthographic = true; cam.orthographicSize = 2075; cam.nearClipPlane = 1; cam.farClipPlane = 3000;
        go.transform.position = new Vector3(2048, 1500, 2048); go.transform.rotation = Quaternion.Euler(90, 0, 0);
        foreach (var l in labels)
        {
            bool big = l.minWorldHeight >= 8f;
            l.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            float h = big ? 64f : 46f; l.transform.localScale = Vector3.one * (h / l.baseHeight);
        }
        // straight-down views of the lit sea come out dark on the software GL renderer: use a flat sea colour for the map shot only
        var sea = GameObject.Find("Sea"); Material seaMat = null;
        if (sea != null) { var sr = sea.GetComponent<MeshRenderer>(); seaMat = sr.sharedMaterial; var um = new Material(Shader.Find("Universal Render Pipeline/Unlit")); um.SetColor("_BaseColor", new Color(0.16f, 0.45f, 0.66f)); sr.sharedMaterial = um; }
        Shot(cam, 2048, 2048, Path.Combine(outDir, "top_down.png"));
        if (sea != null) sea.GetComponent<MeshRenderer>().sharedMaterial = seaMat;
        RenderSettings.fog = fog;

        // --- low-angle views
        cam.orthographic = false; cam.fieldOfView = 55; cam.nearClipPlane = 0.5f; cam.farClipPlane = 4200;
        View(cam, labels, "himalaya", Z(1), new Vector3(-60, 95, -330), new Vector3(0, 40, 80), outDir);
        View(cam, labels, "central_peak", Z(10), new Vector3(-300, 55, -260), new Vector3(0, 45, 0), outDir);
        View(cam, labels, "ganga_plain", Z(6), new Vector3(260, 60, -120), new Vector3(-120, 0, 30), outDir);
        View(cam, labels, "coast_goa", Z(14), new Vector3(-260, 30, -60), new Vector3(20, 6, 10), outDir);
        View(cam, labels, "zone_kolkata", Z(7), new Vector3(-110, 48, -130), new Vector3(0, 8, 0), outDir);
        View(cam, labels, "zone_mumbai_docks", Z(12), new Vector3(-170, 45, -140), new Vector3(0, 8, 0), outDir);
        View(cam, labels, "zone_delhi", Z(3), new Vector3(-90, 45, -150), new Vector3(0, 10, 20), outDir);
        var start = GameObject.Find("Main Camera");
        if (start != null)
        {
            go.transform.SetPositionAndRotation(start.transform.position, start.transform.rotation); cam.fieldOfView = 60;
            foreach (var l in labels) l.Face(cam);
            Shot(cam, 1600, 900, Path.Combine(outDir, "drop_view.png"));
        }
        Debug.Log("[AgniProof] done -> " + outDir);
    }

    static Vector3 Z(int id)
    {
        var t = GameObject.Find("Zones").transform.Cast<Transform>().First(c => c.name.StartsWith($"Zone {id:00} "));
        return t.position;
    }

    static void View(Camera cam, AgniBillboard[] labels, string name, Vector3 zone, Vector3 eyeOff, Vector3 lookOff, string dir)
    {
        Vector3 eye = zone + eyeOff;
        float g = Ground(eye); if (eye.y < g + 3) eye.y = g + 3;
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((zone + lookOff) - eye, Vector3.up);
        foreach (var l in labels) l.Face(cam);
        Shot(cam, 1600, 900, Path.Combine(dir, name + ".png"));
    }

    static float Ground(Vector3 p)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z) return t.SampleHeight(p) + o.y;
        }
        return 0;
    }

    static void Shot(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("[AgniProof] wrote " + path);
    }
}
