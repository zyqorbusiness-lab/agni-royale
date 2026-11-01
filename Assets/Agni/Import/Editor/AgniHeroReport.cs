using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Agni.Import.EditorTools
{
    public static class AgniHeroReport
    {
        static string Path(Transform t, Transform root) { var s = t.name; while (t.parent != null && t.parent != root) { t = t.parent; s = t.name + "/" + s; } return s; }
        public static void Batch()
        {
            foreach (var p in new[] { "Assets/Agni/Import/Heroes/Meera/Meera_HairV3.fbx", "Assets/Agni/Import/Heroes/Arjun/Arjun_HairV3.fbx", "Assets/Agni/Characters/Resources/AgniChars/Meera.fbx" })
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (m == null) { Debug.Log("[HeroRep] missing " + p); continue; }
                var go = (GameObject)Object.Instantiate(m); var root = go.transform;
                var s = "[HeroRep] " + p + " top: " + string.Join(",", root.Cast<Transform>().Select(c => c.name + "@" + c.localPosition.ToString("F2") + "r" + c.localEulerAngles.ToString("F0") + "s" + c.localScale.ToString("F2")));
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var smr = r as SkinnedMeshRenderer; s += "\n  R " + r.name + " mats[" + string.Join(",", r.sharedMaterials.Select(x => x ? x.name : "null")) + "]";
                    if (smr) s += " bones=" + smr.bones.Length + " root=" + (smr.rootBone ? smr.rootBone.name : "-") + " bs=" + (smr.sharedMesh ? smr.sharedMesh.blendShapeCount : 0) + " b=" + smr.bounds.size.ToString("F2") + " c=" + smr.bounds.center.ToString("F2") + " cols=" + (smr.sharedMesh && smr.sharedMesh.colors.Length > 0);
                }
                var hips = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Hips")); if (hips) s += "\n  hips path " + Path(hips, root);
                var head = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Head") && !t.name.Contains("Top")); if (head) s += " | head path " + Path(head, root) + " y=" + head.position.y.ToString("F2");
                s += "\n  jiggle " + go.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("jiggle")) + " eyes " + go.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Eye_"));
                var anim = go.GetComponentInChildren<Animation>(); var an = go.GetComponentInChildren<Animator>(); s += " animation=" + (anim != null) + " animator=" + (an != null) + (an && an.avatar ? " avatar " + an.avatar.isHuman : "");
                Debug.Log(s); Object.DestroyImmediate(go);
            }
            var clip = Resources.LoadAll<AnimationClip>("AgniNet/Anims").FirstOrDefault(c => !c.name.StartsWith("__"));
            if (clip) { var b = AnimationUtility.GetCurveBindings(clip); Debug.Log("[HeroRep] clip " + clip.name + " legacy=" + clip.legacy + " bindings " + b.Length + " e.g. " + string.Join(" ; ", b.Take(4).Select(x => x.path + ":" + x.propertyName))); }
            var mi = (ModelImporter)AssetImporter.GetAtPath("Assets/Agni/Characters/Resources/AgniChars/Meera.fbx"); Debug.Log("[HeroRep] old Meera importer anim=" + mi.animationType + " bs=" + mi.importBlendShapes);
            EditorApplication.Exit(0);
        }
    }
}
