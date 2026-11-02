// AgniShot "uitest" mode (v0.2.1) — simulated taps through the real EventSystem: GUEST card → lobby → every lobby
// button raycast-checked (is the button really the top thing under a finger?) → ⚙ tapped → START tapped → match → ⚙ in match.
using System.Collections;
using System.Collections.Generic;
using Agni.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Agni.Net
{
    public partial class AgniShot
    {
        int uiFails;
        GameObject TopUnder(Vector2 sp)
        {
            var es = EventSystem.current; if (es == null) return null;
            var ped = new PointerEventData(es) { position = sp }; var hits = new List<RaycastResult>(); es.RaycastAll(ped, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }
        static Vector2 ScreenOf(RectTransform rt)
        {
            var cv = rt.GetComponentInParent<Canvas>(); var cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            var c = new Vector3[4]; rt.GetWorldCorners(c); var mid = (c[0] + c[2]) * 0.5f;
            return RectTransformUtility.WorldToScreenPoint(cam, mid);
        }
        bool Tap(Button b, string label)
        {
            var sp = ScreenOf((RectTransform)b.transform); var top = TopUnder(sp);
            bool ok = top != null && (top == b.gameObject || top.transform.IsChildOf(b.transform));
            if (!ok) { uiFails++; Log("UITEST tap " + label + " at " + sp + " BLOCKED by " + (top != null ? UiPath(top.transform) : "nothing (no EventSystem/raycaster hit)")); return false; }
            var ped = new PointerEventData(EventSystem.current) { position = sp, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(top, ped, ExecuteEvents.pointerDownHandler); ExecuteEvents.ExecuteHierarchy(top, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(top, ped, ExecuteEvents.pointerClickHandler);
            Log("UITEST tap " + label + " ok"); return true;
        }
        static string UiPath(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
        void EsReport(string where)
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None); var cur = EventSystem.current;
            Log("UITEST " + where + ": EventSystems=" + all.Length + " current=" + (cur != null ? cur.name + " in scene '" + cur.gameObject.scene.name + "' module=" + (cur.currentInputModule != null ? cur.currentInputModule.GetType().Name : (cur.GetComponent<BaseInputModule>() != null ? cur.GetComponent<BaseInputModule>().GetType().Name : "none")) : "NONE"));
            if (cur == null) uiFails++;
        }
        void CheckAllButtons(Transform root, string where)
        {
            int ok = 0, bad = 0;
            var targets = new List<Component>(); targets.AddRange(root.GetComponentsInChildren<Button>(false)); targets.AddRange(root.GetComponentsInChildren<Hold>(false));
            foreach (var b in targets)
            {
                if (b is Button bb && (!bb.isActiveAndEnabled || !bb.interactable)) continue;
                if (b is Behaviour be && !be.isActiveAndEnabled) continue;
                var sp = ScreenOf((RectTransform)b.transform); if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                var top = TopUnder(sp); bool hit = top != null && (top == b.gameObject || top.transform.IsChildOf(b.transform));
                if (hit) ok++; else { bad++; Log("UITEST " + where + " control " + UiPath(b.transform) + " covered by " + (top != null ? UiPath(top.transform) : "nothing")); }
            }
            Log("UITEST " + where + ": " + ok + " buttons reachable, " + bad + " covered/unreachable (screen " + Screen.width + "x" + Screen.height + ")");
            uiFails += bad;
        }

        IEnumerator UiTest()
        {
            PlayerPrefs.DeleteKey(AgniProfile.KName); PlayerPrefs.DeleteKey("agni.guestName"); PlayerPrefs.DeleteKey("agni.guest");
            AgniLoadingScreen ls = null; float t0 = Time.time;
            while ((ls = FindAnyObjectByType<AgniLoadingScreen>()) == null && Time.time - t0 < 60) yield return null;
            if (ls == null) { Log("UITEST no loading screen"); uiFails++; yield break; }
            yield return Wait(2f);
            while (!ls.PopupOpen && Time.time - t0 < 90) { ls.OnTap(); yield return Wait(0.4f); }
            EsReport("guest card");
            yield return Wait(1f);
            ls.ConfirmName(); Log("UITEST guest name " + AgniProfile.Name);
            AgniLobby lobby = null; t0 = Time.time;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.time - t0 < 60) yield return null;
            if (lobby == null) { Log("UITEST no lobby"); uiFails++; yield break; }
            yield return Wait(3f);   // the old scene has unloaded by now
            EsReport("lobby");
            var hud = GameObject.Find("LobbyHUD"); if (hud != null) CheckAllButtons(hud.transform, "lobby");
            Button gear = null; foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None)) if (b.name == "Settings" && b.gameObject.scene.name != "DontDestroyOnLoad") gear = b;
            if (gear != null && Tap(gear, "lobby ⚙")) { yield return Wait(0.6f); bool open = lobby.SettingsPanel != null && lobby.SettingsPanel.activeInHierarchy; Log("UITEST lobby settings panel open=" + open + " gfx=" + AgniSettings.Gfx + " fps=" + AgniSettings.Fps); if (!open) uiFails++; else UiShot("uitest_lobby_settings"); if (lobby.SettingsPanel != null) lobby.SettingsPanel.SetActive(false); }
            else if (gear == null) { Log("UITEST lobby gear not found"); uiFails++; }
            Button start = null; foreach (var b in hud != null ? hud.GetComponentsInChildren<Button>() : new Button[0]) if (b.name.ToUpper().Contains("START")) start = b;
            if (start == null) { Log("UITEST START button not found, calling OnStart"); lobby.OnStart(); }
            else if (!Tap(start, "START")) lobby.OnStart();
            float tl = Time.time; bool sawOverlay = false;
            while ((AgniLocalPlayer.Instance == null || AgniLocalPlayer.Instance.P == null) && Time.time - tl < 120) { if (GameObject.Find("AgniLoadingOverlay") != null) sawOverlay = true; yield return null; }
            Log("UITEST loading overlay seen=" + sawOverlay + ", match ready after " + (Time.time - tl).ToString("F1") + " s");
            yield return Wait(4f);
            Log("UITEST overlay still up=" + (GameObject.Find("AgniLoadingOverlay") != null));
            EsReport("match");
            var mh = GameObject.Find("MatchHUD"); Button mg = null;
            if (mh != null) CheckAllButtons(mh.transform, "match HUD");
            var lh = GameObject.Find("LootHUD"); if (lh != null) CheckAllButtons(lh.transform, "loot HUD");
            if (mh != null) foreach (var b in mh.GetComponentsInChildren<Button>()) if (b.name == "Settings") mg = b;
            if (mg != null && Tap(mg, "match ⚙")) { yield return Wait(0.6f); var sp = GameObject.Find("Settings"); Log("UITEST match settings open=" + (sp != null && sp.activeInHierarchy)); UiShot("uitest_match_settings"); }
            else { Log("UITEST match gear missing/blocked"); uiFails++; }
            Log("UITEST loot live=" + AgniPickup.Live.Count + " spawned=" + AgniLoot.Spawned + " gfx=" + AgniSettings.Gfx);
            Log("UITEST RESULT fails=" + uiFails);
        }
        void UiShot(string name) { var tex = RenderAll(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(85)); Destroy(tex); }
    }
}
