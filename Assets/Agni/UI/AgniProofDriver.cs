// Editor-only screenshot driver for the loading / name popup / lobby / settings proof images.
// Active only when the editor was started with "-agniProof <dir>" (see Agni Royale ▸ UI ▸ Render UI proof). Never runs in a build.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Agni.UI
{
    public class AgniProofDriver : MonoBehaviour
    {
        RenderTexture rt; int W = 2400, H = 1080; string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void PreInit()
        {
            if (!AgniBoot.ProofMode) return;
            var g = Arg("-agniGfx"); if (g != null && int.TryParse(g, out int gi)) { PlayerPrefs.SetInt("agni.gfx", gi); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!AgniBoot.ProofMode) return;
            var go = new GameObject("AgniProofDriver"); DontDestroyOnLoad(go); go.AddComponent<AgniProofDriver>();
        }

        void SetSize(int w, int h)
        {
            W = w; H = h;
            if (rt != null) { foreach (var c in Camera.allCameras) if (c.targetTexture == rt) c.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        }
        RenderTexture rtUI;
        /// World cameras into rt; HUD overlay cameras rendered alone into rtUI (transparent) and alpha-composited on the CPU
        /// (Camera.Render() ignores the URP overlay stack in batchmode). Returns the final frame.
        Texture2D RenderAll()
        {
            var cams = Camera.allCameras; System.Array.Sort(cams, (a, b) => a.depth.CompareTo(b.depth));
            var overlays = new System.Collections.Generic.List<Camera>();
            foreach (var c in cams) { if (Overlay(c)) { overlays.Add(c); continue; } c.targetTexture = rt; c.Render(); }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); RenderTexture.active = null;
            if (overlays.Count > 0)
            {
                if (rtUI == null || rtUI.width != W || rtUI.height != H) { if (rtUI != null) rtUI.Release(); rtUI = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; }
                var world = tex.GetPixels32();
                var ui = new Texture2D(W, H, TextureFormat.RGBA32, false);
                foreach (var c in overlays)
                {
                    var d = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    Camera baseCam = null; foreach (var b in cams) { var bd = b.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (bd != null && bd.cameraStack != null && bd.cameraStack.Contains(c)) { baseCam = b; bd.cameraStack.Remove(c); } }
                    var cf = c.clearFlags; var bg = c.backgroundColor;
                    d.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0, 0, 0, 0);
                    c.targetTexture = rtUI; c.Render(); c.targetTexture = null;
                    d.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay; c.clearFlags = cf; c.backgroundColor = bg;
                    if (baseCam != null) baseCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().cameraStack.Add(c);
                    RenderTexture.active = rtUI; ui.ReadPixels(new Rect(0, 0, W, H), 0, 0); RenderTexture.active = null;
                    var u = ui.GetPixels32();
                    for (int i = 0; i < world.Length; i++)
                    {
                        int a = u[i].a; if (a == 0) continue; int ia = 255 - a;
                        // UI shaders blend premultiplied into the cleared target: rgb already carries alpha
                        world[i].r = (byte)Mathf.Min(255, u[i].r + world[i].r * ia / 255);
                        world[i].g = (byte)Mathf.Min(255, u[i].g + world[i].g * ia / 255);
                        world[i].b = (byte)Mathf.Min(255, u[i].b + world[i].b * ia / 255);
                    }
                }
                Destroy(ui); tex.SetPixels32(world);
            }
            tex.Apply(); return tex;
        }
        static bool Overlay(Camera c) { var d = c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); return d != null && d.renderType == UnityEngine.Rendering.Universal.CameraRenderType.Overlay; }
        void LateUpdate() { if (rt != null) foreach (var c in Camera.allCameras) if (!Overlay(c) && c.targetTexture != rt) c.targetTexture = rt; }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Shot(string name)
        {
            LateUpdate(); yield return Frames(3); Canvas.ForceUpdateCanvases();
            var tex = RenderAll();
            var p = Path.Combine(dir, name + ".png"); File.WriteAllBytes(p, tex.EncodeToPNG()); Destroy(tex);
            Debug.Log("[AgniUIProof] wrote " + p);
        }

        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null; }

        // "-agniMode lobby": start straight in the Lobby scene; shots at 3 phone aspect ratios (+ a simulated notch),
        // optional "-agniVideo <frames>" = fixed-timestep 1280x720 JPG frames (24 fps) for the MP4.
        IEnumerator LobbyMode()
        {
            if (string.IsNullOrEmpty(AgniProfile.Name)) AgniProfile.Name = "AgniVeer";
            AgniLobby lobby = null; float t0 = Time.realtimeSinceStartup;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.realtimeSinceStartup - t0 < 60) yield return null;
            if (lobby == null) { Debug.LogError("[AgniUIProof] no lobby"); Quit(1); yield break; }
            string pre = Arg("-agniPrefix") ?? "";
            if (Arg("-agniHeroes") != null)
            {   // IMPORT v1: every hair-v3 hero on the lobby stage (face rig live: blink, look-at the camera, Happy on switch)
                SetSize(3000, 1350); yield return new WaitForSecondsRealtime(2.5f);
                var only = Arg("-agniHeroes");
                for (int i = 0; i < Agni.Net.AgniHeroes.Names.Length; i++)
                {
                    if (only != "all" && !only.Contains(Agni.Net.AgniHeroes.Names[i])) continue;
                    Agni.Net.AgniHeroes.Pref = i; lobby.StepHero(0); yield return new WaitForSecondsRealtime(0.9f);
                    var fc = FindAnyObjectByType<AgniFace>(); Debug.Log("[AgniUIProof] hero " + Agni.Net.AgniHeroes.Names[i] + " face=" + (fc != null && fc.face != null ? fc.face.sharedMesh.blendShapeCount + " shapes, Smile=" + fc.face.GetBlendShapeWeight(fc.face.sharedMesh.GetBlendShapeIndex("Smile")).ToString("F0") + " Eyes_Happy=" + fc.face.GetBlendShapeWeight(fc.face.sharedMesh.GetBlendShapeIndex("Eyes_Happy")).ToString("F0") : "none"));
                    yield return Shot(pre + "hero_" + Agni.Net.AgniHeroes.Names[i]);
                    if (fc != null && fc.head != null)
                    {   // face close-up: same lobby lights and backdrop, a proof camera at head height (the lobby camera is driven every frame)
                        var lc = Camera.main; var hp = fc.head.position + Vector3.up * 0.06f; var toCam = lc.transform.position - hp; toCam.y = 0; toCam.Normalize();
                        var fgo = new GameObject("FaceCam"); var cam = fgo.AddComponent<Camera>(); cam.clearFlags = lc.clearFlags; cam.backgroundColor = lc.backgroundColor; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1600; cam.cullingMask = lc.cullingMask;
                        cam.transform.position = hp + toCam * 1.0f + Vector3.up * 0.02f; cam.transform.LookAt(hp - Vector3.up * 0.1f); cam.fieldOfView = 30;
                        fc.lookTarget = cam.transform; fc.SetExpression(i % 3 == 0 ? "Happy" : i % 3 == 1 ? "Soft_Smile" : "Confident_Smirk", 0.2f, 3f);
                        var off = new System.Collections.Generic.List<Camera>(); foreach (var hc in Camera.allCameras) if (hc != cam) { hc.enabled = false; off.Add(hc); }
                        SetSize(1080, 1350); yield return new WaitForSecondsRealtime(0.8f);
                        yield return Shot(pre + "heroface_" + Agni.Net.AgniHeroes.Names[i]);
                        foreach (var hc in off) hc.enabled = true; Destroy(fgo); fc.lookTarget = lc.transform; SetSize(3000, 1350);
                    }
                }
                Quit(0); yield break;
            }
            SetSize(2400, 1080); yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot(pre + "lobby_2400x1080");
            SetSize(1920, 1080); yield return Frames(4); yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(pre + "lobby_1920x1080");
            SetSize(1600, 720); yield return Frames(4); yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(pre + "lobby_1600x720");
            // simulated punch-hole/notch: 4.5% of the width cut off on the left (safe area), 2400x1080
            SafeArea.ProofInset = new Rect(0.045f, 0f, 0.955f, 1f); SetSize(2400, 1080); yield return Frames(4); yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(pre + "lobby_2400x1080_notch");
            SafeArea.ProofInset = new Rect(0, 0, 1, 1);
            if (Arg("-agniPanels") != null)
            {
                SetSize(2400, 1080);
                lobby.OpenSettings(); yield return new WaitForSecondsRealtime(0.7f); yield return Shot(pre + "settings"); lobby.SettingsPanel.SetActive(false);
                lobby.OpenModes(); yield return new WaitForSecondsRealtime(0.7f); yield return Shot(pre + "modes"); GameObject.Find("ModeSelect")?.SetActive(false);
                lobby.ComingSoon("STORE"); yield return new WaitForSecondsRealtime(0.7f); yield return Shot(pre + "coming_soon"); GameObject.Find("ComingSoon")?.SetActive(false);
            }
            SetSize(1920, 1080); yield return Frames(3); RenderAll();
            Debug.Log("[AgniUIProof] stats batches=" + UnityEditor.UnityStats.batches + " setpass=" + UnityEditor.UnityStats.setPassCalls + " tris=" + UnityEditor.UnityStats.triangles + " verts=" + UnityEditor.UnityStats.vertices + " " + AgniMahal.Stats);
            int vf = 0; int.TryParse(Arg("-agniVideo") ?? "0", out vf);
            if (vf > 0)
            {
                string fd = Path.Combine(dir, "frames"); Directory.CreateDirectory(fd);
                SetSize(1280, 720); yield return Frames(4);
                Time.captureFramerate = 24;
                for (int i = 0; i < vf; i++)
                {
                    yield return null; // (WaitForEndOfFrame never fires in batchmode)
                    var tex = RenderAll();
                    File.WriteAllBytes(Path.Combine(fd, string.Format("f{0:0000}.jpg", i)), tex.EncodeToJPG(92)); Destroy(tex);
                }
                Time.captureFramerate = 0;
                Debug.Log("[AgniUIProof] video frames " + vf);
            }
            Debug.Log("[AgniUIProof] done");
            Quit(0);
        }
        IEnumerator Start()
        {
            dir = AgniBoot.ProofDir; Directory.CreateDirectory(dir);
            if (Arg("-agniMode") == "lobby") { yield return LobbyMode(); yield break; }
            PlayerPrefs.DeleteKey(AgniProfile.KName); AgniSettings.ResetDefaults();
            SetSize(2400, 1080);
            AgniLoadingScreen ls = null; float t0 = Time.realtimeSinceStartup;
            while ((ls = FindAnyObjectByType<AgniLoadingScreen>()) == null && Time.realtimeSinceStartup - t0 < 20) yield return null;
            if (ls == null) { Debug.LogError("[AgniUIProof] no loading screen"); Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Shot("01_loading");
            t0 = Time.realtimeSinceStartup; while (!ls.Ready && Time.realtimeSinceStartup - t0 < 25) yield return null;
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot("02_loading_tap_to_start");
            ls.OnTap(); yield return new WaitForSecondsRealtime(0.6f);
            ls.SetNameText("Ab"); ls.ConfirmName(); yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("03b_name_popup_error");
            ls.SetNameText("AgniVeer"); yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("03_name_popup");
            ls.ConfirmName();
            AgniLobby lobby = null; t0 = Time.realtimeSinceStartup;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.realtimeSinceStartup - t0 < 120) yield return null;
            Debug.Log("[AgniUIProof] lobby after " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s");
            if (lobby == null) { Debug.LogError("[AgniUIProof] lobby never loaded"); Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("04_lobby");
            lobby.OpenSettings(); yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("05_settings");
            lobby.SettingsPanel.SetActive(false);
            lobby.ComingSoon("STORE"); yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("06_coming_soon");
            GameObject.Find("ComingSoon")?.SetActive(false);
            lobby.OpenModes(); yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("07_mode_select");
            GameObject.Find("ModeSelect")?.SetActive(false);
            SetSize(1920, 1080); yield return Frames(4); yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("08_lobby_16x9");
            Debug.Log("[AgniUIProof] done");
            PlayerPrefs.DeleteKey(AgniProfile.KName);
            Quit(0);
        }

        static void Quit(int code) { UnityEditor.EditorApplication.Exit(code); }
    }
}
#endif
