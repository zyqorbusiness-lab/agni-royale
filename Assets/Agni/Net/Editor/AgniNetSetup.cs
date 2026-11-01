using System.Collections.Generic;
// Agni Royale — editor setup for the multiplayer build: network prefabs, the AgniArena test island scene,
// import settings for combat clips / rifle, build list, and Linux client + headless server builds.
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod Agni.Net.EditorTools.AgniNetSetup.SetupBatch -logFile <log>
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod Agni.Net.EditorTools.AgniNetSetup.BuildLinuxBatch -out <dir> -logFile <log>
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agni.Net.EditorTools
{
    public class AgniNetImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            var mi = assetImporter as ModelImporter; if (mi == null) return;
            if (assetPath.Contains("/Resources/AgniNet/Anims/"))
            {
                mi.animationType = ModelImporterAnimationType.Legacy; mi.importAnimation = true; mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false;
            }
            else if (assetPath.Contains("/Resources/AgniNet/Weapons/"))
            {
                mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importCameras = false; mi.importLights = false;
            }
        }
    }

    public static class AgniNetSetup
    {
        const string R = "Assets/Agni/Net/Resources/AgniNet/";
        public const string ArenaPath = "Assets/Agni/Net/AgniArena.unity";

        [MenuItem("Agni Royale/Net/Setup prefabs + arena")]
        public static void SetupAll() { Prefabs(); Arena(); BuildList(); AssetDatabase.SaveAssets(); }

        /// Editor proof: open a scene and enter play mode; AgniShot (runtime) takes over when "-agniShot <dir>" is set.
        ///   Unity -batchmode -force-glcore -executeMethod Agni.Net.EditorTools.AgniNetSetup.ShotBatch -agniShotScene Lobby|AgniArena -agniShot <dir> -agniShotMode friends|grenade
        public static void ShotBatch()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-agniShotScene");
            var scene = i >= 0 && i + 1 < a.Length ? a[i + 1] : "AgniArena";
            var path = AssetDatabase.FindAssets("t:Scene " + scene).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);
            if (path == null) { Debug.LogError("[AgniShot] scene not found " + scene); EditorApplication.Exit(2); return; }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[AgniShot] entering play mode in " + path);
            EditorApplication.EnterPlaymode();
        }

        public static void SetupBatch()
        {
            int code = 0;
            try { SetupAll(); CheckClips(); } catch (System.Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log("[AgniNetSetup] done code=" + code); EditorApplication.Exit(code);
        }

        static void CheckClips()
        {
            var meera = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Agni/Characters/Resources/AgniChars/Meera.fbx");
            foreach (var path in Directory.GetFiles(R + "Anims", "*.fbx"))
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                foreach (var c in clips)
                {
                    var b = AnimationUtility.GetCurveBindings(c); int found = 0;
                    foreach (var x in b) if (meera != null && (x.path == "" || meera.transform.Find("Meera_Rig/" + x.path) != null)) found++;
                    Debug.Log("[AgniNetSetup] clip " + Path.GetFileName(path) + " '" + c.name + "' legacy=" + c.legacy + " len=" + c.length + " curves=" + b.Length + " bound-on-Meera=" + found + (b.Length > 0 ? " e.g. " + b[0].path : ""));
                }
            }
        }

        // ------------------------------------------------------------------ prefabs
        static GameObject Save(GameObject go, string name) { var p = PrefabUtility.SaveAsPrefabAsset(go, R + name + ".prefab"); Object.DestroyImmediate(go); Debug.Log("[AgniNetSetup] prefab " + name); return p; }
        static GameObject NO(string name) { var go = new GameObject(name); go.AddComponent<NetworkObject>(); return go; }
        static void Prefabs()
        {
            Directory.CreateDirectory(R);
            // fighter
            var pl = NO("AgniPlayer");
            var cc = pl.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50; cc.skinWidth = 0.05f; cc.minMoveDistance = 0;
            var nt = pl.AddComponent<NetworkTransform>(); nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.Interpolate = true;
            pl.AddComponent<AgniPlayer>();
            var head = new GameObject("Head"); head.transform.SetParent(pl.transform, false); head.transform.localPosition = new Vector3(0, 1.62f, 0.02f);
            var hs = head.AddComponent<SphereCollider>(); hs.radius = 0.15f; hs.isTrigger = true;
            Save(pl, "AgniPlayer");
            // throwable
            var th = NO("AgniThrowable");
            var rb = th.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var sc = th.AddComponent<SphereCollider>(); sc.radius = ThrowMath.Radius;
            var tnt = th.AddComponent<NetworkTransform>(); tnt.SyncScaleX = tnt.SyncScaleY = tnt.SyncScaleZ = false; tnt.Interpolate = true;
            th.AddComponent<AgniThrowable>(); Save(th, "AgniThrowable");
            var w = NO("AgniWall"); w.AddComponent<AgniWall>(); Save(w, "AgniWall");
            var pk = NO("AgniPickup"); pk.AddComponent<AgniPickup>(); Save(pk, "AgniPickup");
            var cr = NO("AgniCrate"); var crt = cr.AddComponent<NetworkTransform>(); crt.SyncRotAngleX = false; crt.SyncRotAngleZ = false; crt.SyncScaleX = crt.SyncScaleY = crt.SyncScaleZ = false; crt.Interpolate = true;
            cr.AddComponent<AgniCrate>(); Save(cr, "AgniCrate");
            var m = NO("AgniMatch"); m.AddComponent<AgniMatch>(); Save(m, "AgniMatch");
            var cb = NO("AgniCombat"); cb.AddComponent<AgniCombat>(); Save(cb, "AgniCombat");
            var z = NO("AgniZone"); z.AddComponent<AgniZone>(); Save(z, "AgniZone");
            var pt = NO("AgniParty"); pt.AddComponent<Agni.Social.AgniPartyNet>(); Save(pt, "AgniParty");
        }

        // ------------------------------------------------------------------ AGNI AKHADA test island
        static Material Lit(string name, Color c, float smooth = 0.15f, Texture2D tex = null, float tile = 1)
        {
            var dir = "Assets/Agni/Net/Arena/"; Directory.CreateDirectory(dir);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", Vector2.one * tile); }
            AssetDatabase.CreateAsset(m, dir + name + ".mat"); return m;
        }
        static Texture2D NoiseTexture(string name, Color a, Color b, Color c, float scale)
        {
            var path = "Assets/Agni/Net/Arena/" + name + ".png"; Directory.CreateDirectory(Path.GetDirectoryName(path));
            int n = 512; var t = new Texture2D(n, n, TextureFormat.RGB24, false);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float F(float s, float o) { float aa = Mathf.PerlinNoise(u * s + o, v * s + o), bb = Mathf.PerlinNoise((u - 1) * s + o, v * s + o), cc2 = Mathf.PerlinNoise(u * s + o, (v - 1) * s + o), dd = Mathf.PerlinNoise((u - 1) * s + o, (v - 1) * s + o); return Mathf.Lerp(Mathf.Lerp(aa, bb, u), Mathf.Lerp(cc2, dd, u), v); }
                    float f1 = F(scale, 3.1f), f2 = F(scale * 4, 7.7f), f3 = F(scale * 16, 1.3f);
                    var col = Color.Lerp(a, b, Mathf.SmoothStep(0.35f, 0.65f, f1)); col = Color.Lerp(col, c, Mathf.Clamp01((f2 - 0.6f) * 3f)); col *= 0.9f + 0.2f * f3;
                    t.SetPixel(x, y, col);
                }
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t); AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Transform root; static System.Random rnd; static Material mWallA, mWallB, mWallC, mRoof, mStone, mWood, mCompound;
        static float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        static GameObject Box(string n, Vector3 pos, Vector3 size, Material m, Transform parent, float yaw = 0)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = size; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            g.GetComponent<MeshRenderer>().sharedMaterial = m; GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic); return g;
        }
        static void House(Vector3 pos, float yaw, float w, float d, float h, Material wall, bool twoFloor)
        {
            var hs = new GameObject("House").transform; hs.SetParent(root, false); hs.localPosition = pos; hs.localRotation = Quaternion.Euler(0, yaw, 0);
            float t = 0.25f, door = 1.4f;
            Box("Floor", new Vector3(0, 0.1f, 0), new Vector3(w, 0.2f, d), mStone, hs);
            Box("Back", new Vector3(0, h / 2, d / 2 - t / 2), new Vector3(w, h, t), wall, hs);
            Box("Left", new Vector3(-w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), wall, hs);
            Box("Right", new Vector3(w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), wall, hs);
            float side = (w - door) / 2; Box("FrontL", new Vector3(-w / 2 + side / 2, h / 2, -d / 2 + t / 2), new Vector3(side, h, t), wall, hs);
            Box("FrontR", new Vector3(w / 2 - side / 2, h / 2, -d / 2 + t / 2), new Vector3(side, h, t), wall, hs);
            Box("Lintel", new Vector3(0, h - 0.4f, -d / 2 + t / 2), new Vector3(door, 0.8f, t), wall, hs);
            // window holes on the side walls are faked with dark insets (cheap on phones)
            Box("Win", new Vector3(-w / 2 - 0.01f, h * 0.6f, 0), new Vector3(0.05f, 0.8f, 1.1f), mWood, hs); Box("Win2", new Vector3(w / 2 + 0.01f, h * 0.6f, 0), new Vector3(0.05f, 0.8f, 1.1f), mWood, hs);
            Box("Roof", new Vector3(0, h + 0.1f, 0), new Vector3(w + 0.3f, 0.2f, d + 0.3f), mRoof, hs);
            // Indian flat-roof parapet
            Box("ParapetF", new Vector3(0, h + 0.55f, -d / 2 - 0.1f), new Vector3(w + 0.3f, 0.7f, 0.15f), wall, hs); Box("ParapetB", new Vector3(0, h + 0.55f, d / 2 + 0.1f), new Vector3(w + 0.3f, 0.7f, 0.15f), wall, hs);
            Box("ParapetL", new Vector3(-w / 2 - 0.1f, h + 0.55f, 0), new Vector3(0.15f, 0.7f, d + 0.3f), wall, hs); Box("ParapetR", new Vector3(w / 2 + 0.1f, h + 0.55f, 0), new Vector3(0.15f, 0.7f, d + 0.3f), wall, hs);
            if (twoFloor) { Box("Upper", new Vector3(-w * 0.2f, h + 0.2f + h * 0.4f, d * 0.15f), new Vector3(w * 0.5f, h * 0.8f, d * 0.6f), wall, hs); }
            // outside stairs to the roof
            for (int i = 0; i < 8; i++) Box("Step" + i, new Vector3(w / 2 + 0.6f, (i + 0.5f) * h / 8f, -d / 2 + 0.6f + i * 0.45f), new Vector3(1.0f, h / 8f, 0.45f), mStone, hs);
        }
        static readonly System.Collections.Generic.List<(Vector2 c, float r)> blockers = new System.Collections.Generic.List<(Vector2, float)>();
        static bool Blocked(Vector3 p, float pad) { if (Agni.Import.AgniIsland.IsWaterOrBank(p.x, p.z, pad)) return true; foreach (var b in blockers) if ((new Vector2(p.x, p.z) - b.c).magnitude < b.r + pad) return true; return false; }
        static GameObject Prop(string kenney, Vector3 pos, float scale, float yaw, bool trunkCollider)
        {
            if (Blocked(pos, trunkCollider ? 2.5f : 1.5f)) return null;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Agni/Kenney/" + kenney + ".fbx"); if (src == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src); g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localScale = Vector3.one * scale; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return g; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (trunkCollider) { var c = g.AddComponent<CapsuleCollider>(); c.radius = 0.3f / scale * 1.4f; c.height = b.size.y / scale; c.center = new Vector3(0, b.size.y / scale / 2, 0); }
            else { var c = g.AddComponent<BoxCollider>(); c.center = g.transform.InverseTransformPoint(b.center); c.size = new Vector3(b.size.x, b.size.y, b.size.z) / scale; }
            foreach (var t in g.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return g;
        }

        static GameObject Tree(GameObject pf, Vector3 pos, float scale, float yaw, float pad)
        {
            if (pf == null || Blocked(pos, pad)) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(root, false); g.transform.localPosition = pos; g.transform.localScale = Vector3.one * scale; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }
        public static void ArenaBatch() { int c = 0; try { Arena(); AssetDatabase.SaveAssets(); } catch (System.Exception e) { Debug.LogException(e); c = 1; } EditorApplication.Exit(c); }
        static void Arena()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            rnd = new System.Random(2026);
            root = new GameObject("AgniAkhada").transform;
            var grass = NoiseTexture("T_Ground", new Color(0.47f, 0.56f, 0.27f), new Color(0.62f, 0.60f, 0.38f), new Color(0.55f, 0.45f, 0.30f), 3f);
            var sand = NoiseTexture("T_Sand", new Color(0.86f, 0.78f, 0.58f), new Color(0.80f, 0.71f, 0.52f), new Color(0.74f, 0.66f, 0.50f), 4f);
            var plaster = NoiseTexture("T_Plaster", new Color(0.93f, 0.90f, 0.84f), new Color(0.86f, 0.82f, 0.75f), new Color(0.78f, 0.74f, 0.68f), 6f);
            var mGround = Lit("M_Ground", Color.white, 0.08f, grass, 70); var mSand = Lit("M_Sand", Color.white, 0.1f, sand, 40);
            mWallA = Lit("M_WallOchre", new Color(0.93f, 0.74f, 0.45f), 0.1f, plaster, 2); mWallB = Lit("M_WallBlue", new Color(0.62f, 0.78f, 0.86f), 0.1f, plaster, 2); mWallC = Lit("M_WallPink", new Color(0.93f, 0.68f, 0.66f), 0.1f, plaster, 2);
            mRoof = Lit("M_Roof", new Color(0.72f, 0.66f, 0.6f), 0.1f, plaster, 3); mStone = Lit("M_Stone", new Color(0.62f, 0.6f, 0.56f), 0.12f, plaster, 3); mWood = Lit("M_Window", new Color(0.18f, 0.14f, 0.12f), 0.2f);
            mCompound = Lit("M_Compound", new Color(0.78f, 0.62f, 0.48f), 0.1f, plaster, 4);
            var mSea = Lit("M_Sea", new Color(0.16f, 0.42f, 0.52f), 0.85f); var wsh = Shader.Find("AgniRoyale/Water"); if (wsh != null) { mSea.shader = wsh; EditorUtility.SetDirty(mSea); } // IMPORT v1 water

            // ground: the IMPORT v1 island (440 m grass + beach, river under Ghanta Ghar's bridge, Kayal's backwater canal) — AgniIslandMesh / AgniIsland.Height
            Agni.Import.EditorTools.AgniIslandMesh.Build(root, mGround, mSand);
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Sea"; sea.transform.SetParent(root, false); sea.transform.localScale = new Vector3(400, 1, 400); sea.transform.localPosition = new Vector3(0, Agni.Import.AgniIsland.SeaY, 0); sea.layer = 4; /* Water layer: boats ignore it, walkers stand on it */ sea.GetComponent<MeshRenderer>().sharedMaterial = mSea;

            // villages (AgniVillages, built at runtime from code): build once here only to learn the footprints, keep props off them
            AgniVillageSetup.Materials();
            AgniVillages.Root = null; var vr = AgniVillages.Build(null); blockers.Clear();
            foreach (var f in AgniVillages.Footprints) blockers.Add((f.c, f.r + 1.5f));
            Object.DestroyImmediate(vr.gameObject); AgniVillages.Root = null;
            // compound walls (waist / chest high cover)
            for (int k = 0; k < 95; k++) { float a = Rf(0, Mathf.PI * 2), r = Rf(15, 420); var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); if (Blocked(p, 7f)) continue; Box("Compound", p + Vector3.up * 0.65f, new Vector3(Rf(6, 14), 1.3f, 0.35f), mCompound, root, Rf(0, 180)); }
            // nature: our own Indian trees (neem, mango, palm, khejri, gulmohar, banana, lantana bushes); a few kit rocks + barrels/crates
            var treePf = AgniTreeSetup.Build(); var big = new List<GameObject>(); var bushes = new List<GameObject>();
            foreach (var kv in treePf) { if (kv.Key.StartsWith("Lantana") || kv.Key.StartsWith("Banana")) bushes.Add(kv.Value); else big.Add(kv.Value); if (kv.Key.StartsWith("Neem") || kv.Key.StartsWith("Mango")) big.Add(kv.Value); }
            for (int k = 0; k < 340 && big.Count > 0; k++) { float a = Rf(0, Mathf.PI * 2), r = Mathf.Sqrt(Rf(0.02f, 1f)) * 432f; Tree(big[rnd.Next(big.Count)], new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), Rf(0.85f, 1.12f), Rf(0, 360), 3.5f); }
            // village edges: banana plants + lantana hedges around each village ring
            foreach (var v in AgniVillages.Villages) for (int k = 0; k < 7 && bushes.Count > 0; k++) { float a = Rf(0, Mathf.PI * 2); var p = new Vector3(v.c.x + Mathf.Cos(a) * (v.r + Rf(2, 9)), 0, v.c.y + Mathf.Sin(a) * (v.r + Rf(2, 9))); Tree(bushes[rnd.Next(bushes.Count)], p, Rf(0.8f, 1.15f), Rf(0, 360), 1.5f); }
            for (int k = 0; k < 140 && bushes.Count > 0; k++) { float a = Rf(0, Mathf.PI * 2), r = Mathf.Sqrt(Rf(0.02f, 1f)) * 430f; Tree(bushes[rnd.Next(bushes.Count)], new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), Rf(0.8f, 1.2f), Rf(0, 360), 1.5f); }
            string[] rocks = { "rock_largeA", "rock_tallA", "stone_tallA" };
            for (int k = 0; k < 0; k++) { float a = Rf(0, Mathf.PI * 2), r = Mathf.Sqrt(Rf(0.02f, 1f)) * 240f; Prop(rocks[rnd.Next(rocks.Length)], new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), Rf(2.5f, 4.5f), Rf(0, 360), false); }
            string[] small = { "barrel", "box-large" };
            for (int k = 0; k < 80; k++) { float a = Rf(0, Mathf.PI * 2), r = Mathf.Sqrt(Rf(0.01f, 1f)) * 420f; var nm = small[rnd.Next(small.Length)]; Prop(nm, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), 2.2f, Rf(0, 360), false); }

            // light: bright warm Indian daylight, soft haze
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.95f, 0.86f); sun.intensity = 1.35f; sun.shadows = LightShadows.Soft; sunGo.transform.rotation = Quaternion.Euler(47, -38, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f); RenderSettings.ambientEquatorColor = new Color(0.66f, 0.64f, 0.55f); RenderSettings.ambientGroundColor = new Color(0.33f, 0.29f, 0.22f);
            var sky = new Material(Shader.Find("Skybox/Procedural")) { name = "M_Sky" }; sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 0.9f); sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.75f)); sky.SetColor("_GroundColor", new Color(0.55f, 0.55f, 0.5f)); sky.SetFloat("_Exposure", 1.25f);
            AssetDatabase.CreateAsset(sky, "Assets/Agni/Net/Arena/M_Sky.mat"); RenderSettings.skybox = sky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 170; RenderSettings.fogEndDistance = 1100; RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.84f);
            // preview camera (before your fighter spawns)
            var camGo = new GameObject("ArenaPreviewCam"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 1500; camGo.transform.position = new Vector3(0, 110, -380); camGo.transform.LookAt(Vector3.zero); camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var boot = new GameObject("AgniArenaBootstrap").AddComponent<AgniArenaBootstrap>(); boot.arenaRadius = 400f;
            EditorSceneManager.SaveScene(scene, ArenaPath);
            Debug.Log("[AgniNetSetup] arena saved, objects=" + root.GetComponentsInChildren<Transform>().Length);
        }

        static void BuildList()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ArenaPath))
            {
                int i = list.FindIndex(s => s.path.EndsWith("Lobby.unity")); list.Insert(i >= 0 ? i + 1 : list.Count, new EditorBuildSettingsScene(ArenaPath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log("[AgniNetSetup] build list: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        // ------------------------------------------------------------------ Linux builds (test client + headless server)
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
        static string[] NetScenes() => EditorBuildSettings.scenes.Where(s => s.enabled && !s.path.EndsWith("AgniMap.unity")).Select(s => s.path).ToArray();

        [MenuItem("Agni Royale/Net/Build Linux client")]
        public static void BuildLinuxClient() { Build(false); }
        [MenuItem("Agni Royale/Net/Build Linux dedicated server")]
        public static void BuildLinuxServer() { Build(true); }
        public static void BuildLinuxBatch() { int c = Build(AgniNetConfig.Has("-server")) ? 0 : 1; EditorApplication.Exit(c); }

        static bool Build(bool server)
        {
            string outDir = Arg("-out", Path.GetFullPath("../builds/" + (server ? "linux_server" : "linux_client")));
            Directory.CreateDirectory(outDir);
            var scenes = NetScenes();
            if (AgniNetConfig.Has("-arenaFirst")) scenes = scenes.Where(s => s == ArenaPath).Concat(scenes.Where(s => s != ArenaPath)).ToArray(); // automated net tests boot straight into the arena
            else if (AgniNetConfig.Has("-lobbyFirst")) scenes = scenes.Where(s => s.EndsWith("/Lobby.unity")).Concat(scenes.Where(s => !s.EndsWith("/Lobby.unity"))).ToArray(); // online (UGS) tests start in the Mahal lobby
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = Path.Combine(outDir, server ? "AgniRoyaleServer.x86_64" : "AgniRoyale.x86_64"), target = BuildTarget.StandaloneLinux64, targetGroup = BuildTargetGroup.Standalone, options = AgniNetConfig.Has("-dev") ? BuildOptions.Development : BuildOptions.None };
                        if (server)
            {
                // Dedicated Server subtarget needs Unity's "Linux Dedicated Server Build Support" module. Without it we ship the
                // normal player and run it with -batchmode -nographics -agniServer (same code path, no rendering).
                opts.subtarget = (int)StandaloneBuildSubtarget.Server;
            }
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SplashScreen.show = false; PlayerSettings.SplashScreen.showUnityLogo = false; PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.defaultScreenWidth = 1200; PlayerSettings.defaultScreenHeight = 540; PlayerSettings.resizableWindow = true;
            BuildReport r;
            try { r = BuildPipeline.BuildPlayer(opts); }
            catch (System.Exception e) { Debug.LogWarning("[AgniNetBuild] server subtarget failed (" + e.Message + ") — building the normal player for headless use"); opts.subtarget = (int)StandaloneBuildSubtarget.Player; r = BuildPipeline.BuildPlayer(opts); }
            if (server && r.summary.result != BuildResult.Succeeded) { Debug.LogWarning("[AgniNetBuild] server subtarget build failed — retry as normal player (run headless with -batchmode -nographics -agniServer)"); opts.subtarget = (int)StandaloneBuildSubtarget.Player; r = BuildPipeline.BuildPlayer(opts); }
            Debug.Log("[AgniNetBuild] " + (server ? "server" : "client") + " result=" + r.summary.result + " size=" + r.summary.totalSize + " out=" + opts.locationPathName + " subtarget=" + opts.subtarget);
            return r.summary.result == BuildResult.Succeeded;
        }
    }
}
