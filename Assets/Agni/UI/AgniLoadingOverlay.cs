// Agni Royale — v0.2.1 loading overlay between the lobby and the match: progress bar, tips, smooth fade in/out.
// One screen-space canvas kept across the scene change (DontDestroyOnLoad); hides itself once the match is ready.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Agni.UI
{
    public class AgniLoadingOverlay : MonoBehaviour
    {
        static AgniLoadingOverlay inst;
        CanvasGroup group; RectTransform fill; Text pct, tip; float shown, target; bool hiding;
        static readonly string[] Tips = {
            "Tip: KUDO! dabao jab chaho — joystick se jahan chaho waha utro",
            "Tip: chamakta beam = achha loot (neela / baingani)",
            "Tip: Agni Rekha ke bahar mat raho — har second HP kam hota hai",
            "Tip: gaadi me baith ke zone tak jaldi pahuncho",
            "Tip: ⚙ me GRAPHICS LOW karo agar phone garam ho ya lag kare",
            "Tip: squad ke saath khelo — gire hue saathi ko utha sakte ho",
        };
        public static float Progress { set { if (inst != null) inst.target = Mathf.Max(inst.target, Mathf.Clamp01(value)); } }

        public static void Show()
        {
            if (inst != null) { inst.hiding = false; inst.group.alpha = 1; return; }
            var go = new GameObject("AgniLoadingOverlay"); DontDestroyOnLoad(go); inst = go.AddComponent<AgniLoadingOverlay>(); inst.Build();
        }
        public static void Hide() { if (inst != null && !inst.hiding) { inst.hiding = true; inst.StartCoroutine(inst.FadeOut()); } }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 5000;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(2400, 1080); sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 1; group.blocksRaycasts = true;   // no stray taps while loading
            var bg = K.Img(transform, "Bg", null, Pal.Hex("#140E1A")); bg.rectTransform.Fill();
            var art = K.Img(transform, "Art", "poster_s1", new Color(1, 1, 1, 0.35f)); art.rectTransform.Fill(); art.preserveAspect = false; art.raycastTarget = false;
            var title = K.Txt(transform, "Title", "AGNI ROYALE", 96, Pal.Gold, TextAnchor.MiddleCenter, true); title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 140));
            var sub = K.Txt(transform, "Sub", "Match taiyaar ho raha hai…", 40, Pal.Cream, TextAnchor.MiddleCenter, true); sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 70));
            var barBg = K.Img(transform, "BarBg", null, new Color(1, 1, 1, 0.15f)); barBg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1100, 34));
            fill = K.Img(barBg.transform, "Fill", null, Pal.Saffron).rectTransform; fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, 0.5f); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            pct = K.Txt(transform, "Pct", "0%", 36, Color.white, TextAnchor.MiddleCenter, true); pct.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(400, 60));
            tip = K.Txt(transform, "Tip", Tips[Random.Range(0, Tips.Length)], 34, Pal.CreamDim, TextAnchor.MiddleCenter); tip.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(2000, 80));
            StartCoroutine(Watch());
        }
        IEnumerator Watch()
        {
            float t0 = Time.unscaledTime, tipAt = t0 + 4f;
            while (!hiding)
            {
                // after the scene switch the match builds villages/loot: creep forward so it never looks frozen
                if (target >= 0.6f) target = Mathf.Min(0.95f, target + Time.unscaledDeltaTime * 0.05f);
                if (Agni.Net.AgniLocalPlayer.Instance != null && Agni.Net.AgniLocalPlayer.Instance.P != null) { target = 1f; if (shown > 0.99f) { Hide(); break; } }
                if (Time.unscaledTime - t0 > 90f) { Hide(); break; }
                if (Time.unscaledTime > tipAt) { tipAt = Time.unscaledTime + 4f; tip.text = Tips[Random.Range(0, Tips.Length)]; }
                yield return null;
            }
        }
        void Update()
        {
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.5f);
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Max(0.02f, shown), 1);
            if (pct != null) pct.text = Mathf.RoundToInt(shown * 100) + "%";
        }
        IEnumerator FadeOut()
        {
            yield return null;
            while (group.alpha > 0) { group.alpha = Mathf.MoveTowards(group.alpha, 0, Time.unscaledDeltaTime * 2.5f); yield return null; }
            if (inst == this) inst = null; Destroy(gameObject);
        }
    }
}
