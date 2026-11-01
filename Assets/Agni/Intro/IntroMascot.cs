// AGNI ROYALE — Intro mascot ("Agni" golu): hops in, winks, blows a kiss, runs off, title pops, then the next scene.
// 100% procedural: every shape (blob body, eyes, flame tuft, heart, sparkles, bokeh) is generated in code at runtime.
// No art files needed. Optional: Resources/AgniIntro/LilitaOne-Regular.ttf, Baloo2.ttf (OFL) and intro_sfx.wav (synthesized in code).
// Drop this on an empty GameObject in Intro.unity (or run Agni Royale > Intro > Create Intro Scene). Tap/click/any key skips.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Agni.Intro
{
    public class IntroMascot : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("Scene to load after the intro. Empty = the next scene in Build Settings (index of this scene + 1).")]
        public string nextSceneName = "";
        public float duration = 5.0f;
        public bool allowSkip = true;
        public bool playSound = true;

        // ---- design constants (reference frame 1280x720, pixels) ----
        const float G = 622f, R = 118f, L = 70f, S = 1.17f;
        static readonly Color BG = Hex("#F2E9E0"), ORANGE = Hex("#FF5A1F"), LIMB = Hex("#F04C14"), LIMB_DARK = Hex("#B8360C"),
            ARM = Hex("#FF6E33"), ARM_DARK = Hex("#A8300A"), INK = Hex("#3A1206");

        float t;                     // intro clock
        bool finished;
        AsyncOperation loadOp;
        Camera cam;
        Material mat;
        Transform world, body, flameRoot;
        Mesh blobMesh; Vector3[] blobVerts; Color[] blobCols; const int BLOB_N = 72;
        SpriteRenderer shadow, rim, sparkA, sparkB;
        SpriteRenderer[] blush = new SpriteRenderer[2];
        Transform[] eyeGroup = new Transform[2]; Transform[] pupil = new Transform[2];
        LineRenderer[] closedEye = new LineRenderer[2]; LineRenderer[] lash = new LineRenderer[2];
        LineRenderer smile; Transform pucker;
        LineRenderer[] leg = new LineRenderer[2], legO = new LineRenderer[2], arm = new LineRenderer[2], armO = new LineRenderer[2];
        Transform[] foot = new Transform[2], footO = new Transform[2], hand = new Transform[2], handO = new Transform[2];
        Transform[] flame = new Transform[3];
        SpriteRenderer heart; SpriteRenderer[] miniHeart = new SpriteRenderer[3];
        Vector2 heartOrigin; bool heartOriginSet;
        struct Bokeh { public SpriteRenderer sr; public float x, y, vx, vy, ph; }
        List<Bokeh> bokeh = new List<Bokeh>();
        // UI
        Text[] letters; float[] letterX; Text subtitle; Image lineL, lineR, fade;
        const string TITLE = "AGNI ROYALE";

        // ================= lifecycle =================
        void Start()
        {
            Application.targetFrameRate = 60;
            BuildCamera();
            BuildWorld();
            BuildUI();
            if (playSound)
            {
                var clip = Resources.Load<AudioClip>("AgniIntro/intro_sfx");
                if (clip != null) { var a = gameObject.AddComponent<AudioSource>(); a.clip = clip; a.playOnAwake = false; a.Play(); }
            }
            StartLoadingNext();
            Apply(0f);
        }

        void StartLoadingNext()
        {
            int idx = SceneManager.GetActiveScene().buildIndex + 1;
            if (!string.IsNullOrEmpty(nextSceneName)) loadOp = SceneManager.LoadSceneAsync(nextSceneName);
            else if (idx > 0 && idx < SceneManager.sceneCountInBuildSettings) loadOp = SceneManager.LoadSceneAsync(idx);
            if (loadOp != null) loadOp.allowSceneActivation = false;
            else Debug.LogWarning("[AgniIntro] No next scene (set nextSceneName or add a scene after Intro in Build Settings).");
        }

        void Update()
        {
            if (finished) return;
            if (allowSkip && SkipPressed() && t < 4.62f) t = 4.62f;   // jump straight to the fade-out
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (t >= duration)
            {
                Apply(duration);
                finished = true;
                if (loadOp != null) loadOp.allowSceneActivation = true;
                return;
            }
            Apply(t);
        }

        bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.anyKeyDown) return true;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
