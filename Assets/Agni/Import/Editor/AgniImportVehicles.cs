// Agni Royale — IMPORT v1: the 7 Blender vehicles -> network prefabs (Resources/AgniNet/Vehicles, auto-registered by AgniNet).
//   Unity -batchmode -projectPath <p> -executeMethod Agni.Import.EditorTools.AgniImportVehicles.Batch
// Fixes the FBX's double axis bake on the pivot children, URP materials (body paint atlas / shared trim / lights), LODGroup 0-2,
// UCX hulls -> convex colliders, WheelColliders at the hub empties (spec table in AgniVehicle.cs = README_VEHICLES.md numbers),
// seats / exits / buoys / pivots wired into AgniVehicle, layer 9 (vehicles).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agni.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agni.Import.EditorTools
{
    public class AgniVehicleImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Agni/Import/Vehicles/")) return;
            var mi = (ModelImporter)assetImporter; mi.globalScale = 1f; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None; mi.importBlendShapes = false;
            mi.isReadable = true; mi.meshCompression = ModelImporterMeshCompression.Low; mi.addCollider = false; mi.importCameras = false; mi.importLights = false;
        }
        void OnPreprocessTexture()
        {
            if (!(assetPath.Contains("/Agni/Import/Vehicles/") || assetPath.Contains("/AgniImport/VehicleTex/") || assetPath.Contains("/AgniImport/VehicleSkins/"))) return;
            var ti = (TextureImporter)assetImporter; ti.maxTextureSize = 1024;
            if (assetPath.Contains("MetallicSmoothness")) ti.sRGBTexture = false;
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.maxTextureSize = 1024; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a);
        }
    }

    public static class AgniImportVehicles
    {
        const string SRC = "Assets/Agni/Import/Vehicles/", MATS = "Assets/Agni/Import/Vehicles/Mats/", OUT = "Assets/Agni/Import/Resources/AgniNet/Vehicles/";
        public static void Batch() { int c = 0; try { Run(); } catch (System.Exception e) { Debug.LogException(e); c = 1; } EditorApplication.Exit(c); }

        static Material Mat(string n, System.Action<Material> f)
        {
            var p = MATS + n + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
            f(m); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static Texture2D T(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

        public static void Run()
        {
            Directory.CreateDirectory(MATS); Directory.CreateDirectory(OUT);
            var trim = Mat("M_Vehicle_Trim", m => { m.SetTexture("_BaseMap", T(SRC + "Textures/T_Vehicles_Trim_Albedo.png")); m.SetTexture("_MetallicGlossMap", T(SRC + "Textures/T_Vehicles_Trim_MetallicSmoothness.png")); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0); });
            var lights = Mat("M_Vehicle_Lights", m => { var e = T(SRC + "Textures/T_Vehicles_Emissive.png"); m.SetTexture("_BaseMap", e); m.SetTexture("_EmissionMap", e); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white * 0.6f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; m.SetFloat("_Smoothness", 0.8f); });
            for (int k = 0; k < VSpec.All.Length; k++)
            {
                var s = VSpec.All[k]; var kind = (VKind)k; var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SRC + s.asset + ".fbx");
                if (fbx == null) { Debug.LogWarning("[AgniVehImport] missing " + s.asset); continue; }
                var variant0 = AgniVehicleSkins.Variants[k][0];
                var body = Mat("M_" + s.asset + "_Body", m => { m.SetTexture("_BaseMap", T("Assets/Agni/Import/Resources/AgniImport/VehicleTex/T_" + s.asset + "_Body_" + variant0 + ".png")); m.SetFloat("_Smoothness", 0.6f); m.SetFloat("_Metallic", 0.1f); });
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); go.name = s.asset;
                var root = go.transform;
                // 1) pivot children: the FBX bakes the axis twice on nested meshes (offset + 90 deg X); their origin is the hub/pivot -> identity
                foreach (Transform c in root)
                    if (c.name.StartsWith("Wheel_") || c.name.StartsWith("Steering_Pivot") || c.name.StartsWith("Motor_Pivot"))
                        foreach (Transform m in c) { m.localPosition = Vector3.zero; m.localRotation = Quaternion.identity; }
                // 2) materials
                var bodyRs = new List<Renderer>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials; bool hasBody = false;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var n = ms[i] != null ? ms[i].name : "";
                        if (n.Contains("_Body")) { ms[i] = body; hasBody = true; } else if (n.Contains("Light")) ms[i] = lights; else ms[i] = trim;
                    }
                    r.sharedMaterials = ms; if (hasBody) bodyRs.Add(r);
                    bool lod2 = r.name.Contains("_LOD2"); r.shadowCastingMode = lod2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                // 3) colliders: UCX hulls (convex), else a box round the body above the wheels
                int ucx = 0;
                foreach (var t in go.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (!t.name.StartsWith("UCX")) continue; var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                    if (mf != null && mf.sharedMesh != null) { var mc = t.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = true; ucx++; }
                    if (mr != null) Object.DestroyImmediate(mr); if (mf != null) Object.DestroyImmediate(mf);
                }
                var b0 = new Bounds(); bool f0 = true; foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.name.Contains("Body_LOD0")) { if (f0) { b0 = r.bounds; f0 = false; } else b0.Encapsulate(r.bounds); }
                if (ucx == 0) { var bc = go.AddComponent<BoxCollider>(); float lift = 0.35f; bc.center = b0.center + Vector3.up * lift * 0.5f; bc.size = new Vector3(b0.size.x * 0.9f, Mathf.Max(0.3f, b0.size.y - lift), b0.size.z * 0.95f); }
                // 4) LODs
                var lods = new List<LOD>(); float[] sh = { 0.25f, 0.08f, 0.015f };
                for (int li = 0; li < 3; li++) { var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r.name.Contains("_LOD" + li)).ToArray(); if (rs.Length > 0) lods.Add(new LOD(sh[li], rs)); }
                var lg = go.GetComponent<LODGroup>(); if (lg == null) lg = go.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
                // 5) physics + network
                var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(1, s.mass); rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                go.AddComponent<NetworkObject>();
                var nt = go.AddComponent<NetworkTransform>(); nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.Interpolate = true;
                var v = go.AddComponent<AgniVehicle>(); v.kind = kind;
                Transform Ch(string n) => root.Find(n);
                v.seats = new[] { "Seat_Driver", "Seat_P1", "Seat_P2", "Seat_P3" }.Select(Ch).Where(t => t != null).ToArray();
                v.exits = new[] { "Exit_Left", "Exit_Right" }.Select(Ch).Where(t => t != null).ToArray();
                v.buoys = root.Cast<Transform>().Where(t => t.name.StartsWith("Buoy_")).ToArray();
                v.steering = Ch("Steering_Pivot"); v.motorPivot = Ch("Motor_Pivot"); v.prop = Ch("Prop_Thrust"); v.com = Ch("CenterOfMass");
                v.bodyRenderers = bodyRs.ToArray(); v.lights = new Light[0];
                // 6) wheels: order FL FR RL RR (bike F R, rickshaw F RL RR) so the anti-roll pairs line up
                string[] order = { "Wheel_FL", "Wheel_FR", "Wheel_F", "Wheel_RL", "Wheel_RR", "Wheel_R" };
                var wv = new List<Transform>(); var wcs = new List<WheelCollider>(); var st = new List<bool>(); var dr = new List<bool>(); var rr = new List<bool>();
                foreach (var wn in order)
                {
                    var w = Ch(wn); if (w == null) continue;
                    var wr = w.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Contains("_LOD0")); float radius = wr != null ? Mathf.Max(wr.bounds.size.y, wr.bounds.size.z) * 0.5f : 0.35f;
                    var wcGo = new GameObject("WC_" + wn.Substring(6)); wcGo.transform.SetParent(root, false);
                    wcGo.transform.localPosition = root.InverseTransformPoint(w.position) + Vector3.up * s.susp * (1f - s.target);
                    var wc = wcGo.AddComponent<WheelCollider>(); wc.radius = radius; wc.mass = s.wheelMass; wc.suspensionDistance = s.susp; wc.forceAppPointDistance = s.forceApp; wc.wheelDampingRate = 0.25f;
                    var sp = wc.suspensionSpring; sp.spring = s.spring; sp.damper = s.damper; sp.targetPosition = s.target; wc.suspensionSpring = sp;
                    if (kind == VKind.Bike) { var sp2 = wc.suspensionSpring; wc.suspensionDistance = wn.EndsWith("F") ? 0.14f : 0.10f; }
                    var ff = wc.forwardFriction; ff.extremumSlip = 0.4f; ff.extremumValue = 1f; ff.asymptoteSlip = 0.8f; ff.asymptoteValue = 0.6f; ff.stiffness = 1f; wc.forwardFriction = ff;
                    var sf = wc.sidewaysFriction; sf.extremumSlip = 0.25f; sf.extremumValue = 1f; sf.asymptoteSlip = 0.5f; sf.asymptoteValue = 0.75f; sf.stiffness = 1f; wc.sidewaysFriction = sf;
                    bool front = wn.EndsWith("FL") || wn.EndsWith("FR") || wn.EndsWith("_F"), rear = !front;
                    wv.Add(w); wcs.Add(wc); st.Add(front); rr.Add(rear); dr.Add(s.fourWD || rear);
                }
                v.wheelVis = wv.ToArray(); v.wcs = wcs.ToArray(); v.steerW = st.ToArray(); v.driveW = dr.ToArray(); v.rearW = rr.ToArray();
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = AgniVehicle.VehicleLayer;
                Debug.Log("[AgniVehImport] " + s.asset + ": wheels " + wcs.Count + " (r " + string.Join("/", wcs.Select(w => w.radius.ToString("F2"))) + ") seats " + v.seats.Length + " exits " + v.exits.Length + " buoys " + v.buoys.Length + " ucx " + ucx + " lods " + lods.Count + " body renderers " + bodyRs.Count + " size " + b0.size.ToString("F2"));
                var pf = PrefabUtility.SaveAsPrefabAsset(go, OUT + s.asset + ".prefab"); Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
