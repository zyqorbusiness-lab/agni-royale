// Agni Royale — LOBBY (2019-style layout, 100% our own art and names).
// Centre: 3D character (Kenney CC0 mini character) on a small platform + Agni mascot pet.
// Top-left: player card (name, level, Kaansa rank). Top-right: coins + Agni Ratna + settings.
// Left column: Store / Bag / Character / Pet / Events ("Jaldi aa raha hai").
// Bottom-left: squad slots. Bottom-right: mode selector + big START.  Built from code at runtime.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Agni.UI
{
    public class AgniLobby : MonoBehaviour
    {
        public string matchScene = "AgniMap";
        [System.NonSerialized] public string characterResource = "AgniChars/Meera"; // our anime Meera (characters_anime); Kenney CC0 mini character is the fallback
        [System.NonSerialized] public string fallbackCharacter = "AgniChar/character-male-f";
        [Tooltip("Lobby music hook — CC0 dhol/shehnai track goes here later (or Resources/AgniAudio/lobby_theme).")]
        public AudioClip lobbyMusic;
        public AudioSource music;

        Camera cam, uiCam; Canvas hud, back; RectTransform safe;
        Transform stage, character, pet; AnimationClip idleClip; SpriteRenderer petSr; Sprite petIdle, petBlink;
        GameObject comingSoon, settings, modePanel; Text comingTitle, modeLabel;
        CanvasGroup fade; bool starting;
        public GameObject SettingsPanel => settings;

        void Awake() { BuildWorld(); BuildHud(); SetupMusic(); }

        // ---------------- 3D stage
        void BuildWorld()
        {
            var camGo = new GameObject("LobbyCamera"); camGo.tag = "MainCamera"; cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Pal.TealDeep; cam.fieldOfView = 40; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1600;
            camGo.transform.position = new Vector3(0, 1.2f, -4.7f); camGo.transform.rotation = Quaternion.Euler(1.5f, 0, 0);
            camGo.AddComponent<AudioListener>();
            var urpData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urpData.renderPostProcessing = false;
            // HUD gets its own overlay camera in the URP stack, so the world's bloom never washes over the UI
            var uiGo = new GameObject("UICamera"); uiGo.transform.SetParent(camGo.transform, false); uiCam = uiGo.AddComponent<Camera>();
            uiCam.clearFlags = CameraClearFlags.Depth; uiCam.cullingMask = 1 << 5; uiCam.fieldOfView = cam.fieldOfView; uiCam.nearClipPlane = 0.1f; uiCam.farClipPlane = 10f;
            var uiData = uiGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); uiData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay; uiData.renderPostProcessing = false;
            urpData.cameraStack.Add(uiCam); cam.cullingMask &= ~(1 << 5);

            var sunGo = new GameObject("KeyLight"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = Pal.Hex("#FFF1DE"); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(30, -25, 0);
            var rimGo = new GameObject("RimLight"); var rim = rimGo.AddComponent<Light>(); rim.type = LightType.Directional; rim.color = Pal.Hex("#FF9A40"); rim.intensity = 0.9f; rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(18, 160, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Pal.Hex("#7FA6AA"); RenderSettings.ambientEquatorColor = Pal.Hex("#A87A62"); RenderSettings.ambientGroundColor = Pal.Hex("#3A2420");
            RenderSettings.fog = false;

            // MAHAL backdrop (3D palace to the horizon, animated) — see AgniMahal.cs
            var mahal = new GameObject("Mahal").AddComponent<AgniMahal>(); mahal.Build(cam, sun);
            mahal.transform.rotation = Quaternion.Euler(0, 12f, 0); // palace axis swings right: the endless vista opens beside the hero, not behind his head

            stage = new GameObject("Stage").transform; stage.position = new Vector3(0.0f, 0, 0);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(Color c, float smooth = 0.35f, float metal = 0f, Color? emit = null)
            {
                var m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
                if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                return m;
            }
            GameObject Cyl(string n, float r, float h, float y, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = n; Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(stage, false); g.transform.localScale = new Vector3(r * 2, h * 0.5f, r * 2); g.transform.localPosition = new Vector3(0, y, 0);
                g.GetComponent<MeshRenderer>().sharedMaterial = m; return g;
            }
            // platform: teal stone base, gold trim, saffron glowing ring, cream top
            Cyl("Base", 1.25f, 0.22f, -0.13f, M(Pal.Hex("#123F47"), 0.5f));
            Cyl("Trim", 1.29f, 0.05f, -0.02f, M(Pal.Hex("#E3A63A"), 0.7f, 0.85f));
            Cyl("GlowRing", 1.21f, 0.035f, 0.005f, M(Pal.Hex("#FF8A1F"), 0.2f, 0f, Pal.Hex("#FF7A1A") * 2.2f));
            Cyl("Top", 1.13f, 0.04f, 0.01f, M(Pal.Hex("#F2E3C8"), 0.25f));
            // floor glow (unlit sprite lying flat)
            var glowGo = new GameObject("FloorGlow"); glowGo.transform.SetParent(stage, false); glowGo.transform.localPosition = new Vector3(0, -0.23f, 0); glowGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var gsr = glowGo.AddComponent<SpriteRenderer>(); gsr.sprite = K.S("glow"); gsr.color = Pal.A(Pal.Hex("#FF9A3A"), 0.55f); glowGo.transform.localScale = Vector3.one * 2.4f;

            SpawnCharacter();
            // contact shadow
            var shGo = new GameObject("ContactShadow"); shGo.transform.SetParent(stage, false); shGo.transform.localPosition = new Vector3(-0.1f, 0.035f, 0); shGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var ssr = shGo.AddComponent<SpriteRenderer>(); ssr.sprite = K.S("glow"); ssr.color = new Color(0.1f, 0.03f, 0, 0.55f); shGo.transform.localScale = new Vector3(0.9f, 0.6f, 1);

            // pet: the Agni mascot (sprite billboard) beside the hero
            petIdle = K.S("mascot_idle"); petBlink = K.S("mascot_blink");
            var petGo = new GameObject("Pet_AgniGolu"); petGo.transform.SetParent(stage, false); pet = petGo.transform;
            petSr = petGo.AddComponent<SpriteRenderer>(); petSr.sprite = petIdle; petSr.sortingOrder = 5;
            float ph = petIdle != null ? petIdle.bounds.size.y : 1; pet.localScale = Vector3.one * (0.62f / ph);
            pet.localPosition = new Vector3(0.78f, 0.36f, -0.35f);
            var pshGo = new GameObject("PetShadow"); pshGo.transform.SetParent(stage, false); pshGo.transform.localPosition = new Vector3(0.78f, 0.04f, -0.35f); pshGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var psr = pshGo.AddComponent<SpriteRenderer>(); psr.sprite = K.S("glow"); psr.color = new Color(0.1f, 0.03f, 0, 0.45f); pshGo.transform.localScale = new Vector3(0.45f, 0.25f, 1);
        }

        static float Height(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return 0; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b.size.y;
        }

        void Update()
        {
            float t = Time.time;
            if (character != null && idleClip != null) idleClip.SampleAnimation(character.gameObject, t % Mathf.Max(0.1f, idleClip.length));
            if (pet != null)
            {
                float hop = Mathf.Abs(Mathf.Sin(t * 2.6f));
                pet.localPosition = new Vector3(0.78f, 0.36f + hop * 0.07f, -0.35f);
                float sq = 1 + (1 - hop) * 0.05f; float bs = pet.localScale.y > 0 ? 1 : 1;
                float baseS = 0.62f / (petIdle != null ? petIdle.bounds.size.y : 1);
                pet.localScale = new Vector3(baseS * sq, baseS / sq, baseS);
                bool blink = (t % 3.7f) < 0.12f; petSr.sprite = blink && petBlink != null ? petBlink : petIdle;
            }
            if (music != null) music.volume = AgniSettings.Music;
        }

        void SetupMusic()
        {
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.spatialBlend = 0;
            if (lobbyMusic == null) lobbyMusic = Resources.Load<AudioClip>("AgniAudio/lobby_theme");
            music.clip = lobbyMusic; music.volume = AgniSettings.Music;
            if (music.clip != null) music.Play();
        }

        // ---------------- HUD
        /// the lobby hero: the hair-v3 hero picked on this phone (AgniHeroes.Pref), else the old anime Meera, else the Kenney mini character
        void SpawnCharacter()
        {
            if (character != null) { Destroy(character.gameObject); character = null; idleClip = null; }
            var heroP = Agni.Net.AgniHeroes.Prefab(Agni.Net.AgniHeroes.Pref); bool v3 = heroP != null;
            var prefab = v3 ? heroP : Resources.Load<GameObject>(characterResource); bool anime = prefab != null;
            if (prefab == null) prefab = Resources.Load<GameObject>(fallbackCharacter);
            if (prefab != null)
            {
                var ch = Instantiate(prefab, stage); ch.name = "Character"; character = ch.transform;
                ch.transform.localPosition = new Vector3(-0.15f, 0.03f, 0); ch.transform.localRotation = Quaternion.Euler(0, 188, 0);
                if (anime && !v3) AgniCharacterLook.Setup(ch, AgniMahal.Quality);
                float h = Height(ch); float want = anime ? 1.62f : 1.32f; if (h > 0.01f) ch.transform.localScale = Vector3.one * (want / h);
                if (v3) ch.transform.localScale = Vector3.one * 0.86f; // one scale for all heroes (Meera 1.89 m in the FBX -> 1.62 m on the stage): the men stand taller
                var fc = ch.GetComponent<AgniFace>(); if (fc != null) { fc.lookTarget = cam.transform; fc.tier = AgniMahal.Quality <= 0 ? AgniFace.Tier.Low : AgniFace.Tier.High; fc.SetExpression("Soft_Smile", 0.4f); }
                var lgp = ch.GetComponent<LODGroup>(); if (lgp != null) lgp.ForceLOD(0); // the lobby hero is always the full face
                if (anime) { ch.transform.localPosition = new Vector3(-0.05f, 0.03f, 0); ch.transform.localRotation = Quaternion.Euler(0, 172, 0); ch.AddComponent<AgniIdle>(); }
                var an = ch.GetComponent<Animator>(); if (an != null) an.enabled = false;
                if (!anime) foreach (var c in Resources.LoadAll<AnimationClip>(fallbackCharacter)) if (c.name.ToLower() == "idle") { idleClip = c; break; }
                foreach (var r in ch.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            else
            {   // fallback capsule hero
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(cap.GetComponent<Collider>()); cap.name = "Character";
                cap.transform.SetParent(stage, false); cap.transform.localPosition = new Vector3(0, 0.9f, 0); cap.transform.localScale = new Vector3(0.7f, 0.85f, 0.7f);
                var cm = new Material(Shader.Find("Universal Render Pipeline/Lit")); cm.SetColor("_BaseColor", Pal.Saffron); cap.GetComponent<MeshRenderer>().sharedMaterial = cm; character = cap.transform;
            }
        }
        /// hero picker under the stage: < NAME >
        Text heroName;
        void HeroPicker(RectTransform s)
        {
            var bar = K.R("HeroPicker", s).Place(new Vector2(0.5f, 0), new Vector2(-640, 150), new Vector2(440, 88), new Vector2(0.5f, 0));
            var bg = K.Img(bar, "Bg", "rr_40", Pal.A(Pal.Navy, 0.72f)); bg.rectTransform.Fill();
            heroName = K.Txt(bar, "Name", Agni.Net.AgniHeroes.Display[Agni.Net.AgniHeroes.Pref].ToUpper(), 44, Color.white, TextAnchor.MiddleCenter, true); heroName.rectTransform.Fill(96, 0, 96, 0); heroName.Shadow(Pal.A(Color.black, 0.5f), 0, -3);
            var prev = K.Btn(bar, "Prev", "<", Pal.Saffron, 52, () => StepHero(-1), "btn_40"); ((RectTransform)prev.transform).Place(new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(84, 84), new Vector2(0, 0.5f));
            var next = K.Btn(bar, "Next", ">", Pal.Saffron, 52, () => StepHero(1), "btn_40"); ((RectTransform)next.transform).Place(new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(84, 84), new Vector2(1, 0.5f));
        }
        public void StepHero(int d)
        {
            int n = Agni.Net.AgniHeroes.Names.Length; Agni.Net.AgniHeroes.Pref = ((Agni.Net.AgniHeroes.Pref + d) % n + n) % n;
            SpawnCharacter(); if (heroName != null) heroName.text = Agni.Net.AgniHeroes.Display[Agni.Net.AgniHeroes.Pref].ToUpper();
            var fc = character != null ? character.GetComponent<AgniFace>() : null; if (fc != null) fc.SetExpression("Happy", 0.25f, 1.6f);
        }
        void BuildHud()
        {
            // (the old flat painted backdrop canvas is gone: the 3D Mahal is the background now)

            hud = CanvasMaker.Make("LobbyHUD", uiCam, 0.5f, 10, out safe);
            CanvasMaker.EnsureEventSystem();
            var s = safe;
            // soft shades so HUD reads over the art
            var topShade = K.Img(hud.transform, "TopShade", "vgrad", Pal.A(Pal.Navy, 0.6f)); var tr = topShade.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 0.5f); tr.sizeDelta = new Vector2(0, 220); tr.anchoredPosition = new Vector2(0, -110); tr.localRotation = Quaternion.Euler(0, 0, 180);
            var botShade = K.Img(hud.transform, "BottomShade", "vgrad", Pal.A(Pal.Navy, 0.6f)); var br = botShade.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0.5f, 0); br.sizeDelta = new Vector2(0, 260);
            var leftShade = K.Img(hud.transform, "LeftShade", "hgrad", Pal.A(Pal.Navy, 0.4f)); var lr = leftShade.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1); lr.pivot = new Vector2(0, 0.5f); lr.sizeDelta = new Vector2(420, 0);

            leftShade.transform.SetAsFirstSibling(); botShade.transform.SetAsFirstSibling(); topShade.transform.SetAsFirstSibling();
            PlayerCard(s);
            Currencies(s);
            LeftColumn(s);
            Squad(s);
            StartArea(s);
            SeasonTag(s);
            HeroPicker(s);

            var f = K.Img(hud.transform, "Fade", null, Color.black); f.rectTransform.Fill(); fade = f.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 1; fade.blocksRaycasts = false;
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn() { if (AgniBoot.ProofMode) { fade.alpha = 0; yield break; } while (fade.alpha > 0) { fade.alpha = Mathf.MoveTowards(fade.alpha, 0, Time.unscaledDeltaTime * 2.5f); yield return null; } }

        void PlayerCard(RectTransform s)
        {
            var card = K.R("PlayerCard", s).Place(new Vector2(0, 1), new Vector2(40, -30), new Vector2(640, 150), new Vector2(0, 1));
            var bg = K.Img(card, "Bg", "rr_40", Pal.A(Pal.Navy, 0.82f)); bg.rectTransform.Fill();
            var ol = K.Img(card, "Ol", "ol_40", Pal.A(Pal.Gold, 0.55f)); ol.rectTransform.Fill();
            // avatar
            var av = K.R("Avatar", card).Place(new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(122, 122), new Vector2(0, 0.5f));
            var avBg = K.Img(av, "Bg", "circle", Pal.Hex("#FFD08A")); avBg.rectTransform.Fill();
            var face = K.Img(av, "Face", "mascot_idle", Color.white); face.preserveAspect = true; face.rectTransform.Fill(14, 4, 14, 8);
            var ring = K.Img(av, "Ring", "ring", Pal.Saffron); ring.rectTransform.Fill(-6, -6, -6, -6);
            // name
            string nm = AgniProfile.HasName ? AgniProfile.Name : "Agni Player";
            var name = K.Txt(card, "Name", nm, 46, Color.white, TextAnchor.UpperLeft, true); name.rectTransform.Place(new Vector2(0, 1), new Vector2(162, -14), new Vector2(330, 60), new Vector2(0, 1)); name.Shadow(Pal.A(Color.black, 0.5f), 0, -3);
            // level pill + xp bar
            var lv = K.Img(card, "LevelPill", "btno_16", Color.white); lv.rectTransform.Place(new Vector2(0, 0), new Vector2(162, 24), new Vector2(118, 48), new Vector2(0, 0));
            var lvT = K.Txt(lv.transform, "T", "LV. " + AgniProfile.Level, 32, Color.white, TextAnchor.MiddleCenter, true); lvT.rectTransform.Fill(); lvT.Shadow(Pal.A(Pal.Ink, 0.5f), 0, -2);
            var xpBg = K.Img(card, "XpBg", "rr_16", Pal.A(Color.black, 0.5f)); xpBg.rectTransform.Place(new Vector2(0, 0), new Vector2(292, 48), new Vector2(200, 30), new Vector2(0, 0.5f));
            var xp = K.Img(xpBg.transform, "Xp", "rr_16", Pal.Gold); xp.rectTransform.anchorMin = Vector2.zero; xp.rectTransform.anchorMax = new Vector2(Mathf.Max(0.12f, AgniProfile.XpFrac), 1); xp.rectTransform.offsetMin = new Vector2(2, 2); xp.rectTransform.offsetMax = new Vector2(-2, -2);
            var xpT = K.Txt(xpBg.transform, "XpT", "XP " + Mathf.RoundToInt(AgniProfile.XpFrac * 100) + "%", 22, Color.white, TextAnchor.MiddleCenter, true); xpT.rectTransform.Fill(); xpT.Outline(Pal.A(Pal.Ink, 0.8f), 2);
            // rank badge
            var badge = K.Icon(card, "rank_kaansa", new Vector2(92, 101), Color.white); badge.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-34, 8), new Vector2(92, 101), new Vector2(1, 0.5f));
            var rk = K.Txt(card, "Rank", AgniProfile.RankName + " " + AgniProfile.RankTier, 26, Pal.Hex("#FFD7A6"), TextAnchor.MiddleCenter, true); rk.rectTransform.Place(new Vector2(1, 0), new Vector2(-80, 14), new Vector2(150, 30), new Vector2(0.5f, 0.5f)); rk.Shadow(Pal.A(Color.black, 0.7f), 0, -2);
        }

        void Currencies(RectTransform s)
        {
            var gear = K.Btn(s, "Settings", null, Pal.NavyLight, 10, OpenSettings, "btn_40");
            ((RectTransform)gear.transform).Place(new Vector2(1, 1), new Vector2(-40, -36), new Vector2(104, 104), new Vector2(1, 1));
            var gi = K.Icon(gear.transform, "ic_gear", new Vector2(62, 62), Color.white); gi.gameObject.AddComponent<Spin>();
            Pill(s, "Gems", "cur_gem", AgniProfile.Gems.ToString("N0"), new Vector2(-170, -48), "AGNI RATNA");
            Pill(s, "Coins", "cur_coin", AgniProfile.Coins.ToString("N0"), new Vector2(-520, -48), "SIKKE");
        }

        void Pill(RectTransform s, string n, string icon, string value, Vector2 pos, string caption)
        {
            var p = K.R(n, s).Place(new Vector2(1, 1), pos, new Vector2(320, 80), new Vector2(1, 1));
            var bg = K.Img(p, "Bg", "rr_40", Pal.A(Pal.Navy, 0.85f)); bg.rectTransform.Fill();
            var ol = K.Img(p, "Ol", "ol_40", Pal.A(Pal.Gold, 0.5f)); ol.rectTransform.Fill();
            var ic = K.Icon(p, icon, new Vector2(96, 96), Color.white); ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(-30, 4), new Vector2(96, 96), new Vector2(0, 0.5f));
            var v = K.Txt(p, "Value", value, 44, Color.white, TextAnchor.MiddleLeft, true); v.rectTransform.Fill(80, 0, 80, 0); v.Shadow(Pal.A(Color.black, 0.5f), 0, -3);
            var cap = K.Txt(p, "Caption", caption, 26, Pal.Hex("#FFE2B0"), TextAnchor.UpperLeft, true); cap.rectTransform.Place(new Vector2(0, 0), new Vector2(84, -6), new Vector2(220, 30), new Vector2(0, 1)); cap.Shadow(Pal.A(Color.black, 0.8f), 0, -2);
            var plus = K.Btn(p, "Add", null, Pal.Saffron, 10, () => ComingSoon(caption), "btn_16");
            ((RectTransform)plus.transform).Place(new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(72, 72), new Vector2(1, 0.5f));
            K.Icon(plus.transform, "ic_plus", new Vector2(40, 40), Color.white);
            K.HitPad(plus.transform, 24); // 120 px touch target (≥ 48 dp on a 1080p phone)
        }

        void LeftColumn(RectTransform s)
        {
            string[,] items = { { "STORE", "ic_store", "Dukaan" }, { "BAG", "ic_bag", "Bag" }, { "CHARACTER", "ic_character", "Character" }, { "PET", "ic_pet", "Pet" }, { "EVENTS", "ic_events", "Events" } };
            for (int i = 0; i < items.GetLength(0); i++)
            {
                string label = items[i, 0], icon = items[i, 1];
                var b = K.Btn(s, "Btn_" + label, null, Pal.A(Pal.Navy, 0.9f), 10, () => ComingSoon(label), "btn_24");
                var rt = (RectTransform)b.transform; rt.Place(new Vector2(0, 1), new Vector2(46, -200 - i * 128), new Vector2(156, 116), new Vector2(0, 1));
                var ol = K.Img(rt, "Ol", "ol_24", Pal.A(Pal.Gold, 0.6f)); ol.rectTransform.Fill();
                var ic = K.Icon(rt, icon, new Vector2(60, 60), Pal.Hex("#FFD08A")); ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(60, 60), new Vector2(0.5f, 1));
                var t = K.Txt(rt, "Label", label, label.Length > 7 ? 23 : 27, Color.white, TextAnchor.LowerCenter, true); t.rectTransform.Fill(0, 8, 0, 0); t.Shadow(Pal.A(Color.black, 0.6f), 0, -2);
                if (label == "EVENTS") { var dot = K.Img(rt, "NewDot", "circle", Pal.Red); dot.rectTransform.Place(new Vector2(1, 1), new Vector2(-14, -14), new Vector2(26, 26), new Vector2(0.5f, 0.5f)); var dr = K.Img(dot.transform, "R", "ring", Color.white); dr.rectTransform.Fill(-2, -2, -2, -2); dot.gameObject.AddComponent<Pulse>(); }
            }
        }

        RectTransform squadRow; Text squadCap, startLabel, friendsBadge; Image friendsBadgeBg; string squadSig = "~"; bool myReady, arena = true;
        GameObject lobbyToast; Text lobbyToastT; float lobbyToastUntil; AgniFriendsPanel friendsPanel;
        static Agni.Social.IPartyService PartySvc => Agni.Social.AgniSocial.Party;

        void Squad(RectTransform s)
        {
            squadRow = K.R("Squad", s).Place(new Vector2(0, 0), new Vector2(46, 34), new Vector2(560, 150), new Vector2(0, 0));
            squadCap = K.Txt(squadRow, "Caption", "SQUAD  1/4", 28, Pal.Cream, TextAnchor.LowerLeft, true); squadCap.rectTransform.Place(new Vector2(0, 1), new Vector2(4, 8), new Vector2(560, 40), new Vector2(0, 0)); squadCap.Shadow(Pal.A(Color.black, 0.6f), 0, -2);
            // DOST button (friends + room code) right of the four slots
            var fb = K.Btn(s, "Btn_DOST", null, Pal.A(Pal.Navy, 0.9f), 10, () => OpenFriends(AgniFriendsPanel.Tab.Dost), "btn_24");
            var fr = (RectTransform)fb.transform; fr.Place(new Vector2(0, 0), new Vector2(46 + 4 * 134 + 6, 50), new Vector2(150, 118), new Vector2(0, 0));
            K.Img(fr, "Ol", "ol_24", Pal.A(Pal.Gold, 0.6f)).rectTransform.Fill();
            var fic = K.Icon(fr, "ic_character", new Vector2(56, 56), Pal.Hex("#FFD08A")); fic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(56, 56), new Vector2(0.5f, 1));
            var ft = K.Txt(fr, "Label", "DOST", 27, Color.white, TextAnchor.LowerCenter, true); ft.rectTransform.Fill(0, 8, 0, 0); ft.Shadow(Pal.A(Color.black, 0.6f), 0, -2);
            friendsBadgeBg = K.Img(fr, "Online", "circle", Pal.Hex("#2FBF71")); friendsBadgeBg.rectTransform.Place(new Vector2(1, 1), new Vector2(-10, -10), new Vector2(40, 40), new Vector2(0.5f, 0.5f));
            friendsBadge = K.Txt(friendsBadgeBg.transform, "N", "", 24, Color.white, TextAnchor.MiddleCenter, true); friendsBadge.rectTransform.Fill();
            K.HitPad(fb.transform, 10);
            RefreshSquad();
        }

        string SquadSignature()
        {
            var p = PartySvc; var sig = p.InParty + "|" + p.Code + "|" + p.IsLeader + "|" + myReady + "|";
            foreach (var m in p.Members) sig += m.name + m.ready + m.leader + ",";
            int on = 0; foreach (var f in Agni.Social.AgniSocial.Friends.Friends) if (f.presence != Agni.Social.Presence.Offline) on++;
            return sig + on;
        }

        void RefreshSquad()
        {
            if (squadRow == null) return;
            var sig = SquadSignature(); if (sig == squadSig) return; squadSig = sig;
            for (int i = squadRow.childCount - 1; i >= 0; i--) { var c = squadRow.GetChild(i); if (c.name.StartsWith("Slot")) Destroy(c.gameObject); }
            var p = PartySvc; var others = new List<Agni.Social.PartyMember>();
            foreach (var m in p.Members) if (!m.isLocal) others.Add(m);
            bool meLeader = !p.InParty || p.IsLeader;
            for (int i = 0; i < 4; i++)
            {
                var slot = K.R("Slot" + i, squadRow).Place(new Vector2(0, 0.5f), new Vector2(i * 134, 0), new Vector2(118, 118), new Vector2(0, 0.5f));
                if (i == 0)
                {
                    var c = K.Img(slot, "Bg", "circle", Pal.Hex("#FFD08A")); c.rectTransform.Fill();
                    var f = K.Img(slot, "Face", "mascot_idle", Color.white); f.preserveAspect = true; f.rectTransform.Fill(14, 4, 14, 8);
                    var r = K.Img(slot, "Ring", "ring", Pal.Gold); r.rectTransform.Fill(-5, -5, -5, -5);
                    var crown = K.Img(slot, "Leader", "btno_16", Color.white); crown.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(90, 32), new Vector2(0.5f, 0.5f));
                    var ct = K.Txt(crown.transform, "T", meLeader ? "TUM ★" : "TUM", 22, Color.white, TextAnchor.MiddleCenter, true); ct.rectTransform.Fill();
                    if (p.InParty && !p.IsLeader && myReady) Tick(slot);
                }
                else if (i - 1 < others.Count)
                {
                    var m = others[i - 1];
                    var c = K.Img(slot, "Bg", "circle", m.leader ? Pal.Gold : Pal.Hex("#F2E3C8")); c.rectTransform.Fill();
                    var ini = K.Txt(slot, "I", string.IsNullOrEmpty(m.name) ? "?" : m.name.Substring(0, 1).ToUpperInvariant(), 58, Pal.Ink, TextAnchor.MiddleCenter, true); ini.rectTransform.Fill(0, 8, 0, 0);
                    var r = K.Img(slot, "Ring", "ring", m.ready || m.leader ? Pal.Hex("#2FBF71") : Pal.A(Pal.Cream, 0.6f)); r.rectTransform.Fill(-5, -5, -5, -5);
                    var tagBg = K.Img(slot, "NameBg", "rr_16", Pal.A(Pal.Navy, 0.9f)); tagBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(128, 32), new Vector2(0.5f, 0.5f));
                    var nm = K.Txt(tagBg.transform, "T", (m.leader ? "★ " : "") + (m.name.Length > 8 ? m.name.Substring(0, 8) : m.name), 20, Color.white, TextAnchor.MiddleCenter, true); nm.rectTransform.Fill();
                    if (m.ready || m.leader) Tick(slot);
                }
                else
                {
                    var b = K.Btn(slot, "Invite", null, Pal.A(Pal.Navy, 0.85f), 10, () => OpenFriends(AgniFriendsPanel.Tab.Dost), "btn_40");
                    ((RectTransform)b.transform).Fill();
                    var dash = K.Img(b.transform, "Ring", "ring", Pal.A(Pal.Cream, 0.4f)); dash.rectTransform.Fill(2, 2, 2, 2);
                    K.Icon(b.transform, "ic_plus", new Vector2(46, 46), Pal.A(Pal.Cream, 0.85f));
                    b.GetComponent<Image>().sprite = K.S("circle"); b.GetComponent<Image>().type = Image.Type.Simple;
                    var sh = b.transform.Find("Shadow"); if (sh != null) sh.gameObject.SetActive(false);
                }
            }
            int n = p.InParty ? Mathf.Max(1, p.Members.Count) : 1;
            squadCap.text = "SQUAD  " + n + "/4" + (p.InParty && !string.IsNullOrEmpty(p.Code) ? "   ·   ROOM " + p.Code : "");
            int on = 0; foreach (var f in Agni.Social.AgniSocial.Friends.Friends) if (f.presence != Agni.Social.Presence.Offline) on++;
            if (friendsBadge != null) { friendsBadge.text = on.ToString(); friendsBadgeBg.gameObject.SetActive(on > 0); }
            if (startLabel != null)
            {
                if (p.InParty && !p.IsLeader) startLabel.text = myReady ? "READY ✓" : "READY";
                else startLabel.text = "START";
            }
        }
        static void Tick(RectTransform slot)
        {
            var tk = K.Img(slot, "Ready", "circle", Pal.Hex("#2FBF71")); tk.rectTransform.Place(new Vector2(1, 1), new Vector2(4, 4), new Vector2(38, 38), new Vector2(1, 1));
            K.Img(tk.transform, "R", "ring", Color.white).rectTransform.Fill(-2, -2, -2, -2);
            K.Txt(tk.transform, "v", "✓", 26, Color.white, TextAnchor.MiddleCenter, true).rectTransform.Fill();
        }

        GameObject invitePopup;
        void OnEnable() { Agni.Social.AgniSocial.InviteReceived += OnInvite; }
        void OnDisable() { Agni.Social.AgniSocial.InviteReceived -= OnInvite; }
        /// a friend's squad invite (online: UGS Friends message): JUDO joins their room by code
        public void OnInvite(string from, string code)
        {
            if (invitePopup != null) Destroy(invitePopup);
            var card = K.Modal(hud.transform, "SquadInvite", new Vector2(1100, 520), out invitePopup);
            var t = K.Txt(card, "Title", "SQUAD INVITE", 58, Pal.SaffronDeep, TextAnchor.MiddleLeft, true); t.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -75), new Vector2(900, 100), new Vector2(0, 0.5f));
            var b = K.Txt(card, "Body", from + " ne tumhe apni squad me bulaya!\nRoom code: " + code, 38, Pal.Ink, TextAnchor.UpperLeft, true); b.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -190), new Vector2(980, 130), new Vector2(0, 1));
            var go = invitePopup;
            var yes = K.Btn(card, "Join", "JUDO", Pal.Saffron, 46, () => { Destroy(go); PartySvc.JoinByCode(code); OpenFriends(AgniFriendsPanel.Tab.Room).Toast("Room " + code + " se jud rahe hain…"); }, "btn_24");
            ((RectTransform)yes.transform).Place(new Vector2(0, 0), new Vector2(60, 40), new Vector2(400, 100), new Vector2(0, 0));
            var no = K.Btn(card, "Later", "BAAD ME", Pal.Hex("#C9B8A4"), 40, () => Destroy(go), "btn_24");
            ((RectTransform)no.transform).Place(new Vector2(0, 0), new Vector2(490, 40), new Vector2(320, 100), new Vector2(0, 0));
            K.CloseX(card, () => Destroy(go));
            invitePopup.AddComponent<PopIn>(); invitePopup.transform.SetSiblingIndex(hud.transform.childCount - 2);
            AgniSfx.Play("sfx_reward_coin");
        }

        public AgniFriendsPanel OpenFriends(AgniFriendsPanel.Tab tab)
        {
            if (friendsPanel != null) Destroy(friendsPanel.gameObject);
            friendsPanel = AgniFriendsPanel.Open(hud.transform, tab);
            friendsPanel.transform.SetSiblingIndex(hud.transform.childCount - 2); // under the fade
            friendsPanel.gameObject.AddComponent<PopIn>();
            return friendsPanel;
        }

        void LobbyToast(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (lobbyToast == null)
            {
                var bg = K.Img(hud.transform, "LobbyToast", "rr_24", Pal.A(Pal.Ink, 0.9f)); bg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(980, 84), new Vector2(0.5f, 0));
                lobbyToastT = K.Txt(bg.transform, "T", "", 32, Color.white, TextAnchor.MiddleCenter, true); lobbyToastT.rectTransform.Fill(20, 0, 20, 0); lobbyToast = bg.gameObject;
            }
            lobbyToastT.text = msg; lobbyToast.SetActive(true); lobbyToast.transform.SetSiblingIndex(hud.transform.childCount - 2); lobbyToastUntil = Time.unscaledTime + 2.6f;
        }

        float squadPoll;
        void LateUpdate()
        {
            if (Time.unscaledTime > squadPoll) { squadPoll = Time.unscaledTime + 0.4f; RefreshSquad(); }
            if (lobbyToast != null && lobbyToast.activeSelf && Time.unscaledTime > lobbyToastUntil) lobbyToast.SetActive(false);
            var code = Agni.Social.AgniDeepLink.PendingCode;
            if (!string.IsNullOrEmpty(code) && !starting)
            {   // agniroyale://join?code=XXXXXX opened the game: jump straight into that squad
                Agni.Social.AgniDeepLink.PendingCode = null; PartySvc.JoinByCode(code); OpenFriends(AgniFriendsPanel.Tab.Room).Toast(PartySvc.Status);
            }
        }

        void StartArea(RectTransform s)
        {
            // mode selector
            var mode = K.Btn(s, "ModeSelector", null, Pal.A(Pal.Navy, 0.9f), 10, OpenModes, "btn_24");
            var mr = (RectTransform)mode.transform; mr.Place(new Vector2(1, 0), new Vector2(-46, 218), new Vector2(560, 100), new Vector2(1, 0));
            var mol = K.Img(mr, "Ol", "ol_24", Pal.A(Pal.Gold, 0.7f)); mol.rectTransform.Fill();
            var thumb = K.Img(mr, "MapThumb", "rr_16", Color.white); thumb.sprite = K.S("poster_s1"); thumb.type = Image.Type.Simple; thumb.preserveAspect = false;
            thumb.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(150, 76), new Vector2(0, 0.5f));
            var mk = K.Txt(mr, "Kind", "MODE", 26, Pal.A(Pal.Gold, 0.9f), TextAnchor.UpperLeft, true); mk.rectTransform.Place(new Vector2(0, 1), new Vector2(180, -10), new Vector2(330, 30), new Vector2(0, 1));
            modeLabel = K.Txt(mr, "Mode", "AKHADA BR · 50 KHILADI", 30, Color.white, TextAnchor.LowerLeft, true); modeLabel.rectTransform.Place(new Vector2(0, 0), new Vector2(180, 14), new Vector2(340, 50), new Vector2(0, 0)); modeLabel.Shadow(Pal.A(Color.black, 0.5f), 0, -2);
            var chev = K.Icon(mr, "ic_chevron", new Vector2(40, 40), Pal.Gold); chev.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(40, 40), new Vector2(1, 0.5f));

            // START button
            var go = K.R("StartWrap", s).Place(new Vector2(1, 0), new Vector2(-46, 40), new Vector2(560, 160), new Vector2(1, 0));
            var glow = K.Img(go, "Glow", "glow", Pal.A(Pal.Hex("#FFB040"), 0.55f)); glow.rectTransform.Fill(-140, -90, -140, -90); glow.gameObject.AddComponent<Pulse>();
            var start = K.Btn(go, "Start", "START", Pal.Saffron, 100, OnStart, "btn_40"); startLabel = start.transform.Find("Label").GetComponent<Text>(); squadSig = "~";
            var sr = (RectTransform)start.transform; sr.Fill();
            var stripes = K.Img(sr, "Stripes", "stripes", Pal.A(Color.white, 0.07f)); stripes.type = Image.Type.Tiled; stripes.rectTransform.Fill(12, 12, 12, 12); stripes.transform.SetSiblingIndex(2);
            var lbl = sr.Find("Label").GetComponent<Text>(); lbl.Outline(Pal.Hex("#8A2E06"), 4);
        }

        void SeasonTag(RectTransform s)
        {
            var tag = K.R("SeasonTag", s).Place(new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(480, 84), new Vector2(0.5f, 0));
            var bg = K.Img(tag, "Bg", "rr_40", Pal.A(Pal.Navy, 0.75f)); bg.rectTransform.Fill();
            var ol = K.Img(tag, "Ol", "ol_40", Pal.A(Pal.Gold, 0.5f)); ol.rectTransform.Fill();
            var t = K.Txt(tag, "Season", "SEASON 1 · BHARAT YATRA", 31, Color.white, TextAnchor.UpperCenter, true); t.rectTransform.Fill(0, 0, 0, 10);
            var g = t.gameObject.AddComponent<Agni.Intro.VerticalGradient>(); g.top = Pal.Hex("#FFE9A8"); g.bottom = Pal.Hex("#FF9A3A");
            var sub = K.Txt(tag, "Sub", "Kashmir se Kanyakumari tak", 23, Pal.A(Pal.Cream, 0.85f), TextAnchor.LowerCenter); sub.rectTransform.Fill(0, 10, 0, 0); sub.fontStyle = FontStyle.Bold;
        }

        // ---------------- panels
        public void ComingSoon(string what)
        {
            if (comingSoon == null)
            {
                var card = K.Modal(hud.transform, "ComingSoon", new Vector2(1020, 560), out comingSoon);
                comingTitle = K.Txt(card, "Title", "", 60, Pal.Ink, TextAnchor.MiddleLeft, true); comingTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -75), new Vector2(760, 100), new Vector2(0, 0.5f));
                var m = K.Icon(card, "mascot_wave", new Vector2(250, 350), Color.white); m.rectTransform.Place(new Vector2(1, 0), new Vector2(-60, 40), new Vector2(250, 350), new Vector2(1, 0)); m.gameObject.AddComponent<Bob>().amp = 7;
                var big = K.Txt(card, "Big", "Jaldi aa raha hai!", 64, Pal.SaffronDeep, TextAnchor.MiddleLeft, true); big.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -230), new Vector2(660, 90), new Vector2(0, 0.5f));
                var body = K.Txt(card, "Body", "Hum ise bana rahe hain. Agle update me\nmilega — tab tak match khel ke level badhao.", 31, Pal.Hex("#6B4A3A"), TextAnchor.UpperLeft); body.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -282), new Vector2(660, 110), new Vector2(0, 1)); body.lineSpacing = 0.95f;
                var ok = K.Btn(card, "Ok", "THEEK HAI", Pal.Saffron, 46, () => comingSoon.SetActive(false), "btn_24");
                ((RectTransform)ok.transform).Place(new Vector2(0, 0), new Vector2(60, 40), new Vector2(380, 96), new Vector2(0, 0));
                K.CloseX(card, () => comingSoon.SetActive(false));
                comingSoon.AddComponent<PopIn>();
                comingSoon.transform.SetSiblingIndex(hud.transform.childCount - 2);
            }
            comingTitle.text = what;
            comingSoon.SetActive(true);
        }

        public void OpenModes()
        {
            if (modePanel != null) { Destroy(modePanel); modePanel = null; }
            if (modePanel == null)
            {
                var card = K.Modal(hud.transform, "ModeSelect", new Vector2(1300, 600), out modePanel);
                var title = K.Txt(card, "Title", "MODE CHUNO", 60, Pal.Ink, TextAnchor.MiddleLeft, true); title.rectTransform.Place(new Vector2(0, 1), new Vector2(60, -75), new Vector2(760, 100), new Vector2(0, 0.5f));
                ModeCard(card, new Vector2(60, -190), "AKHADA BR", "Agni Akhada · 50 khiladi · Squad", true, () => { arena = true; modeLabel.text = "AKHADA BR · 50 KHILADI"; modePanel.SetActive(false); });
                ModeCard(card, new Vector2(660, -190), "BHARAT MAP", "Map ghoomo · combat jaldi", true, () => { arena = false; modeLabel.text = "CLASSIC · BHARAT MAP"; modePanel.SetActive(false); });
                K.CloseX(card, () => modePanel.SetActive(false));
                modePanel.AddComponent<PopIn>();
                modePanel.transform.SetSiblingIndex(hud.transform.childCount - 2);
            }
            modePanel.SetActive(true);
        }

        void ModeCard(RectTransform card, Vector2 pos, string title, string sub, bool enabled, System.Action pick)
        {
            bool selected = enabled && ((title == "AKHADA BR") == arena);
            var b = K.Btn(card, "Mode_" + title, null, enabled ? Color.white : Pal.Hex("#CFC6BC"), 10, pick, "btn_24");
            var rt = (RectTransform)b.transform; rt.Place(new Vector2(0, 1), pos, new Vector2(580, 340), new Vector2(0, 1)); b.interactable = enabled;
            var img = K.Img(rt, "Art", null, Color.white); img.sprite = K.S(enabled ? "poster_s1" : "lobby_bg"); img.rectTransform.Fill(14, 110, 14, 14); img.color = enabled ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            var t = K.Txt(rt, "Title", title, 44, enabled ? Pal.Ink : Pal.Hex("#7D7168"), TextAnchor.UpperLeft, true); t.rectTransform.Place(new Vector2(0, 0), new Vector2(24, 100), new Vector2(540, 50), new Vector2(0, 1));
            var st = K.Txt(rt, "Sub", sub, 28, enabled ? Pal.Hex("#6B4A3A") : Pal.Hex("#8F837A"), TextAnchor.UpperLeft); st.rectTransform.Place(new Vector2(0, 0), new Vector2(24, 50), new Vector2(540, 40), new Vector2(0, 1)); st.fontStyle = FontStyle.Bold;
            if (selected) { var sel = K.Img(rt, "Selected", "ol_24", Pal.Saffron); sel.rectTransform.Fill(-6, -6, -6, -6); var chk = K.Img(rt, "Tick", "btno_16", Color.white); chk.rectTransform.Place(new Vector2(1, 1), new Vector2(-24, -24), new Vector2(150, 46), new Vector2(1, 1)); var ct = K.Txt(chk.transform, "T", "CHUNA", 26, Color.white, TextAnchor.MiddleCenter, true); ct.rectTransform.Fill(); }
            else if (!enabled) { var lk = K.Icon(rt, "ic_lock", new Vector2(90, 90), Color.white); lk.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(90, 90), new Vector2(0.5f, 1)); }
        }

        public void OpenSettings()
        {
            if (settings == null) { settings = AgniSettingsPanel.Build(hud.transform); settings.transform.SetSiblingIndex(hud.transform.childCount - 2); }
            settings.SetActive(true);
        }

        public void OnStart()
        {
            if (starting) return;
            var p = PartySvc;
            if (p.InParty && !p.IsLeader)
            {   // squad member: START is the ready toggle; the leader takes everyone in
                myReady = !myReady; p.SetReady(myReady); AgniSfx.Play("sfx_confirm"); squadSig = "~"; RefreshSquad();
                LobbyToast(myReady ? "Ready! Leader START dabayega" : "Ready hata diya"); return;
            }
            if (p.InParty && p.IsLeader)
            {
                if (!p.AllReady) { LobbyToast("Squad me sab ready nahi hain"); return; }
                starting = true; AgniSfx.Play("sfx_confirm"); StartCoroutine(PartyStart()); return;
            }
            starting = true; AgniSfx.Play("sfx_confirm"); StartCoroutine(StartRoutine());
        }
        IEnumerator PartyStart()
        {
            while (fade.alpha < 1) { fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * 3); yield return null; }
            PartySvc.StartMatch(); // NGO loads the arena for the whole squad
        }
        IEnumerator StartRoutine()
        {
            var scene = arena ? Agni.Net.AgniNetConfig.MatchScene : matchScene; // solo arena: AgniArenaBootstrap hosts an offline match with bots
            AsyncOperation op = Application.CanStreamedLevelBeLoaded(scene) ? SceneManager.LoadSceneAsync(scene) : null;
            if (op != null) op.allowSceneActivation = false;
            while (fade.alpha < 1) { fade.alpha = Mathf.MoveTowards(fade.alpha, 1, Time.unscaledDeltaTime * 3); yield return null; }
            if (op != null) op.allowSceneActivation = true; else { Debug.LogWarning("[AgniLobby] match scene not in build: " + scene); starting = false; fade.alpha = 0; }
        }
    }

    public class Spin : MonoBehaviour { public float speed = -14; void Update() { transform.Rotate(0, 0, speed * Time.unscaledDeltaTime); } }
    public class Pulse : MonoBehaviour
    {
        Graphic g; float a0;
        void Start() { g = GetComponent<Graphic>(); a0 = g.color.a; }
        void Update() { float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f); var c = g.color; c.a = a0 * (0.55f + 0.45f * k); g.color = c; float s = 1 + 0.04f * k; transform.localScale = new Vector3(s, s, 1); }
    }
}