#endif
            return false;
        }

        // ================= helpers =================
        static Color Hex(string h, float a = 1f) { ColorUtility.TryParseHtmlString(h, out var c); c.a = a; return c; }
        static Color A(Color c, float a) { c.a = a; return c; }
        static Vector3 U(float px, float py) => new Vector3(px - 640f, 360f - py, 0f);       // reference px (y down) -> world
        static Vector2 Py(Vector3 u) => new Vector2(u.x + 640f, 360f - u.y);                  // world -> reference px
        static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        static float Lerp(float a, float b, float k) => a + (b - a) * k;
        static float Seg(float t, float a, float b) => Clamp01((t - a) / (b - a));
        static float Eio(float k) => k * k * (3 - 2 * k);
        static float Eout(float k) => 1 - (1 - k) * (1 - k) * (1 - k);
        static float Ein(float k) => k * k;
        static float Eback(float k, float s = 1.9f) { k -= 1; return 1 + (s + 1) * k * k * k + s * k * k; }
        static float Spring(float dt, float amp, float freq = 24, float damp = 11) => dt < 0 ? 0 : amp * Mathf.Exp(-damp * dt) * Mathf.Cos(freq * dt);
        static float Elastic(float k)
        {
            if (k <= 0) return 0; if (k >= 1) return 1;
            return Mathf.Pow(2, -10 * k) * Mathf.Sin((k * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
        }

        // ---- procedural textures ----
        delegate Color PixelFn(float x, float y); // x,y in [-1,1], y up
        static Sprite MakeSprite(int size, PixelFn fn, int ss = 3)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    Color acc = new Color(0, 0, 0, 0);
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float x = ((i + (sx + 0.5f) / ss) / size) * 2f - 1f, y = ((j + (sy + 0.5f) / ss) / size) * 2f - 1f;
                            Color c = fn(x, y); acc.r += c.r * c.a; acc.g += c.g * c.a; acc.b += c.b * c.a; acc.a += c.a;
                        }
                    float n = ss * ss;
                    px[j * size + i] = acc.a > 0 ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a / n) : new Color(1, 1, 1, 0);
                }
            tex.SetPixels(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);   // 1 world unit wide
        }
        static Sprite circleSpr, softSpr, eyeSpr, heartSpr, flameOutSpr, flameInSpr, starSpr;
        static void BuildSprites()
        {
            if (circleSpr != null) return;
            circleSpr = MakeSprite(128, (x, y) => new Color(1, 1, 1, x * x + y * y <= 0.97f ? 1 : 0));
            softSpr = MakeSprite(64, (x, y) => { float d = x * x + y * y; return new Color(1, 1, 1, Mathf.Exp(-d * 4.2f) * Clamp01((1 - d) * 4)); }, 1);
            eyeSpr = MakeSprite(128, (x, y) =>
            {
                float r = Mathf.Sqrt((x + 0.18f) * (x + 0.18f) + (y - 0.24f) * (y - 0.24f)) / 1.15f;
                Color c = r < 0.6f ? Color.Lerp(Color.white, Hex("#F4F1EE"), r / 0.6f) : Color.Lerp(Hex("#F4F1EE"), Hex("#C9C4C0"), Clamp01((r - 0.6f) / 0.4f));
                c.a = x * x + y * y <= 0.97f ? 1 : 0; return c;
            });
            heartSpr = MakeSprite(256, (x, y) =>
            {
                float hx = x * 1.3f, hy = y * 1.3f + 0.15f;
                float a = hx * hx + hy * hy - 1; bool inside = a * a * a - hx * hx * hy * hy * hy <= 0;
                Color c = Color.Lerp(Hex("#E8173F"), Hex("#FF5C7A"), Clamp01((y + 0.8f) / 1.5f));
                float hl = ((x + 0.42f) * (x + 0.42f)) / 0.02f + ((y - 0.38f) * (y - 0.38f)) / 0.008f;
                if (hl < 1) c = Color.Lerp(c, Color.white, 0.6f);
                c.a = inside ? 1 : 0; return c;
            }, 2);
            PixelFn flameShape(Color bottom, Color top) => (x, y) =>
            {
                float v = (y + 1) * 0.5f;      // 0 base .. 1 tip
                float hw = v < 0.3f ? Mathf.Sqrt(Mathf.Max(0, 0.09f - (v - 0.3f) * (v - 0.3f))) : 0.3f * Mathf.Pow(Mathf.Max(0, (1 - v) / 0.7f), 0.85f);
                Color c = Color.Lerp(bottom, top, v); c.a = Mathf.Abs(x) <= hw / 0.3f ? 1 : 0; return c;
            };
            flameOutSpr = MakeSprite(128, flameShape(Hex("#FF6A1A"), Hex("#FFB12E")));
            flameInSpr = MakeSprite(128, flameShape(Hex("#FFE27A"), Hex("#FFF2B0")));
            starSpr = MakeSprite(64, (x, y) => new Color(Hex("#FFC93C").r, Hex("#FFC93C").g, Hex("#FFC93C").b,
                Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) <= 1 ? 1 : 0));
        }

        SpriteRenderer Spr(string name, Transform parent, Sprite s, Color c, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            if (mat == null) mat = sr.sharedMaterial;     // the pipeline's default sprite material (vertex colours, unlit)
            return sr;
        }
        LineRenderer Line(string name, Transform parent, Color c, float width, int order, int points, bool worldSpace)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat; lr.startColor = lr.endColor = c; lr.widthMultiplier = width;
            lr.numCapVertices = 8; lr.numCornerVertices = 4; lr.positionCount = points; lr.useWorldSpace = worldSpace;
            lr.sortingOrder = order;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            return lr;
        }

        // ================= build =================
        void BuildCamera()
        {
            var go = new GameObject("IntroCamera"); go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0, -10);
            cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 360f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BG;
        }

        void BuildWorld()
        {
            BuildSprites();
            world = new GameObject("IntroWorld").transform; world.SetParent(transform, false);
            // vignette + bokeh
            var vig = Spr("Vignette", world, softSpr, A(Hex("#FBF6F0"), 0.9f), -10); vig.transform.position = U(640, 396); vig.transform.localScale = new Vector3(2400, 1500, 1);
            var rnd = new System.Random(7); string[] cols = { "#FF8A5B", "#FFB089", "#FF7043", "#FFC4A3" };
            for (int i = 0; i < 13; i++)
            {
                float r = 26 + (float)rnd.NextDouble() * 59, a = 0.16f + (float)rnd.NextDouble() * 0.18f;
                var b = new Bokeh
                {
                    x = -60 + (float)rnd.NextDouble() * 1400, y = -40 + (float)rnd.NextDouble() * 800,
                    vx = -14 + (float)rnd.NextDouble() * 28, vy = -9 + (float)rnd.NextDouble() * 7, ph = (float)rnd.NextDouble() * 6.28f
                };
                b.sr = Spr("Bokeh" + i, world, softSpr, A(Hex(cols[i % 4]), a * 1.6f), -9); b.sr.transform.localScale = Vector3.one * r * 2.6f;
                bokeh.Add(b);
            }
            shadow = Spr("Shadow", world, softSpr, A(Hex("#5B4A40"), 0.3f), 1);

            // limbs behind the body
            for (int s = 0; s < 2; s++)
            {
                legO[s] = Line("LegOutline" + s, world, LIMB_DARK, 18, 2, 10, true);
                leg[s] = Line("Leg" + s, world, LIMB, 13, 3, 10, true);
                footO[s] = Spr("FootOutline" + s, world, circleSpr, LIMB_DARK, 3).transform;
                foot[s] = Spr("Foot" + s, world, circleSpr, LIMB, 4).transform;
            }

            body = new GameObject("Body").transform; body.SetParent(world, false);
            // flame tuft (behind body)
            flameRoot = new GameObject("Flame").transform; flameRoot.SetParent(body, false);
            flameRoot.localPosition = new Vector3(0, 1.85f * R, 0); flameRoot.localScale = Vector3.one * 1.3f;
            float[,] fl = { { -28, 46, 15, -20 }, { 26, 40, 13, 20 }, { 0, 78, 24, 0 } };
            for (int i = 0; i < 3; i++)
            {
                var f = new GameObject("Flame" + i).transform; f.SetParent(flameRoot, false);
                f.localPosition = new Vector3(fl[i, 3], -10, 0);
                var o = Spr("Outer", f, flameOutSpr, Color.white, 5); o.transform.localPosition = new Vector3(0, fl[i, 1] * 0.5f, 0); o.transform.localScale = new Vector3(fl[i, 2] * 2f, fl[i, 1], 1);
                var n = Spr("Inner", f, flameInSpr, Color.white, 6); n.transform.localPosition = new Vector3(0, fl[i, 1] * 0.29f - 2, 0); n.transform.localScale = new Vector3(fl[i, 2] * 1.1f, fl[i, 1] * 0.58f, 1);
                flame[i] = f;
            }
            // wobbly blob body mesh (with a feathered rim for anti-aliasing)
            var bgo = new GameObject("Blob"); bgo.transform.SetParent(body, false);
            blobMesh = new Mesh { name = "AgniBlob" }; blobMesh.MarkDynamic();
            blobVerts = new Vector3[1 + BLOB_N * 2]; blobCols = new Color[blobVerts.Length];
            var tris = new List<int>();
            for (int i = 0; i < BLOB_N; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % BLOB_N, ao = 1 + BLOB_N + i, bo = 1 + BLOB_N + (i + 1) % BLOB_N;
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(a); tris.Add(b); tris.Add(bo); tris.Add(a); tris.Add(bo); tris.Add(ao);
            }
            UpdateBlob(0);
            blobMesh.triangles = tris.ToArray();
            bgo.AddComponent<MeshFilter>().sharedMesh = blobMesh;
            var mr = bgo.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.sortingOrder = 7;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;

            rim = Spr("Rim", body, softSpr, A(Color.white, 0.22f), 8); rim.transform.localPosition = new Vector3(-0.42f * R, 0.95f * R + 0.5f * R, 0); rim.transform.localScale = Vector3.one * 0.9f * R;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                blush[s] = Spr("Blush" + s, body, softSpr, A(Hex("#FF2E63"), 0), 9);
                blush[s].transform.localPosition = new Vector3(side * 72, 0.95f * R - 10, 0); blush[s].transform.localScale = new Vector3(46, 26, 1);
                var eg = new GameObject("Eye" + s).transform; eg.SetParent(body, false); eg.localPosition = new Vector3(side * 40, 0.95f * R + 24, 0);
                var sh = Spr("EyeShade", eg, softSpr, A(Hex("#B83A0C"), 0.45f), 9); sh.transform.localPosition = new Vector3(0, -3, 0); sh.transform.localScale = Vector3.one * 80;
                Spr("White", eg, eyeSpr, Color.white, 10).transform.localScale = Vector3.one * 66;
                var pg = new GameObject("Pupil").transform; pg.SetParent(eg, false);
                Spr("Black", pg, circleSpr, Hex("#141010"), 11).transform.localScale = Vector3.one * 29;
                var h1 = Spr("Hi1", pg, circleSpr, Color.white, 12); h1.transform.localPosition = new Vector3(-5, 5, 0); h1.transform.localScale = Vector3.one * 9.2f;
                var h2 = Spr("Hi2", pg, circleSpr, A(Color.white, 0.8f), 12); h2.transform.localPosition = new Vector3(4, -5, 0); h2.transform.localScale = Vector3.one * 3.6f;
                eyeGroup[s] = eg; pupil[s] = pg;
                closedEye[s] = Line("ClosedEye" + s, body, INK, 6.5f, 12, 9, false);
                lash[s] = Line("Lash" + s, body, INK, 5f, 12, 2, false);
                var ec = eg.localPosition; float er = 33;
                for (int k = 0; k < 9; k++)
                {
                    float u = k / 8f; // quad bezier ◡
                    Vector2 p0 = new Vector2(ec.x - er * 0.75f, ec.y + 2), c1 = new Vector2(ec.x, ec.y - er * 0.55f), p1 = new Vector2(ec.x + er * 0.75f, ec.y + 2);
                    Vector2 q = (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c1 + u * u * p1;
                    closedEye[s].SetPosition(k, q);
                }
                lash[s].SetPosition(0, new Vector3(ec.x + side * er * 0.72f, ec.y + 2)); lash[s].SetPosition(1, new Vector3(ec.x + side * er * 0.98f, ec.y + 11));
                closedEye[s].enabled = lash[s].enabled = false;
            }
            smile = Line("Smile", body, INK, 5, 12, 9, false);
            pucker = new GameObject("Pucker").transform; pucker.SetParent(body, false);
            Spr("Lips", pucker, circleSpr, Hex("#C0153A"), 12).transform.localScale = new Vector3(18, 20, 1);
            Spr("Hole", pucker, circleSpr, Hex("#5E0716"), 13).transform.localScale = new Vector3(8, 10, 1);
            var lh = Spr("Shine", pucker, circleSpr, A(Hex("#FF8AA0"), 0.8f), 13); lh.transform.localPosition = new Vector3(-3.5f, 6, 0); lh.transform.localScale = new Vector3(5, 4, 1);

            // arms in front
            for (int s = 0; s < 2; s++)
            {
                armO[s] = Line("ArmOutline" + s, world, ARM_DARK, 16, 14, 10, true);
                arm[s] = Line("Arm" + s, world, ARM, 11, 15, 10, true);
                handO[s] = Spr("HandOutline" + s, world, circleSpr, ARM_DARK, 15).transform; handO[s].localScale = Vector3.one * 24;
                hand[s] = Spr("Hand" + s, world, circleSpr, ARM, 16).transform; hand[s].localScale = Vector3.one * 19;
            }
            heart = Spr("Heart", world, heartSpr, Color.white, 30);
            for (int i = 0; i < 3; i++) miniHeart[i] = Spr("MiniHeart" + i, world, heartSpr, Color.white, 29);
            sparkA = Spr("SparkA", world, starSpr, Color.white, 31); sparkB = Spr("SparkB", world, starSpr, Color.white, 31);
        }

        void UpdateBlob(float t)
        {
            float cy = 0.95f * R;
            Color top = Hex("#FF7B3C"), mid = ORANGE, bot = Hex("#EC4A12");
            blobVerts[0] = new Vector3(0, cy, 0); blobCols[0] = mid;
            for (int i = 0; i < BLOB_N; i++)
            {
                float th = 2 * Mathf.PI * i / BLOB_N;
                float r = R * (1 + 0.028f * Mathf.Sin(3 * th + 1.3f + t * 1.7f) + 0.018f * Mathf.Sin(5 * th + 0.4f - t * 2.3f) + 0.012f * Mathf.Sin(2 * th + t * 3.1f));
                Vector3 dir = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0);
                blobVerts[1 + i] = new Vector3(0, cy, 0) + dir * r;
                blobVerts[1 + BLOB_N + i] = new Vector3(0, cy, 0) + dir * (r + 1.6f);
                // light from top-left
                float lit = Clamp01(0.5f + 0.5f * Vector2.Dot(new Vector2(dir.x, dir.y), new Vector2(-0.55f, 0.83f)));
                Color c = lit > 0.5f ? Color.Lerp(mid, top, (lit - 0.5f) * 2f) : Color.Lerp(bot, mid, lit * 2f);
                blobCols[1 + i] = c; blobCols[1 + BLOB_N + i] = A(c, 0);
            }
            blobMesh.vertices = blobVerts; blobMesh.colors = blobCols; blobMesh.RecalculateBounds();
        }

        void BuildUI()
        {
            var cgo = new GameObject("IntroUI"); cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1f;
            Font titleFont = Resources.Load<Font>("AgniIntro/LilitaOne-Regular");
            Font subFont = Resources.Load<Font>("AgniIntro/Baloo2");
            if (titleFont == null) titleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (subFont == null) subFont = titleFont;
            const int size = 136;
            titleFont.RequestCharactersInTexture(TITLE, size);
            letters = new Text[TITLE.Length]; letterX = new float[TITLE.Length];
            float total = 0; var adv = new float[TITLE.Length];
            for (int i = 0; i < TITLE.Length; i++)
            {
                adv[i] = titleFont.GetCharacterInfo(TITLE[i], out var ci, size) ? ci.advance : size * 0.5f;
                total += adv[i] + (i > 0 ? 4 : 0);
            }
            float x = -total / 2f;
            for (int i = 0; i < TITLE.Length; i++)
            {
                var go = new GameObject("L_" + TITLE[i]); go.transform.SetParent(cgo.transform, false);
                var tx = go.AddComponent<Text>(); tx.font = titleFont; tx.fontSize = size; tx.text = TITLE[i].ToString();
                tx.alignment = TextAnchor.MiddleCenter; tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
                tx.color = Color.white; tx.raycastTarget = false;
                var g = go.AddComponent<VerticalGradient>();
                g.top = i < 4 ? Hex("#FFD23F") : Hex("#FFB347"); g.bottom = i < 4 ? Hex("#FF4A1A") : Hex("#E8380D");
                var ol = go.AddComponent<Outline>(); ol.effectColor = Hex("#4A1505"); ol.effectDistance = new Vector2(5, -5);
                var ol2 = go.AddComponent<Outline>(); ol2.effectColor = Hex("#4A1505"); ol2.effectDistance = new Vector2(-4, 4);
                var shd = go.AddComponent<Shadow>(); shd.effectColor = A(Hex("#5A1A08"), 0.35f); shd.effectDistance = new Vector2(0, -10);
                var rt = tx.rectTransform; rt.sizeDelta = new Vector2(adv[i] + 40, 220);
                letterX[i] = x + adv[i] / 2f; rt.anchoredPosition = new Vector2(letterX[i], 360 - H_TITLE_Y);
                x += adv[i] + 4; letters[i] = tx; tx.enabled = TITLE[i] != ' ';
            }
            var sgo = new GameObject("Subtitle"); sgo.transform.SetParent(cgo.transform, false);
            subtitle = sgo.AddComponent<Text>(); subtitle.font = subFont; subtitle.fontSize = 42; subtitle.fontStyle = FontStyle.Bold;
            subtitle.text = "Zyqor  Studio"; subtitle.alignment = TextAnchor.MiddleCenter; subtitle.color = A(Hex("#6A3A26"), 0);
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow; subtitle.raycastTarget = false; subtitle.rectTransform.sizeDelta = new Vector2(600, 80);
            lineL = MakeImg(cgo.transform, "LineL", A(Hex("#E8A07E"), 0)); lineR = MakeImg(cgo.transform, "LineR", A(Hex("#E8A07E"), 0));
            fade = MakeImg(cgo.transform, "Fade", A(Hex("#1B120E"), 0));
            var frt = fade.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        }
        const float H_TITLE_Y = 720 * 0.53f - 136 * 0.36f; // letter centre line in reference px
        Image MakeImg(Transform p, string n, Color c)
        {
            var go = new GameObject(n); go.transform.SetParent(p, false);
            var im = go.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
        }

        // ================= the timeline (mirrors render_intro.py) =================
        struct St { public float x, h, sx, sy, tilt, swing, aswing, fsway, drop, wink, blink, blush, pucker, armMouth, armThrow, armWave; public Vector2 look; }

        static St State(float t)
        {
            var st = new St { sx = 1, sy = 1, look = new Vector2(0.6f, 0) };
            float sq = 0, x;
            if (t < 1.0f)
            {
                x = Lerp(-200, 640, 1 - Mathf.Pow(1 - t, 1.6f));
                float per = 1f / 3f; int i = Mathf.Min(2, (int)(t / per)); float p = (t - i * per) / per;
                float[] Hs = { 78, 58, 34 };
                st.h = Hs[i] * Mathf.Sin(Mathf.PI * p); st.swing = 16 * Mathf.Sin(2 * Mathf.PI * p);
                sq += -0.07f * (1 - Mathf.Sin(Mathf.PI * p)) * 0.6f;
                sq += Spring(t - 1f / 3f, 0.12f, 26, 14) + Spring(t - 2f / 3f, 0.12f, 26, 14);
                st.tilt = 7 * (1 - t) + 3; st.fsway = -14; st.look = new Vector2(0.7f, 0.1f); st.aswing = 10 * Mathf.Sin(2 * Mathf.PI * p);
            }
            else
            {
                x = 640; sq += Spring(t - 1.0f, 0.16f, 20, 7);
                st.tilt = Spring(t - 1.0f, 4, 13, 6); st.fsway = Spring(t - 1.0f, -14, 12, 5);
            }
            if (t >= 1.0f && t < 3.1f) { float k = Eio(Seg(t, 1.05f, 1.3f)); st.look = new Vector2(Lerp(0.7f, 0, k), Lerp(0.1f, 0.15f, k)); }
            if (t > 1.25f && t < 1.34f) st.blink = Mathf.Sin(Mathf.PI * Seg(t, 1.25f, 1.34f));
            float tl = Eio(Seg(t, 1.4f, 1.55f)) * (1 - Eio(Seg(t, 1.85f, 2.02f)));
            st.tilt += -10 * tl;
            st.wink = Eout(Seg(t, 1.5f, 1.6f)) * (1 - Eio(Seg(t, 1.8f, 1.9f)));
            st.blush = Mathf.Max(Eio(Seg(t, 1.5f, 1.65f)) * (1 - Seg(t, 3.0f, 3.3f)), 0);
            if (t > 1.4f && t < 2.0f) st.look = new Vector2(Lerp(0, -0.25f, tl), 0.15f);
            float a1 = Eio(Seg(t, 2.0f, 2.2f)), a2 = Eio(Seg(t, 2.32f, 2.45f)), a3 = Eio(Seg(t, 2.6f, 2.9f));
            st.pucker = Eout(Seg(t, 2.02f, 2.18f)) * (1 - Eio(Seg(t, 2.42f, 2.6f)));
            st.armMouth = a1 * (1 - a2); st.armThrow = a2 * (1 - a3);
            st.tilt += -5 * a1 * (1 - a2) + 9 * a2 * (1 - a3);
            sq += 0.06f * a1 * (1 - a2) - 0.07f * a2 * (1 - a3);
            if (t > 2.4f) sq += Spring(t - 2.4f, -0.05f, 22, 9);
            if (t > 2.0f && t < 2.9f) st.look = new Vector2(Lerp(0, 0.35f, a2), Lerp(0.15f, -0.25f, a2));
            st.armWave = Eio(Seg(t, 2.85f, 2.95f)) * (1 - Eio(Seg(t, 3.18f, 3.3f)));
            if (t >= 3.05f) st.look = new Vector2(1, 0);
            if (t >= 3.1f && t < 3.25f) { float k = Mathf.Sin(Mathf.PI * Seg(t, 3.1f, 3.25f)); sq += 0.12f * k; st.tilt += -6 * k; }
            if (t >= 3.25f)
            {
                float tt = t - 3.25f; x = 640 + 940 * Mathf.Pow(Ein(Clamp01(tt / 0.55f)), 0.9f);
                float per = 0.26f; int i = (int)(tt / per); float p = (tt % per) / per;
                float[] Hs = { 85, 75, 60, 50 };
                st.h = Hs[Mathf.Min(i, 3)] * Mathf.Sin(Mathf.PI * p);
                st.swing = 18 * Mathf.Sin(2 * Mathf.PI * p); st.aswing = 12 * Mathf.Sin(2 * Mathf.PI * p);
                st.tilt += 12; st.fsway = -18;
                sq += -0.08f * (1 - Mathf.Sin(Mathf.PI * p));
                for (int k = 1; k < 4; k++) sq += Spring(tt - k * per, 0.1f, 26, 14);
            }
            st.x = x; st.sy = 1 - sq; st.sx = 1 + sq * 0.85f; st.drop = Mathf.Max(0, sq) * 30;
            return st;
        }

        void Apply(float t)
        {
            // background
            foreach (var b in bokeh)
                b.sr.transform.position = U(b.x + b.vx * t + 8 * Mathf.Sin(t * 0.9f + b.ph), b.y + b.vy * t + 6 * Mathf.Cos(t * 0.7f + b.ph));

            var st = State(t);
            bool visible = st.x < 1500;
            body.gameObject.SetActive(visible);
            float hipy = G - L - st.h + st.drop;
            body.position = U(st.x, hipy);
            body.rotation = Quaternion.Euler(0, 0, -st.tilt);
            body.localScale = new Vector3(st.sx * S, st.sy * S, 1);
            UpdateBlob(t);

            float k = Clamp01(1 - st.h / 260f);
            shadow.enabled = visible;
            shadow.transform.position = U(st.x, G); shadow.transform.localScale = new Vector3(260 * k * st.sx, 34 * k, 1);
            shadow.color = A(Hex("#5B4A40"), (60 * k + 15) / 255f * 2.2f);

            // flame flicker
            for (int i = 0; i < 3; i++)
            {
                float ph = i == 0 ? 1.1f : i == 1 ? 2.3f : 0f, ang = i == 0 ? -28 : i == 1 ? 26 : 0;
                float f = 1 + 0.09f * Mathf.Sin(t * 17 + ph) + 0.05f * Mathf.Sin(t * 29 + ph * 2);
                flame[i].localRotation = Quaternion.Euler(0, 0, -(ang + st.fsway * (i == 2 ? 1.2f : 0.8f) + 3 * Mathf.Sin(t * 9 + ph)));
                flame[i].localScale = new Vector3(1, f, 1);
            }
            // face
            for (int s = 0; s < 2; s++)
            {
                float close = s == 1 ? st.wink : 0; close = Mathf.Max(close, st.blink);
                bool shut = close > 0.93f;
                eyeGroup[s].gameObject.SetActive(!shut && visible);
                closedEye[s].enabled = lash[s].enabled = shut && visible;
                eyeGroup[s].localScale = new Vector3(1, Mathf.Max(0.06f, 1 - close), 1);
                pupil[s].localPosition = new Vector3(st.look.x * 12, -st.look.y * 10, 0);
                blush[s].color = A(Hex("#FF2E63"), 0.47f * st.blush);
            }
            float pk = st.pucker, my = 0.95f * R - 32;   // local y of mouth (y up)
            smile.enabled = pk < 0.98f;
            float w = 15 * (1 - pk) + 2;
            for (int i = 0; i < 9; i++)
            {
                float u = i / 8f;
                Vector2 p0 = new Vector2(-w, my + 3), c1 = new Vector2(0, my - (9 * (1 - pk) + 2)), p1 = new Vector2(w, my + 3);
                smile.SetPosition(i, (1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * c1 + u * u * p1);
            }
            pucker.gameObject.SetActive(pk > 0.02f);
            pucker.localPosition = new Vector3(4 * pk * 1.45f, my - 1, 0); pucker.localScale = Vector3.one * pk * 1.45f;

            // legs
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                Vector2 hp = Py(body.TransformPoint(new Vector3(side * 30, 6, 0)));
                float fx = hp.x + side * 6 + st.swing * side, fy = Mathf.Min(G - 7, hp.y + L + 2);
                float comp = Mathf.Max(0, (hp.y + L + 2) - (G - 7));
                Vector2 c = new Vector2((hp.x + fx) / 2 + side * (comp * 0.9f + 2), (hp.y + fy) / 2);
                for (int i = 0; i < 10; i++)
                {
                    float u = i / 9f; Vector2 q = (1 - u) * (1 - u) * hp + 2 * (1 - u) * u * c + u * u * new Vector2(fx, fy);
                    leg[s].SetPosition(i, U(q.x, q.y)); legO[s].SetPosition(i, U(q.x, q.y));
                }
                foot[s].position = U(fx + 1 + 7, fy); foot[s].localScale = new Vector3(30, 18, 1);
                footO[s].position = foot[s].position; footO[s].localScale = new Vector3(35, 23, 1);
                leg[s].enabled = legO[s].enabled = visible; foot[s].gameObject.SetActive(visible); footO[s].gameObject.SetActive(visible);
            }
            // arms
            Vector2 mouth = Py(body.TransformPoint(new Vector3(10, my, 0)));
            Vector2 throwHand = Vector2.zero;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1;
                Vector2 sh = Py(body.TransformPoint(new Vector3(side * 0.93f * R, 0.59f * R, 0)));
                Vector2 h = new Vector2(sh.x + side * 22 - side * st.aswing, sh.y + 56);
                if (s == 1)
                {
                    h = Vector2.Lerp(h, new Vector2(mouth.x + 12, mouth.y + 4), st.armMouth);
                    h = Vector2.Lerp(h, new Vector2(sh.x + 88, sh.y - 70), st.armThrow);
                    h = Vector2.Lerp(h, new Vector2(sh.x + 34 + 26 * Mathf.Sin(2 * Mathf.PI * 3.2f * t), sh.y - 76), st.armWave);
                    throwHand = h;
                }
                Vector2 c = new Vector2((sh.x + h.x) / 2 + side * 10, (sh.y + h.y) / 2 + 12);
                for (int i = 0; i < 10; i++)
                {
                    float u = i / 9f; Vector2 q = (1 - u) * (1 - u) * sh + 2 * (1 - u) * u * c + u * u * h;
                    arm[s].SetPosition(i, U(q.x, q.y)); armO[s].SetPosition(i, U(q.x, q.y));
                }
                hand[s].position = U(h.x, h.y); handO[s].position = hand[s].position;
                arm[s].enabled = armO[s].enabled = visible; hand[s].gameObject.SetActive(visible); handO[s].gameObject.SetActive(visible);
            }
            // wink sparkle
            float sa = (t > 1.55f && t < 1.95f) ? Mathf.Sin(Mathf.PI * Seg(t, 1.55f, 1.95f)) : 0;
            Vector2 eye = Py(body.TransformPoint(new Vector3(73, 0.95f * R + 54, 0)));
            sparkA.enabled = sparkB.enabled = sa > 0.01f;
            sparkA.transform.position = U(eye.x + 8, eye.y - 6); sparkA.transform.localScale = Vector3.one * (32 * sa + 4);
            sparkB.transform.position = U(eye.x + 30, eye.y + 14); sparkB.transform.localScale = Vector3.one * (18 * sa + 2);
            sparkB.color = A(Color.white, sa * 0.8f);

            // heart: pops out of the throwing hand, flies up-right toward camera, beats twice, fades
            if (t >= 2.46f && !heartOriginSet) { heartOrigin = throwHand + new Vector2(18, -18); heartOriginSet = true; }
            if (!heartOriginSet && t >= 2.4f) heartOrigin = throwHand + new Vector2(18, -18);
            bool hv = t >= 2.4f && t < 3.45f;
            heart.enabled = hv;
            if (hv)
            {
                float hk = Eio(Seg(t, 2.46f, 3.45f)), kx = Eout(Seg(t, 2.46f, 3.45f));
                float hx = Lerp(heartOrigin.x, heartOrigin.x + 230, kx), hy = Lerp(heartOrigin.y, heartOrigin.y - 170, hk) - 25 * Mathf.Sin(Mathf.PI * kx);
                float pop = Eback(Seg(t, 2.4f, 2.6f), 2.6f);
                float beat = 1 + 0.13f * Mathf.Exp(-28 * Mathf.Abs(t - 2.78f)) + 0.13f * Mathf.Exp(-28 * Mathf.Abs(t - 2.98f));
                float sz = Lerp(46, 170, Eout(Seg(t, 2.46f, 3.4f))) * pop * beat;
                heart.transform.position = U(hx, hy); heart.transform.localScale = Vector3.one * sz * 1.36f;
                heart.transform.rotation = Quaternion.Euler(0, 0, -(8 + 10 * Mathf.Sin(t * 5) * (1 - hk)));
                heart.color = A(Color.white, 1 - Eio(Seg(t, 3.08f, 3.45f)));
            }
            float[,] mh = { { -80, -40, 30, 0.0f }, { 70, 60, 24, 0.1f }, { 10, -110, 20, 0.2f } };
            for (int j = 0; j < 3; j++)
            {
                float dl = mh[j, 3], k2 = Seg(t, 2.62f + dl, 3.35f + dl);
                bool on = k2 > 0 && k2 < 1; miniHeart[j].enabled = on; if (!on) continue;
                float q = Seg(2.62f + dl, 2.46f, 3.45f);
                float bx = Lerp(heartOrigin.x, heartOrigin.x + 230, Eout(q)), by = Lerp(heartOrigin.y, heartOrigin.y - 170, Eio(q));
                miniHeart[j].transform.position = U(bx + mh[j, 0] * Eout(k2), by + mh[j, 1] * Eout(k2) - 60 * k2);
                miniHeart[j].transform.localScale = Vector3.one * mh[j, 2] * Eback(Seg(t, 2.62f + dl, 2.82f + dl)) * 1.36f;
                miniHeart[j].transform.rotation = Quaternion.Euler(0, 0, -(-15 + j * 20));
                miniHeart[j].color = A(Color.white, 1 - Eio(k2));
            }

            // title
            for (int i = 0; i < letters.Length; i++)
            {
                float lk = Seg(t, 3.82f + i * 0.035f, 3.82f + i * 0.035f + 0.5f);
                float s = Elastic(lk), a = Clamp01(lk * 4);
                var rt = letters[i].rectTransform;
                rt.localScale = Vector3.one * s;
                rt.localRotation = Quaternion.Euler(0, 0, -(1 - Eout(lk)) * (i % 2 == 1 ? -14 : 14));
                var c = letters[i].color; c.a = a; letters[i].color = c;
            }
            float sa2 = Eout(Seg(t, 4.15f, 4.45f));
            float y2 = 720 * 0.53f + 66 + 18 * (1 - sa2) - 14;
            subtitle.rectTransform.anchoredPosition = new Vector2(0, 360 - y2);
            subtitle.color = A(Hex("#6A3A26"), sa2);
            float half = subtitle.preferredWidth / 2f;
            foreach (var (ln, sgn) in new[] { (lineL, -1f), (lineR, 1f) })
            {
                ln.rectTransform.sizeDelta = new Vector2(48, 3); ln.rectTransform.anchoredPosition = new Vector2(sgn * (half + 46), 360 - y2);
                ln.color = A(Hex("#E8A07E"), 0.86f * sa2);
            }
            fade.color = A(Hex("#1B120E"), Eio(Seg(t, 4.62f, 5.0f)));
        }
    }

    /// Vertical two-colour gradient for legacy UI Text (applied before Outline/Shadow).
    public class VerticalGradient : BaseMeshEffect
    {
        public Color top = Color.yellow, bottom = Color.red;
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var v = new UIVertex(); float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++) { vh.PopulateUIVertex(ref v, i); minY = Mathf.Min(minY, v.position.y); maxY = Mathf.Max(maxY, v.position.y); }
            float hgt = Mathf.Max(1e-3f, maxY - minY);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Color c = Color.Lerp(bottom, top, (v.position.y - minY) / hgt); c.a *= v.color.a / 255f;
                v.color = c; vh.SetUIVertex(v, i);
            }
        }
    }
}
