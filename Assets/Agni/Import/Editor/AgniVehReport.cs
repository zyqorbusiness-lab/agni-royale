using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Agni.Import.EditorTools
{
    public static class AgniVehReport
    {
        public static void Batch()
        {
            foreach (var n in new[] { "ShereJeep", "PhatphatiBike", "ToofanBuggy", "ChhotaHaathiPickup", "AutoRickshaw", "DhoomMonsterTruck", "NaavSpeedboat" })
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Agni/Import/Vehicles/" + n + ".fbx");
                var go = (GameObject)Object.Instantiate(m);
                var s2 = n + " BODY " + go.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Body_LOD0")).Select(r => r.bounds.center.ToString("F2") + " s" + r.bounds.size.ToString("F2")).FirstOrDefault();
                foreach (Transform c in go.transform) if (c.name.StartsWith("Wheel_") || c.name.StartsWith("Steering") || c.name.StartsWith("Motor") || c.name.StartsWith("Seat") || c.name.StartsWith("Prop") || c.name.StartsWith("Tail"))
                { var r = c.GetComponentInChildren<Renderer>(); s2 += " | " + c.name + " p" + c.position.ToString("F2") + " r" + c.eulerAngles.ToString("F0") + (r ? " meshC" + r.bounds.center.ToString("F2") + " s" + r.bounds.size.ToString("F2") : ""); }
                Debug.Log("[VehRep2] " + s2);
                var s = n + ": rootRot " + go.transform.rotation.eulerAngles + " children: ";
                foreach (Transform c in go.transform) if (!c.name.StartsWith("UCX")) s += c.name + "@" + c.localPosition.ToString("F2") + "r" + c.localEulerAngles.ToString("F0") + " ";
                s += " UCX=" + go.transform.Cast<Transform>().Count(c => c.name.StartsWith("UCX"));
                var b0 = go.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Body_LOD0")).Select(r => r.bounds).FirstOrDefault();
                s += " bodyLOD0 bounds c" + b0.center.ToString("F2") + " s" + b0.size.ToString("F2");
                s += " mats: " + string.Join(",", go.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(x => x != null).Select(x => x.name).Distinct());
                var deep = go.GetComponentsInChildren<Transform>().Where(t => t.parent != go.transform && t != go.transform).Select(t => t.parent.name + "/" + t.name + "@" + t.localPosition.ToString("F2") + "r" + t.localEulerAngles.ToString("F0"));
                Debug.Log("[VehRep] " + s + "\n  deep: " + string.Join(" ", deep));
                Object.DestroyImmediate(go);
            }
            EditorApplication.Exit(0);
        }
    }
}
