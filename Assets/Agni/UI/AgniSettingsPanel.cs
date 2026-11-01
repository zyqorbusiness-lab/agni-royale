// Agni Royale — Settings panel: graphics preset, FPS, sensitivity, sound, music, language. Saved in PlayerPrefs (AgniSettings), applied instantly.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Agni.UI
{
    public static class AgniSettingsPanel
    {
        public static GameObject Build(Transform canvas)
        {
            var card = K.Modal(canvas, "Settings", new Vector2(1640, 1000), out var root);
            var title = K.Txt(card, "Title", "SETTINGS", 64, Pal.Ink, TextAnchor.MiddleLeft, true); title.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -75), new Vector2(700, 100), new Vector2(0, 0.5f));
            var hint = K.Txt(card, "Hint", "Badlaav turant save ho jata hai", 30, Pal.Hex("#8A6A55"), TextAnchor.MiddleLeft); hint.rectTransform.Place(new Vector2(0, 1), new Vector2(390, -80), new Vector2(700, 60), new Vector2(0, 0.5f)); hint.fontStyle = FontStyle.Bold;
            K.CloseX(card, () => root.SetActive(false));

            float y = -212; const float row = 106;
            Segmented(card, "GRAPHICS", y, new[] { "SMOOTH", "STANDARD", "ULTRA" }, () => (int)AgniSettings.Gfx, i => AgniSettings.Gfx = (GfxPreset)i, new bool[] { true, true, true },
                new[] { "Purane phone ke liye", "Balanced", "Best look, zyada battery" }); y -= row;
            Segmented(card, "FPS", y, new[] { "30 FPS", "60 FPS" }, () => AgniSettings.Fps >= 60 ? 1 : 0, i => AgniSettings.Fps = i == 1 ? 60 : 30, new[] { true, true }, null); y -= row;
            SliderRow(card, "SENSITIVITY", y, 0.2f, 2.5f, () => AgniSettings.Sensitivity, v => AgniSettings.Sensitivity = v, v => v.ToString("0.0") + "x"); y -= row;
            SliderRow(card, "SOUND EFFECTS", y, 0, 1, () => AgniSettings.Sfx, v => AgniSettings.Sfx = v, v => Mathf.RoundToInt(v * 100) + "%"); y -= row;
            SliderRow(card, "MUSIC", y, 0, 1, () => AgniSettings.Music, v => AgniSettings.Music = v, v => Mathf.RoundToInt(v * 100) + "%"); y -= row;
            Segmented(card, "LANGUAGE", y, new[] { "ENGLISH", "HINGLISH", "HINDI" }, () => AgniSettings.Language, i => AgniSettings.Language = i, new[] { true, true, false }, new[] { "", "", "Jaldi" });

            var reset = K.Btn(card, "Reset", "RESET", Pal.Hex("#7D8C8F"), 40, () => { AgniSettings.ResetDefaults(); }, "btn_24");
            ((RectTransform)reset.transform).Place(new Vector2(1, 0), new Vector2(-420, 40), new Vector2(300, 96), new Vector2(1, 0));
            var done = K.Btn(card, "Done", "DONE", Pal.Saffron, 46, () => root.SetActive(false), "btn_24");
            ((RectTransform)done.transform).Place(new Vector2(1, 0), new Vector2(-60, 40), new Vector2(330, 96), new Vector2(1, 0));
            var note = K.Txt(card, "Note", "Controls layout editor + Google login: agle update me", 28, Pal.Hex("#8A6A55"), TextAnchor.MiddleLeft); note.rectTransform.Place(new Vector2(0, 0), new Vector2(60, 88), new Vector2(900, 50), new Vector2(0, 0.5f)); note.fontStyle = FontStyle.Italic;
            root.AddComponent<PopIn>();
            return root;
        }

        static RectTransform Row(RectTransform card, string label, float y)
        {
            var r = K.R("Row_" + label, card); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(-120, 92);
            var bg = K.Img(r, "Bg", "rr_24", Pal.Hex("#FBE9CF")); bg.rectTransform.Fill();
            var t = K.Txt(r, "Label", label, 38, Pal.Ink, TextAnchor.MiddleLeft, true); t.rectTransform.Fill(34, 0, 0, 0);
            return r;
        }

        static void Segmented(RectTransform card, string label, float y, string[] opts, Func<int> get, Action<int> set, bool[] enabled, string[] subs)
        {
            var r = Row(card, label, y);
            float w = opts.Length == 2 ? 300 : 300, gap = 16, x0 = 440;
            var imgs = new List<Image>(); var txts = new List<Text>();
            void Refresh()
            {
                int cur = get();
                for (int i = 0; i < imgs.Count; i++)
                {
                    bool on = i == cur, en = enabled[i];
                    imgs[i].sprite = K.S(on && en ? "btno_16" : "btn_16"); imgs[i].color = !en ? Pal.Hex("#E2D6C6") : Color.white;
                    txts[i].color = !en ? Pal.Hex("#A89A8C") : on ? Color.white : Pal.Hex("#6B4A3A");
                }
            }
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                var b = K.Btn(r, "Opt_" + opts[i], null, Color.white, 10, () => { if (enabled[idx]) { set(idx); Refresh(); } }, "btn_16");
                var br = (RectTransform)b.transform; br.Place(new Vector2(0, 0.5f), new Vector2(x0 + i * (w + gap), 0), new Vector2(w, 74), new Vector2(0, 0.5f));
                var sh = br.Find("Shadow"); if (sh != null) ((RectTransform)sh).Fill(0, -4, 0, 4);
                var t = K.Txt(br, "T", opts[i], 32, Color.white, TextAnchor.MiddleCenter, true); t.rectTransform.Fill(0, subs != null && !string.IsNullOrEmpty(subs[i]) ? 14 : 0, 0, 0);
                if (subs != null && !string.IsNullOrEmpty(subs[i])) { var st = K.Txt(br, "Sub", subs[i], 19, Pal.A(Pal.Ink, 0.6f), TextAnchor.LowerCenter); st.rectTransform.Fill(0, 6, 0, 0); st.fontStyle = FontStyle.Bold; }
                if (!enabled[i]) b.interactable = false;
                imgs.Add(b.GetComponent<Image>()); txts.Add(t);
            }
            Refresh();
            AgniSettings.Changed += () => { if (r != null) Refresh(); };
        }

        static void SliderRow(RectTransform card, string label, float y, float min, float max, Func<float> get, Action<float> set, Func<float, string> fmt)
        {
            var r = Row(card, label, y);
            var sr = K.R("Slider", r).Place(new Vector2(0, 0.5f), new Vector2(440, 0), new Vector2(860, 60), new Vector2(0, 0.5f));
            var track = K.Img(sr, "Track", "rr_16", Pal.Hex("#E7D2B4")); track.rectTransform.Fill(0, 18, 0, 18);
            var fillArea = K.R("FillArea", sr); fillArea.Fill(0, 18, 0, 18);
            var fill = K.Img(fillArea, "Fill", "btno_16", Color.white); fill.rectTransform.Fill();
            var handleArea = K.R("HandleArea", sr); handleArea.Fill(28, 0, 28, 0);
            var handle = K.Img(handleArea, "Handle", "circle", Color.white); handle.rectTransform.sizeDelta = new Vector2(60, 0); handle.raycastTarget = true;
            var hring = K.Img(handle.transform, "Ring", "ring", Pal.SaffronDeep); hring.rectTransform.Fill(-2, -2, -2, -2);
            handle.Shadow(Pal.A(Color.black, 0.3f), 0, -4);
            var s = sr.gameObject.AddComponent<Slider>(); s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle; s.direction = Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.SetValueWithoutNotify(get());
            var hit = sr.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); // whole bar draggable
            var val = K.Txt(r, "Value", fmt(get()), 38, Pal.SaffronDeep, TextAnchor.MiddleRight, true); val.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(160, 60), new Vector2(1, 0.5f));
            s.onValueChanged.AddListener(v => { set(v); val.text = fmt(v); });
            AgniSettings.Changed += () => { if (s != null && Mathf.Abs(s.value - get()) > 0.001f) { s.SetValueWithoutNotify(get()); val.text = fmt(get()); } };
        }
    }
}
