using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Agni.Import.EditorTools
{
    public static class AgniEmoteReport
    {
        public static void Batch()
        {
            AssetDatabase.Refresh();
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Agni/Import/Resources/AgniImport/Emotes" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g); foreach (var c in AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                {
                    n++; var b = AnimationUtility.GetCurveBindings(c);
                    if (n <= 3) Debug.Log("[EmoteRep] " + p + " clip " + c.name + " legacy=" + c.legacy + " len=" + c.length.ToString("F2") + " bindings " + b.Length + " e.g. " + string.Join(" ; ", b.Where(x => x.path.Length > 0).Take(3).Select(x => x.path + ":" + x.propertyName)));
                    else Debug.Log("[EmoteRep] " + System.IO.Path.GetFileNameWithoutExtension(p) + " " + c.name + " len=" + c.length.ToString("F2") + " legacy=" + c.legacy);
                }
            }
            var body = Resources.LoadAll<AnimationClip>("AgniNet/Anims").FirstOrDefault(c => !c.name.StartsWith("__"));
            if (body) Debug.Log("[EmoteRep] body clip " + body.name + " e.g. " + string.Join(" ; ", AnimationUtility.GetCurveBindings(body).Where(x => x.path.Length > 0).Take(3).Select(x => x.path + ":" + x.propertyName)));
            Debug.Log("[EmoteRep] clips " + n);
            EditorApplication.Exit(0);
        }
    }
}
