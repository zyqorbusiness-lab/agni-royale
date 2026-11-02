// Agni Royale — CGTrader heroes v1: humanoid pose transfer from the shared Meera skeleton to a hero with its own rig.
// The hero prefab carries "Proxy_Rig" (a transform-only copy of Meera's mixamorig skeleton, FIRST child so every Mixamo-name
// lookup finds it) and "Body" (the hero's own FBX: its skeleton + LOD meshes). The Animation component, AgniIdle and every
// procedural bone edit (aim pose, spine pitch, sit pose, throw) work on Proxy_Rig exactly as on the 10 Mixamo heroes;
// this component runs LAST in LateUpdate and copies the final pose to Body through Unity's humanoid muscle space
// (HumanPoseHandler, both avatars built in T-pose by Agni.Import.EditorTools.AgniImportCgtHeroes).
using UnityEngine;

namespace Agni.Net
{
    [DefaultExecutionOrder(10000)]
    public class AgniRetarget : MonoBehaviour
    {
        public Transform srcRoot; public Avatar srcAvatar;
        public Transform dstRoot; public Avatar dstAvatar;
        public Transform[] dstBones = new Transform[(int)HumanBodyBones.LastBone];
        public Renderer[] renderers;
        /// ankle-bone height above the sole in rest pose (hero-root units) for the proxy and the body: feet are planted after each copy
        public float srcAnkle, dstAnkle; Transform sL, sR; Vector3 bodyRest; bool bodyRestSet;
        /// lobby / proof: retarget even when no camera sees the hero
        public bool always; public static bool debugLog;
        /// fired right after the pose is copied (the match view re-seats the held gun on the visible hand)
        public System.Action afterApply;
        HumanPoseHandler src, dst; HumanPose pose; bool failed;
        public Transform Bone(HumanBodyBones b) { int i = (int)b; return i >= 0 && i < dstBones.Length ? dstBones[i] : null; }
        bool Ready()
        {
            if (src != null && dst != null) return true; if (failed) return false;
            if (srcAvatar == null || dstAvatar == null || !srcAvatar.isValid || !dstAvatar.isValid || srcRoot == null || dstRoot == null) { failed = true; Debug.LogWarning("[AgniRetarget] not set up on " + name); return false; }
            src = new HumanPoseHandler(srcAvatar, srcRoot); dst = new HumanPoseHandler(dstAvatar, dstRoot); pose = new HumanPose(); return true;
        }
        static int camFrame = -1; static Vector3 camPos;
        bool Visible()
        {
            if (always || renderers == null || renderers.Length == 0) return true;
            foreach (var r in renderers) if (r != null && r.isVisible) return true;
            // isVisible lags a frame and stays false under manually rendered cameras (shots / replays): near heroes always pose,
            // far unseen ones refresh every 8th frame so they never stand in bind pose when a camera swings round
            if (camFrame != Time.frameCount) { camFrame = Time.frameCount; var c = Camera.main; if (c == null && Camera.allCamerasCount > 0) c = Camera.allCameras[0]; camPos = c != null ? c.transform.position : Vector3.zero; }
            if ((transform.position - camPos).sqrMagnitude < 45f * 45f) return true;
            return ((Time.frameCount + GetInstanceID()) & 7) == 0;
        }
        void LateUpdate() { if (Visible()) Apply(); }
        public void Apply()
        {
            if (!Ready()) return;
            if (!bodyRestSet) { bodyRest = dstRoot.localPosition; bodyRestSet = true; }
            dstRoot.localPosition = bodyRest;
            // the muscle copy is only exact when the hero root carries no local transform of its own (lobby: 172 deg + 0.86 scale under
            // the stage; match: identity under View), so the root's local transform is lifted off for the copy and put back after
            var t = transform; Vector3 lp = t.localPosition, ls = t.localScale; Quaternion lr = t.localRotation;
            bool lift = lp != Vector3.zero || lr != Quaternion.identity || ls != Vector3.one;
            if (lift) { t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; }
            src.GetHumanPose(ref pose); dst.SetHumanPose(ref pose);
            // plant the feet: the body's lower ankle sits as high above its sole as the proxy's lower ankle above the proxy's sole
            var dl = Bone(HumanBodyBones.LeftFoot); var dr = Bone(HumanBodyBones.RightFoot);
            if (sL == null) { foreach (var x in srcRoot.GetComponentsInChildren<Transform>(true)) { if (x.name == "mixamorig:LeftFoot") sL = x; else if (x.name == "mixamorig:RightFoot") sR = x; } }
            if (dl != null && dr != null && sL != null && sR != null)
            {
                float k = transform.lossyScale.y;
                float sy = Mathf.Min(sL.position.y, sR.position.y) - srcAnkle * k, dy = Mathf.Min(dl.position.y, dr.position.y) - dstAnkle * k;
                dstRoot.localPosition = bodyRest + transform.InverseTransformVector(new Vector3(0, sy - dy, 0));
            }
            if (lift) { t.localPosition = lp; t.localRotation = lr; t.localScale = ls; }
            afterApply?.Invoke();
            if (debugLog && (Time.frameCount % 60) == 0) { var hp = Bone(HumanBodyBones.Hips); Debug.Log("[AgniRetarget] " + name + " root " + transform.position + " body " + dstRoot.position + " hips " + (hp ? hp.position.ToString() : "-") + " srcHips " + pose.bodyPosition + " lossy " + transform.lossyScale + " parent " + (transform.parent ? transform.parent.name : "-")); }
        }
        void OnDestroy() { src?.Dispose(); dst?.Dispose(); src = dst = null; }
    }
}
