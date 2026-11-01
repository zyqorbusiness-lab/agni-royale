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
        void LateUpdate() { if (rt != null) foreach (var c in Camera.allCameras) if (c.targetTexture != rt) c.targetTexture = rt; }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Shot(string name)
        {
            LateUpdate(); yield return Frames(3); Canvas.ForceUpdateCanvases();
            var cams = Camera.allCameras; System.Array.Sort(cams, (a, b) => a.depth.CompareTo(b.depth));
            foreach (var c in cams) { c.targetTexture = rt; c.Render(); }
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
            var p = Path.Combine(dir, name + ".png"); File.WriteAllBytes(p, tex.EncodeToPNG()); Destroy(tex);
            Debug.Log("[AgniUIProof] wrote " + p);
        }

        IEnumerator Start()
        {
            dir = AgniBoot.ProofDir; Directory.CreateDirectory(dir);
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
