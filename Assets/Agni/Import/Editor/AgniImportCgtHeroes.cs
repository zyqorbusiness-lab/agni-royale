// Agni Royale — CGTrader heroes v1: 7 royalty-free CGTrader characters as playable heroes.
//   Unity -batchmode -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportCgtHeroes.Batch
// Source: Assets/Agni/Import/HeroesCgt/<Name>/<Name>.fbx (skeleton + LOD0) + <Name>_LOD12.fbx (LOD1/LOD2 on the same skeleton) +
// <Name>_opaque.jpg (2K atlas) + <Name>_cut.png (alpha-tested hair/lash atlas), all made by Tools/cgtrader/prep_cgt_hero.py (Blender) from
// the author's files in zyqor_game/cgtrader/ (read-only). Credits: CREDITS.md (author + listing per hero).
// Each prefab: root (LODGroup + AgniRetarget) -> Proxy_Rig (Meera's mixamorig skeleton, transforms only; the shared clips and all procedural
// bone code run here) + Body (the hero's own rig and meshes, posed every frame through humanoid muscle space). Avatars are built here in T-pose.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agni.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import.EditorTools
{
    public class AgniCgtHeroImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Agni/Import/HeroesCgt/")) return;
            var mi = (ModelImporter)assetImporter; mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar; mi.importAnimation = false; mi.importBlendShapes = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importCameras = false; mi.importLights = false; mi.importVisibility = false;
            mi.optimizeGameObjects = false; mi.importNormals = ModelImporterNormals.Import; mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Agni/Import/HeroesCgt/")) return;
            var ti = (TextureImporter)assetImporter; bool cut = assetPath.Contains("_cut");
            ti.maxTextureSize = 2048; ti.alphaIsTransparency = cut; ti.alphaSource = cut ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.anisoLevel = 4; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.mipMapBias = -0.25f; ti.sRGBTexture = true;
            // 2K on HIGH/VERY HIGH; the tier's texture limit (AgniCore: NORMAL half, LOW quarter) gives the 1K / 512 fallback from the same file
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 2048; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
    }

    public static class AgniImportCgtHeroes
    {
        public const string SRC = "Assets/Agni/Import/HeroesCgt/", OUT = "Assets/Agni/Import/Resources/AgniImport/Heroes/";
        // folder name -> rig family (bone naming)
        public static readonly string[] Heroes = { "Pari", "Ishita", "Tara", "Rhea", "Naina", "Veer", "Aryan" };
        /// standing height in metres at match scale (the Blender prep scaled each model to this / AgniHeroes.MatchScale; re-measured here)
        static readonly Dictionary<string, float> Height = new Dictionary<string, float> { { "Pari", 1.66f }, { "Ishita", 1.65f }, { "Tara", 1.68f }, { "Rhea", 1.70f }, { "Naina", 1.72f }, { "Veer", 1.84f }, { "Aryan", 1.83f } };
        static readonly Dictionary<string, string> Rig = new Dictionary<string, string> { { "Pari", "vroid" }, { "Ishita", "mx" }, { "Tara", "mh" }, { "Rhea", "mh" }, { "Naina", "ue" }, { "Veer", "cc" }, { "Aryan", "mh" } };

        static string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        static string[] Phal = { "Proximal", "Intermediate", "Distal" };
        /// HumanTrait bone name -> transform name, per rig family
        public static Dictionary<string, string> Map(string fam)
        {
            var m = new Dictionary<string, string>();
            void Body(string hips, string spine, string chest, string upper, string neck, string head) { m["Hips"] = hips; m["Spine"] = spine; if (chest != null) m["Chest"] = chest; if (upper != null) m["UpperChest"] = upper; m["Neck"] = neck; m["Head"] = head; }
            foreach (var side in new[] { "Left", "Right" })
            {
                string L = side == "Left" ? "L" : "R", l = side == "Left" ? "l" : "r";
                switch (fam)
                {
                    case "mixamo": case "mx":
                        {
                            string p = fam == "mx" ? "mx_" : "mixamorig:";
                            m[side + "Shoulder"] = p + side + "Shoulder"; m[side + "UpperArm"] = p + side + "Arm"; m[side + "LowerArm"] = p + side + "ForeArm"; m[side + "Hand"] = p + side + "Hand";
                            m[side + "UpperLeg"] = p + side + "UpLeg"; m[side + "LowerLeg"] = p + side + "Leg"; m[side + "Foot"] = p + side + "Foot"; m[side + "Toes"] = p + side + "ToeBase";
                            string[] mf = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
                            for (int f = 0; f < 5; f++) for (int k = 0; k < 3; k++) m[side + " " + Fingers[f] + " " + Phal[k]] = p + side + "Hand" + mf[f] + (k + 1);
                            if (side == "Left") Body(p + "Hips", p + "Spine", p + "Spine1", p + "Spine2", p + "Neck", p + "Head");
                            break;
                        }
                    case "vroid":
                        m[side + "Shoulder"] = "J_Bip_" + L + "_Shoulder"; m[side + "UpperArm"] = "J_Bip_" + L + "_UpperArm"; m[side + "LowerArm"] = "J_Bip_" + L + "_LowerArm"; m[side + "Hand"] = "J_Bip_" + L + "_Hand";
                        m[side + "UpperLeg"] = "J_Bip_" + L + "_UpperLeg"; m[side + "LowerLeg"] = "J_Bip_" + L + "_LowerLeg"; m[side + "Foot"] = "J_Bip_" + L + "_Foot"; m[side + "Toes"] = "J_Bip_" + L + "_ToeBase";
                        for (int f = 0; f < 5; f++) for (int k = 0; k < 3; k++) m[side + " " + Fingers[f] + " " + Phal[k]] = "J_Bip_" + L + "_" + Fingers[f] + (k + 1);
                        if (side == "Left") Body("J_Bip_C_Hips", "J_Bip_C_Spine", "J_Bip_C_Chest", "J_Bip_C_UpperChest", "J_Bip_C_Neck", "J_Bip_C_Head");
                        break;
                    case "mh":
                        m[side + "Shoulder"] = "shoulder01_" + L; m[side + "UpperArm"] = "upperarm01_" + L; m[side + "LowerArm"] = "lowerarm01_" + L; m[side + "Hand"] = "wrist_" + L;
                        m[side + "UpperLeg"] = "upperleg01_" + L; m[side + "LowerLeg"] = "lowerleg01_" + L; m[side + "Foot"] = "foot_" + L;
                        for (int f = 0; f < 5; f++) for (int k = 0; k < 3; k++) m[side + " " + Fingers[f] + " " + Phal[k]] = "finger" + (f + 1) + "-" + (k + 1) + "_" + L;
                        if (side == "Left") Body("hips", "spine04", "spine03", "spine01", "neck01", "head");
                        break;
                    case "ue":
                        m[side + "Shoulder"] = "clavicle_" + l; m[side + "UpperArm"] = "upperarm_" + l; m[side + "LowerArm"] = "lowerarm_" + l; m[side + "Hand"] = "hand_" + l;
                        m[side + "UpperLeg"] = "thigh_" + l; m[side + "LowerLeg"] = "calf_" + l; m[side + "Foot"] = "foot_" + l; m[side + "Toes"] = "ball_" + l;
                        string[] uf = { "thumb", "index", "middle", "ring", "pinky" };
                        for (int f = 0; f < 5; f++) for (int k = 0; k < 3; k++) m[side + " " + Fingers[f] + " " + Phal[k]] = uf[f] + "_0" + (k + 1) + "_" + l;
                        if (side == "Left") Body("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head");
                        break;
                    case "cc":
                        m[side + "Shoulder"] = side + " shoulder"; m[side + "UpperArm"] = side + " arm"; m[side + "LowerArm"] = side + " elbow"; m[side + "Hand"] = side + " wrist";
                        m[side + "UpperLeg"] = side + " leg"; m[side + "LowerLeg"] = side + " knee"; m[side + "Foot"] = side + " ankle";
                        m[side + " Thumb Proximal"] = "Thumb0_" + L; m[side + " Thumb Intermediate"] = "Thumb1_" + L;
                        m[side + " Index Proximal"] = "Index_01_" + L; m[side + " Index Intermediate"] = "IndexFinger2_" + L;
                        m[side + " Middle Proximal"] = "MiddleFinger1_" + L; m[side + " Middle Intermediate"] = "MiddleFinger2_" + L;
                        m[side + " Ring Proximal"] = "RingFinger1_" + L; m[side + " Ring Intermediate"] = "RingFinger2_" + L;
                        m[side + " Little Proximal"] = "LittleFinger1_" + L; m[side + " Little Intermediate"] = "LittleFinger2_" + L;
                        if (side == "Left") Body("Hips", "Spine", null, "Chest", "Neck", "Head");
                        break;
                }
            }
            // cc rig: its two-bone fingers splay under the muscle copy; they stay at rest (relaxed hand) instead
            return m;
        }

        static Transform Find(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        static void Align(Dictionary<string, Transform> hb, string a, string b, Vector3 dir)
        {
            if (!hb.TryGetValue(a, out var ta) || !hb.TryGetValue(b, out var tb) || ta == null || tb == null) return;
            var d = tb.position - ta.position; if (d.sqrMagnitude < 1e-10f) return;
            ta.rotation = Quaternion.FromToRotation(d.normalized, dir) * ta.rotation;
        }
        /// put a skeleton into Unity's reference T-pose (arms straight out along the character's X, legs straight down, fingers along the arm)
        public static void TPose(Transform root, Dictionary<string, Transform> hb)
        {
            // the character's own right (from its upper arms), so a model facing -Z is T-posed correctly too
            Vector3 D = root.TransformDirection(Vector3.down), R = root.TransformDirection(Vector3.right);
            if (hb.TryGetValue("LeftUpperArm", out var la) && hb.TryGetValue("RightUpperArm", out var ra)) { var r = Vector3.ProjectOnPlane(ra.position - la.position, D); if (r.sqrMagnitude > 1e-8f) R = r.normalized; }
            foreach (var side in new[] { "Left", "Right" })
            {
                var o = side == "Left" ? -R : R;
                Align(hb, side + "UpperArm", side + "LowerArm", o); Align(hb, side + "LowerArm", side + "Hand", o);
                if (hb.ContainsKey(side + " Middle Proximal")) Align(hb, side + "Hand", side + " Middle Proximal", o);
                for (int f = 1; f < 5; f++) { Align(hb, side + " " + Fingers[f] + " Proximal", side + " " + Fingers[f] + " Intermediate", o); Align(hb, side + " " + Fingers[f] + " Intermediate", side + " " + Fingers[f] + " Distal", o); }
                Align(hb, side + "UpperLeg", side + "LowerLeg", D); Align(hb, side + "LowerLeg", side + "Foot", D);
            }
        }
        /// build + save a humanoid Avatar for the hierarchy under root from the bone map (T-posed for the build, pose restored after)
        static bool IsFinger(string k) => k.Contains(" Proximal") || k.Contains(" Intermediate") || k.Contains(" Distal");
        /// fingersInAvatar=false: fingers still steer the T-pose (hand direction) but stay unmapped, so they keep their rest shape
        public static Avatar BuildAvatar(Transform root, Dictionary<string, string> map, string path, out string report, bool fingersInAvatar = true)
        {
            var hb = new Dictionary<string, Transform>(); var missing = new List<string>();
            foreach (var kv in map) { var t = Find(root, kv.Value); if (t != null) hb[kv.Key] = t; else missing.Add(kv.Key + "=" + kv.Value); }
            var all = root.GetComponentsInChildren<Transform>(true); var saved = all.Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
            TPose(root, hb);
            var desc = new HumanDescription
            {
                human = hb.Where(kv => fingersInAvatar || !IsFinger(kv.Key)).Select(kv => new HumanBone { boneName = kv.Value.name, humanName = kv.Key, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = all.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f, armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
            };
            var av = AvatarBuilder.BuildHumanAvatar(root.gameObject, desc); av.name = Path.GetFileNameWithoutExtension(path);
            foreach (var s in saved) { s.t.localPosition = s.Item2; s.t.localRotation = s.Item3; s.t.localScale = s.Item4; }
            report = "valid=" + av.isValid + " human=" + av.isHuman + " mapped=" + hb.Count + (missing.Count > 0 ? " missing=" + string.Join(",", missing) : "");
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(av, path); return av;
        }

        static Material Mat(string p, Shader sh, System.Action<Material> f)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); } else m.shader = sh;
            f(m); EditorUtility.SetDirty(m); return m;
        }

        public static void Batch() { int c = 0; try { Run(); } catch (System.Exception e) { Debug.LogException(e); c = 1; } EditorApplication.Exit(c); }

        static GameObject ProxyTemplate()
        {
            var meera = AssetDatabase.LoadAssetAtPath<GameObject>(OUT + "Meera.prefab");
            Transform rig = null; foreach (Transform c in meera.transform) if (c.name.EndsWith("_Rig")) rig = c;
            var go = Object.Instantiate(rig.gameObject); go.name = "Proxy_Rig";
            foreach (var comp in go.GetComponentsInChildren<Component>(true)) if (!(comp is Transform)) Object.DestroyImmediate(comp);
            go.transform.localPosition = rig.localPosition; go.transform.localRotation = rig.localRotation; go.transform.localScale = rig.localScale;
            return go;
        }

        public static void Run()
        {
            Directory.CreateDirectory(OUT);
            ShaderUtil.allowAsyncCompilation = false;
            var toon = Shader.Find("AgniRoyale/HeroToon"); var toonCut = Shader.Find("AgniRoyale/HeroToonCutout");
            Debug.Log("[AgniCgtImport] shaders toon=" + (toon != null) + " cut=" + (toonCut != null));
            var proxyT = ProxyTemplate();
            foreach (var name in Heroes)
            {
                var dir = SRC + name + "/"; var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".fbx"); var fbx12 = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + "_LOD12.fbx");
                if (fbx == null) { Debug.LogWarning("[AgniCgtImport] missing " + name); continue; }
                var root = new GameObject(name);
                var proxy = Object.Instantiate(proxyT, root.transform, false); proxy.name = "Proxy_Rig"; proxy.transform.SetSiblingIndex(0);
                // source avatar: rooted at the (identity) hero root, so the proxy's Blender axis rotation sits inside the avatar like on any FBX import
                var proxyAvatar = BuildAvatar(root.transform, Map("mixamo"), dir + name + "_ProxyAvatar.asset", out var prep);
                var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx); PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                body.name = "Body"; body.transform.SetParent(root.transform, false); body.transform.SetSiblingIndex(1);
                var skel = body.transform.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("_Skel"));
                var lods = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
                foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)) lods[0].Add(r);
                if (fbx12 != null)
                {   // LOD1/LOD2 live in their own FBX (size limit of the repo upload): move them onto LOD0's skeleton by bone name
                    var b12 = (GameObject)PrefabUtility.InstantiatePrefab(fbx12); PrefabUtility.UnpackPrefabInstance(b12, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    var byName = body.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                    foreach (var r in b12.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        r.bones = r.bones.Select(b => b != null && byName.TryGetValue(b.name, out var t) ? t : null).ToArray();
                        if (r.rootBone != null && byName.TryGetValue(r.rootBone.name, out var rb)) r.rootBone = rb;
                        var lp = r.transform.localPosition; var lr = r.transform.localRotation; var ls = r.transform.localScale;
                        r.transform.SetParent(body.transform, false); r.transform.localPosition = lp; r.transform.localRotation = lr; r.transform.localScale = ls;
                        lods[r.name.EndsWith("_LOD2") ? 2 : 1].Add(r);
                    }
                    Object.DestroyImmediate(b12);
                }
                var map = Map(Rig[name]);
                // facing: the hero must look down +Z like the proxy (right hand on +X)
                var lu = Find(body.transform, map["LeftUpperArm"]); var ru = Find(body.transform, map["RightUpperArm"]);
                if (lu != null && ru != null) { var fwd = Vector3.Cross((ru.position - lu.position).normalized, Vector3.up); if (fwd.z < 0) { foreach (Transform ch in body.transform.Cast<Transform>().ToArray()) ch.RotateAround(Vector3.zero, Vector3.up, 180); Debug.Log("[AgniCgtImport] " + name + " turned 180 to face +Z"); } }
                // height check on the skinned rest pose (an FBX can carry a scale inside its armature): scale the skeleton so the hero stands at its height
                float restH = 0, sole = 0;
                {
                    var s0 = (SkinnedMeshRenderer)lods[0][0]; var bm = new Mesh(); s0.BakeMesh(bm, true); var M = s0.transform.localToWorldMatrix;
                    var ys = bm.vertices.Select(v => M.MultiplyPoint3x4(v).y).ToArray(); restH = ys.Max() - ys.Min(); sole = ys.Min(); Object.DestroyImmediate(bm);
                    float want = Height[name] / AgniHeroes.MatchScale;
                    if (skel != null && restH > 0.1f && Mathf.Abs(want / restH - 1) > 0.01f)
                    {
                        float k = want / restH; skel.localScale *= k; skel.localPosition *= k;
                        Debug.Log("[AgniCgtImport] " + name + " rest height " + restH.ToString("F3") + " -> " + want.ToString("F3") + " (skeleton x" + k.ToString("F3") + ")"); sole *= k;
                    }
                }
                // cc rig (Veer): its two-bone fingers splay under the muscle copy, so they keep their relaxed rest shape
                var av = BuildAvatar(body.transform, map, dir + name + "_Avatar.asset", out var rep, Rig[name] != "cc");
                // materials: one 2K atlas (toon + outline) + an alpha-tested atlas for hair cards / lashes (toon, no outline)
                var opaqueT = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + name + "_opaque.jpg"); var cutT = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + name + "_cut.png");
                Directory.CreateDirectory(dir + "Mats");
                var mo = Mat(dir + "Mats/M_" + name + "_Opaque.mat", toon, m =>
                {
                    m.SetTexture("_BaseMap", opaqueT); m.SetColor("_BaseColor", Color.white); m.SetColor("_ShadowTint", new Color(0.90f, 0.82f, 0.86f)); m.SetColor("_CoreTint", new Color(0.74f, 0.64f, 0.76f));
                    m.SetFloat("_Softness", 0.10f); m.SetFloat("_Step", -0.12f); m.SetFloat("_CoreStep", -0.6f); m.SetFloat("_NormalFlatten", 0.45f); m.SetFloat("_ShadowStrength", 0.45f); m.SetFloat("_Ambient", 0.36f);
                    m.SetFloat("_OutlineWidth", 0.0017f); m.SetFloat("_OutlineAlbedo", 0.62f); m.SetFloat("_RimStrength", 0.2f); m.SetFloat("_RimWidth", 0.72f); m.SetFloat("_SpecStrength", 0f); m.SetFloat("_VColWidth", 0); m.SetFloat("_Cull", 0);
                });
                var mc = cutT == null ? null : Mat(dir + "Mats/M_" + name + "_Cutout.mat", toonCut, m =>
                {
                    m.SetTexture("_BaseMap", cutT); m.SetColor("_BaseColor", Color.white); m.SetColor("_ShadowTint", new Color(0.86f, 0.82f, 0.94f)); m.SetColor("_CoreTint", new Color(0.64f, 0.60f, 0.80f));
                    m.SetFloat("_Softness", 0.12f); m.SetFloat("_Step", 0.0f); m.SetFloat("_CoreStep", -0.45f); m.SetFloat("_ShadowStrength", 0.5f); m.SetFloat("_Ambient", 0.36f); m.SetFloat("_RimStrength", 0.3f); m.SetFloat("_SpecStrength", 0.06f); m.SetFloat("_SpecPower", 24f);
                    m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_AlphaClip", 1); m.renderQueue = (int)RenderQueue.AlphaTest;
                });
                foreach (var list in lods) foreach (var r in list)
                {
                    var smr = (SkinnedMeshRenderer)r; int n = smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1;
                    // submesh order from Blender: [Opaque, Cutout] (a hero with no alpha parts has only the opaque one)
                    var ms = new List<Material> { mo }; if (n > 1) ms.Add(mc != null ? mc : mo);
                    smr.sharedMaterials = ms.Take(n).ToArray(); smr.updateWhenOffscreen = false; smr.skinnedMotionVectors = false; smr.quality = SkinQuality.Bone4;
                    smr.shadowCastingMode = r.name.EndsWith("_LOD2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                var lg = root.AddComponent<LODGroup>(); float[] sh = { 0.22f, 0.07f, 0.008f };
                lg.SetLODs(Enumerable.Range(0, 3).Where(i => lods[i].Count > 0).Select(i => new LOD(sh[i], lods[i].ToArray())).ToArray()); lg.RecalculateBounds();
                var rt = root.AddComponent<AgniRetarget>(); rt.srcRoot = root.transform; rt.srcAvatar = proxyAvatar; rt.dstRoot = body.transform; rt.dstAvatar = av;
                rt.renderers = lods.SelectMany(x => x).ToArray();
                foreach (var kv in map) { if (Rig[name] == "cc" && IsFinger(kv.Key)) continue; int idx = System.Array.IndexOf(HumanTrait.BoneName, kv.Key); if (idx < 0) continue; int hbIdx = idx; if (hbIdx < 0 || hbIdx >= rt.dstBones.Length) continue; rt.dstBones[hbIdx] = Find(body.transform, kv.Value); }
                {   // ankle height above the sole, in hero-root units, for proxy and body (feet are planted after each copy)
                    var pl = proxy.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:LeftFoot");
                    var mi2 = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OUT + "Meera.prefab")); var ms2 = mi2.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.EndsWith("_LOD0"));
                    var bm2 = new Mesh(); ms2.BakeMesh(bm2, true); var M2 = ms2.transform.localToWorldMatrix; float msole = bm2.vertices.Min(v => M2.MultiplyPoint3x4(v).y);
                    var mfoot = mi2.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:LeftFoot").position.y; Object.DestroyImmediate(mi2); Object.DestroyImmediate(bm2);
                    rt.srcAnkle = mfoot - msole; rt.dstAnkle = rt.Bone(HumanBodyBones.LeftFoot).position.y - sole;
                }
                // test: the shared clips must move the visible body (Rifle_Idle frame retargeted, hand height compared)
                float h = lods[0].Count > 0 ? lods[0][0].bounds.size.y : 0;
                PrefabUtility.SaveAsPrefabAsset(root, OUT + name + ".prefab");
                Debug.Log("[AgniCgtImport] " + name + ": proxy avatar " + prep + " | avatar " + rep + " lods " + lods[0].Count + "/" + lods[1].Count + "/" + lods[2].Count + " height " + h.ToString("F2") + " tris " + string.Join("/", lods.Select(l => l.Sum(r => ((SkinnedMeshRenderer)r).sharedMesh.triangles.Length / 3))));
                Object.DestroyImmediate(root);
            }
            Object.DestroyImmediate(proxyT);
            AssetDatabase.SaveAssets();
        }
    }

    public static class AgniCgtProof
    {
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
        static AnimationClip Clip(string path) { foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c; return null; }
        static void Pose(GameObject h, AnimationClip c, float t)
        {
            Transform rig = null; foreach (Transform ch in h.transform) if (ch.name.EndsWith("_Rig")) { rig = ch; break; }
            if (rig != null && c != null) c.SampleAnimation(rig.gameObject, t);
            Debug.Log("[AgniCgtProof] pose " + h.name + " " + (c ? c.name + " legacy=" + c.legacy + " len=" + c.length : "null") + " t=" + t);
            var rt = h.GetComponent<AgniRetarget>(); if (rt != null) { rt.always = true; rt.Apply(); }
            var lg = h.GetComponent<LODGroup>(); if (lg) lg.ForceLOD(0);
        }
        /// -proofOut <dir> [-cgtOnly Name|Name]: lineup of Meera + the CGTrader heroes, then per hero: idle front, run 3/4, face
        public static void Lineup()
        {
            int code = 0;
            try
            {
                var dir = Arg("-proofOut", "/tmp/agni_cgt_proof"); Directory.CreateDirectory(dir);
                AgniImportProof.Stage(true); var cam = AgniImportProof.cam;
                var idle = Clip("Assets/Agni/Net/Resources/AgniNet/Anims/Anim_Rifle_Idle.fbx"); var run = Clip("Assets/Agni/Net/Resources/AgniNet/Anims/Anim_Run_Fwd_InPlace.fbx");
                var pose = Clip("Assets/Agni/Import/Resources/AgniImport/Emotes/" + Arg("-cgtEmote", "Emote_Golden_Pose") + ".fbx");
                var names = new List<string> { "Meera" }; names.AddRange(AgniImportCgtHeroes.Heroes.Where(n => File.Exists(AgniImportCgtHeroes.OUT + n + ".prefab")));
                var gos = new List<GameObject>(); float sp = 0.95f, x0 = -(names.Count - 1) * sp * 0.5f;
                for (int i = 0; i < names.Count; i++)
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(AgniImportCgtHeroes.OUT + names[i] + ".prefab");
                    // held like the match view holds it: a "View" parent placed in the world, the hero as its child at match scale
                    var holder = new GameObject("View_" + names[i]); holder.transform.position = new Vector3(x0 + i * sp, 0, 0); holder.transform.rotation = Quaternion.Euler(0, 180, 0);
                    var g = (GameObject)Object.Instantiate(pf, holder.transform); g.name = names[i]; g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
                    g.transform.localScale = Vector3.one * AgniHeroes.MatchScale; gos.Add(g); Pose(g, idle, 0.4f);
                    var smr = g.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.name.EndsWith("_LOD0")); float top = 0, bot = 0;
                    if (smr != null) { var bm = new Mesh(); smr.BakeMesh(bm, true); var vs = bm.vertices; var M = smr.transform.localToWorldMatrix; top = vs.Max(v => M.MultiplyPoint3x4(v).y); bot = vs.Min(v => M.MultiplyPoint3x4(v).y); }
                    var rtg = g.GetComponent<AgniRetarget>(); string bones = "";
                    if (rtg != null) foreach (var hb in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot, HumanBodyBones.LeftHand }) { var bt = rtg.Bone(hb); bones += " " + hb + "=" + (bt ? bt.position.ToString("F2") : "-"); }
                    var ph = g.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "mixamorig:Head"); var pf2 = g.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "mixamorig:LeftFoot");
                    Debug.Log("[AgniCgtProof] " + names[i] + " mesh top " + top.ToString("F3") + " bottom " + bot.ToString("F3") + bones + " proxyHead=" + (ph ? ph.position.ToString("F2") : "-") + " proxyFoot=" + (pf2 ? pf2.position.ToString("F2") : "-") + " skelScale=" + (smr && smr.rootBone ? smr.rootBone.lossyScale.ToString("F3") : "-"));
                }
                float half = (names.Count - 1) * sp * 0.5f + 0.6f;
                AgniImportProof.Shot(dir, "lineup_idle", new Vector3(0, 1.05f, -half / Mathf.Tan(15 * Mathf.Deg2Rad) * 0.62f), new Vector3(0, 0.88f, 0), 30, 1920, 1080);
                for (int i = 0; i < gos.Count; i++) Pose(gos[i], pose, pose != null ? pose.length * 0.5f : 0);
                AgniImportProof.Shot(dir, "lineup_pose", new Vector3(0, 1.05f, -half / Mathf.Tan(15 * Mathf.Deg2Rad) * 0.62f), new Vector3(0, 0.88f, 0), 30, 1920, 1080);
                var only = Arg("-cgtOnly", "");
                for (int i = 0; i < gos.Count; i++)
                {
                    if (only != "" && !only.Split('|').Contains(names[i])) continue;
                    for (int j = 0; j < gos.Count; j++) gos[j].transform.parent.gameObject.SetActive(j == i);
                    var g = gos[i]; var p = g.transform.position;
                    Pose(g, idle, 0.4f);
                    AgniImportProof.Shot(dir, names[i] + "_front", p + new Vector3(0, 0.95f, -3.6f), p + new Vector3(0, 0.82f, 0), 32, 1080, 1350);
                    var head = g.GetComponent<AgniRetarget>() ? g.GetComponent<AgniRetarget>().Bone(HumanBodyBones.Head) : g.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head"));
                    var hp = head != null ? head.position + Vector3.up * 0.07f : p + Vector3.up * 1.6f;
                    AgniImportProof.Shot(dir, names[i] + "_face", hp + new Vector3(-0.25f, 0.03f, -0.75f), hp, 30, 1080, 1350);
                    Pose(g, run, run != null ? run.length * 0.3f : 0);
                    AgniImportProof.Shot(dir, names[i] + "_run", p + new Vector3(-2.2f, 1.0f, -2.6f), p + new Vector3(0, 0.85f, 0), 34, 1080, 1350);
                    Pose(g, idle, 0.4f);
                    AgniImportProof.Shot(dir, names[i] + "_back", p + new Vector3(1.4f, 1.0f, 3.2f), p + new Vector3(0, 0.85f, 0), 34, 1080, 1350);
                }
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
