// Agni Royale — Loading screen (season key art, progress bar, rotating Hinglish tips, TAP TO START) + first-launch name popup.
// Everything is built from code at runtime; the scene only holds one GameObject with this component.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Agni.UI
{
    public class AgniLoadingScreen : MonoBehaviour
    {
        public string nextScene = "Lobby";
        public float minSeconds = 3.2f;

        static readonly string[] Tips =
        {
            "Vindhya Shikhar hot drop hai — loot zyada, khatra bhi zyada!",
            "Headphone lagao — dushman ke kadmon ki awaaz pehle sunai degi.",
            "Bambai Mill ki chhat pe seedhi nahi hai — wahan sirf parachute se pahunchoge.",
            "Safe zone chhota hota rahega — map pe nazar rakho, der mat karo.",
            "Squad me khelo, gire hue dost ko utha lo — dosti pakki, jeet pakki.",
            "Purana phone hai? Settings me Graphics 'Smooth' karo, game makkhan chalega.",
            "Ghanta Ghar ki chhat se poora chowk dikhta hai — sniper ke liye best jagah.",
            "Chai Bagan ki jhaadiyon me chhupna aasaan hai — par hilna mat!",
        };

        Camera cam; Canvas canvas; RectTransform safe;
        Image barFill; Text pct, tip, tap; CanvasGroup barGroup, tipGroup, tapGroup, fade;
        RectTransform barRect; Image barGlow;
        AsyncOperation op; float shown; bool ready, started;
        GameObject namePopup; InputField nameField; Text nameErr;
        public bool Ready => ready;
        public bool PopupOpen => namePopup != null && namePopup.activeSelf;

        void Awake() { Build(); }

        void Build()
        {
            var camGo = new GameObject("LoadingCamera"); cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Pal.TealDeep; cam.orthographic = true; cam.cullingMask = 1 << 5;
            camGo.AddComponent<AudioListener>();
            canvas = CanvasMaker.Make("LoadingCanvas", cam, 1f, 0, out safe);
            CanvasMaker.EnsureEventSystem();
            var root = canvas.transform;

            // key art, envelope-fit (fills 20:9 and 16:9, crops edges)
            var art = K.R("KeyArt", root); art.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2400, 1080));
            var artImg = art.gameObject.AddComponent<Image>(); artImg.sprite = K.S("poster_s1"); artImg.raycastTarget = false;
            var arf = art.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 2400f / 1080f;
            art.gameObject.AddComponent<SlowZoom>();
            // bottom shade for the bar + tips
            var shade = K.Img(root, "BottomShade", "vgrad", Pal.A(Pal.TealDeep, 0.95f)); shade.rectTransform.anchorMin = new Vector2(0, 0); shade.rectTransform.anchorMax = new Vector2(1, 0); shade.rectTransform.pivot = new Vector2(0.5f, 0); shade.rectTransform.sizeDelta = new Vector2(0, 360);
            shade.rectTransform.anchoredPosition = Vector2.zero;

            art.SetAsFirstSibling(); shade.transform.SetSiblingIndex(1);
            // logo (top-left)
            Logo(safe, new Vector2(0, 1), new Vector2(60, -40), 84);

            // progress bar
            var bar = K.R("Bar", safe).Place(new Vector2(0.5f, 0), new Vector2(0, 88), new Vector2(1500, 64), new Vector2(0.5f, 0.5f));
            barRect = bar; barGroup = bar.gameObject.AddComponent<CanvasGroup>();
            var track = K.Img(bar, "Track", "rr_16", Pal.A(Color.black, 0.55f)); track.rectTransform.Fill(0, 18, 0, 18);
            var trackOl = K.Img(bar, "TrackOutline", "ol_16", Pal.A(Pal.Gold, 0.55f)); trackOl.rectTransform.Fill(-4, 14, -4, 14);
            var fillMask = K.R("FillArea", bar); fillMask.Fill(4, 22, 4, 22);
            barFill = K.Img(fillMask, "Fill", "btno_16", Color.white); barFill.rectTransform.anchorMin = Vector2.zero; barFill.rectTransform.anchorMax = new Vector2(0, 1); barFill.rectTransform.pivot = new Vector2(0, 0.5f); barFill.rectTransform.offsetMin = Vector2.zero; barFill.rectTransform.offsetMax = Vector2.zero;
            barGlow = K.Img(fillMask, "Spark", "glow", Pal.A(Pal.Gold, 0.95f)); barGlow.rectTransform.sizeDelta = new Vector2(90, 90); barGlow.rectTransform.anchorMin = barGlow.rectTransform.anchorMax = new Vector2(0, 0.5f);
            pct = K.Txt(bar, "Pct", "0%", 40, Pal.Cream, TextAnchor.MiddleRight, true); pct.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(150, 0), new Vector2(140, 60), new Vector2(1, 0.5f)); pct.Shadow(Pal.A(Color.black, 0.6f));
            var loadLbl = K.Txt(bar, "LoadingLbl", "Loading…", 34, Pal.CreamDim, TextAnchor.MiddleLeft); loadLbl.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 54), new Vector2(400, 50), new Vector2(0, 0.5f)); loadLbl.fontStyle = FontStyle.Bold;

            // tip line
            var tipRt = K.R("Tip", safe).Place(new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(1900, 70), new Vector2(0.5f, 0.5f));
            tipGroup = tipRt.gameObject.AddComponent<CanvasGroup>();
            var pill = K.Img(tipRt, "Pill", "rr_24", Pal.A(Color.black, 0.45f)); pill.rectTransform.Fill(); 
            var tag = K.Img(tipRt, "Tag", "btn_24", Pal.SaffronDeep); tag.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(130, 52), new Vector2(0, 0.5f));
            var tagT = K.Txt(tag.transform, "T", "TIP", 34, Color.white, TextAnchor.MiddleCenter, true); tagT.rectTransform.Fill();
            tip = K.Txt(tipRt, "Text", Tips[0], 38, Pal.Cream, TextAnchor.MiddleLeft); tip.rectTransform.Fill(170, 0, 20, 0); tip.fontStyle = FontStyle.Bold;

            // tap to start
            var tapRt = K.R("Tap", safe).Place(new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1200, 110), new Vector2(0.5f, 0.5f));
            tapGroup = tapRt.gameObject.AddComponent<CanvasGroup>(); tapGroup.alpha = 0; tapGroup.blocksRaycasts = false;
            var tapGlow = K.Img(tapRt, "Glow", "glow", Pal.A(Pal.Saffron, 0.55f)); tapGlow.rectTransform.Fill(-120, -60, -120, -60);
            tap = K.Txt(tapRt, "Text", "TAP TO START", 78, Color.white, TextAnchor.MiddleCenter, true); tap.rectTransform.Fill();
            tap.Outline(Pal.Ink, 4); tap.Shadow(Pal.A(Color.black, 0.5f), 0, -7);

            // version
            var ver = K.Txt(safe, "Version", "v0.2 test · Zyqor Studio", 26, Pal.A(Pal.Cream, 0.7f), TextAnchor.LowerRight); ver.rectTransform.Place(new Vector2(1, 0), new Vector2(-30, 14), new Vector2(600, 40), new Vector2(1, 0));

            // full-screen tap catcher (behind popup)
            var catcher = K.R("TapCatcher", root); catcher.Fill();
            var ci = catcher.gameObject.AddComponent<Image>(); ci.color = new Color(0, 0, 0, 0);
            var cb = catcher.gameObject.AddComponent<Button>(); cb.transition = Selectable.Transition.None; cb.onClick.AddListener(OnTap);

            // fade overlay
            var f = K.Img(root, "Fade", null, Color.black); f.rectTransform.Fill(); fade = f.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 1; fade.blocksRaycasts = false;
        }

        public static void Logo(Transform parent, Vector2 anchor, Vector2 pos, int size)
        {
            var rt = K.R("Logo", parent).Place(anchor, pos, new Vector2(size * 7.2f, size * 1.7f), anchor);
            var t = K.Txt(rt, "AgniRoyale", "AGNI ROYALE", size, Color.white, anchor.x < 0.5f ? TextAnchor.UpperLeft : TextAnchor.UpperCenter, true);
            t.rectTransform.Fill();
            var g = t.gameObject.AddComponent<Agni.Intro.VerticalGradient>(); g.top = Pal.Hex("#FFE27A"); g.bottom = Pal.Hex("#FF6A1F");
            t.Outline(Pal.Hex("#3B1406"), Mathf.Max(3, size / 18f)); t.Shadow(Pal.A(Color.black, 0.45f), 0, -size / 12f);
            var sub = K.Txt(rt, "Studio", "ZYQOR STUDIO", Mathf.RoundToInt(size * 0.32f), Pal.Cream, anchor.x < 0.5f ? TextAnchor.LowerLeft : TextAnchor.LowerCenter, true);
            sub.rectTransform.Fill(anchor.x < 0.5f ? 6 : 0, -size * 0.1f, 0, 0); sub.Shadow(Pal.A(Color.black, 0.6f), 0, -3);
        }

        IEnumerator Start()
        {
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            { op = SceneManager.LoadSceneAsync(nextScene); op.allowSceneActivation = false; }
            float t = 0; int tipI = Random.Range(0, Tips.Length); tip.text = Tips[tipI]; float tipT = 0;
            while (true)
            {
                float dt = Time.unscaledDeltaTime; t += dt; tipT += dt;
                if (!started) fade.alpha = Mathf.MoveTowards(fade.alpha, 0, dt * 2.5f);
                float real = op != null ? Mathf.Clamp01(op.progress / 0.9f) : 1f;
                float target = Mathf.Min(real, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / minSeconds)));
                shown = Mathf.MoveTowards(shown, target, dt * 0.9f);
                SetBar(shown);
                // tips rotate every 3.2 s with a soft fade
                float ph = tipT % 3.2f; tipGroup.alpha = Mathf.Clamp01(Mathf.Min(ph / 0.25f, (3.2f - ph) / 0.25f));
                if (tipT >= 3.2f) { tipT -= 3.2f; tipI = (tipI + 1) % Tips.Length; tip.text = Tips[tipI]; }
                if (!ready && shown >= 0.999f) { ready = true; }
                if (ready)
                {
                    barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, 0, dt * 4);
                    tapGroup.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 2.4f));
                    float s = 1 + 0.03f * Mathf.Sin(t * 4.8f); tap.transform.localScale = new Vector3(s, s, 1);
                }
                yield return null;
            }
        }

        public void SetBar(float k)
        {
            barFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, k), 1);
            barGlow.rectTransform.anchorMin = barGlow.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, k), 0.5f);
            pct.text = Mathf.RoundToInt(k * 100) + "%";
        }

        public void OnTap()
        {
            if (!ready || started || PopupOpen) return;
            if (!AgniProfile.HasName) { ShowNamePopup(); return; }
            Go();
        }

        void Go()
        {
            if (started) return; started = true;
            StartCoroutine(GoRoutine());
        }
        IEnumerator GoRoutine()
        {
            while (fade.alpha < 1) { fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * 4); yield return null; }
            if (op != null) op.allowSceneActivation = true; else SceneManager.LoadScene(nextScene);
        }

        // ---------------- first-launch name popup
        public void ShowNamePopup()
        {
            if (namePopup != null) { namePopup.SetActive(true); return; }
            var card = K.Modal(canvas.transform, "NamePopup", new Vector2(1240, 700), out namePopup);
            namePopup.transform.SetSiblingIndex(canvas.transform.childCount - 2); // under the fade
            var title = K.Txt(card, "Title", "APNA NAAM LIKHO", 64, Pal.Ink, TextAnchor.MiddleLeft, true); title.rectTransform.Place(new Vector2(0, 1), new Vector2(380, -75), new Vector2(800, 100), new Vector2(0, 0.5f));
            var mascot = K.Icon(card, "mascot_wink", new Vector2(300, 412), Color.white); mascot.rectTransform.Place(new Vector2(0, 0), new Vector2(50, 40), new Vector2(300, 412), new Vector2(0, 0));
            mascot.gameObject.AddComponent<Bob>().amp = 8;
            var sub = K.Txt(card, "Sub", "Yahi naam match me sabko dikhega. 4–12 akshar.", 36, Pal.Hex("#6B4A3A"), TextAnchor.MiddleLeft); sub.rectTransform.Place(new Vector2(0, 1), new Vector2(380, -205), new Vector2(820, 60), new Vector2(0, 0.5f));

            // input
            var inRt = K.R("NameField", card).Place(new Vector2(0, 1), new Vector2(380, -310), new Vector2(800, 110), new Vector2(0, 0.5f));
            var inBg = inRt.gameObject.AddComponent<Image>(); inBg.sprite = K.S("rr_24"); inBg.type = Image.Type.Sliced; inBg.color = Color.white;
            var inOl = K.Img(inRt, "Outline", "ol_24", Pal.Saffron); inOl.rectTransform.Fill(-3, -3, -3, -3);
            var txt = K.Txt(inRt, "Text", "", 52, Pal.Ink, TextAnchor.MiddleLeft, true); txt.rectTransform.Fill(36, 0, 36, 0); txt.supportRichText = false;
            var ph = K.Txt(inRt, "Placeholder", "Player name", 48, Pal.Hex("#B9A796"), TextAnchor.MiddleLeft); ph.rectTransform.Fill(36, 0, 36, 0); ph.fontStyle = FontStyle.Italic;
            nameField = inRt.gameObject.AddComponent<InputField>(); nameField.textComponent = txt; nameField.placeholder = ph; nameField.targetGraphic = inBg;
            nameField.characterLimit = 12; nameField.lineType = InputField.LineType.SingleLine; nameField.caretColor = Pal.SaffronDeep; nameField.caretWidth = 4; nameField.selectionColor = Pal.A(Pal.Saffron, 0.35f);
            var count = K.Txt(inRt, "Count", "0/12", 32, Pal.Hex("#B9A796"), TextAnchor.MiddleRight, true); count.rectTransform.Fill(0, 0, 30, 0);
            nameErr = K.Txt(card, "Error", "", 32, Pal.Hex("#D93025"), TextAnchor.MiddleLeft); nameErr.rectTransform.Place(new Vector2(0, 1), new Vector2(384, -400), new Vector2(800, 50), new Vector2(0, 0.5f)); nameErr.fontStyle = FontStyle.Bold;
            nameField.onValueChanged.AddListener(v => { count.text = v.Length + "/12"; nameErr.text = ""; });

            var ok = K.Btn(card, "Confirm", "PAKKA!", Pal.Saffron, 60, ConfirmName, "btn_24");
            ((RectTransform)ok.transform).Place(new Vector2(0, 0), new Vector2(380, 70), new Vector2(800, 130), new Vector2(0, 0));
            namePopup.AddComponent<PopIn>();
        }

        public void SetNameText(string n) { if (nameField != null) nameField.text = n; }

        public void ConfirmName()
        {
            string n = (nameField.text ?? "").Trim();
            var err = AgniProfile.Validate(n);
            if (err != null) { nameErr.text = err; return; }
            AgniProfile.Name = n;
            namePopup.SetActive(false);
            Go();
        }
    }

    public class SlowZoom : MonoBehaviour
    {
        void Update() { float s = 1.0f + 0.035f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.25f)); transform.localScale = new Vector3(s, s, 1); }
    }
    public class Bob : MonoBehaviour
    {
        public float amp = 10, speed = 2.2f; Vector2 basePos; bool init;
        void Update()
        {
            var rt = transform as RectTransform; if (!init) { basePos = rt != null ? rt.anchoredPosition : (Vector2)transform.localPosition; init = true; }
            float y = Mathf.Sin(Time.unscaledTime * speed) * amp;
            if (rt != null) rt.anchoredPosition = basePos + new Vector2(0, y); else transform.localPosition = (Vector3)basePos + new Vector3(0, y, 0);
        }
    }
    public class PopIn : MonoBehaviour
    {
        float t; Transform card;
        void OnEnable() { t = 0; card = transform.Find("Card"); Update(); }
        void Update()
        {
            if (card == null) return; t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.28f); float e = 1 + 2.2f * Mathf.Pow(k - 1, 3) + 1.2f * Mathf.Pow(k - 1, 2);
            float s = Mathf.Lerp(0.82f, 1f, e); card.localScale = new Vector3(s, s, 1);
            var cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); cg.alpha = Mathf.Clamp01(t / 0.15f);
        }
    }
}
