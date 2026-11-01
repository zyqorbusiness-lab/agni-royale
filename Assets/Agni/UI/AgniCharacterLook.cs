// Agni Royale — lobby character look + procedural idle for the anime roster (characters_anime/*: Mixamo-named rig, A-pose).
using System.Collections.Generic;
using UnityEngine;

namespace Agni.UI
{
    public static class AgniCharacterLook
    {
        /// Toon materials (AgniRoyale/AnimeToonOutline, Resources/AgniChars) + LOD by graphics level (LOW=LOD2, NORMAL=LOD1, HIGH+=LOD0).
        public static void Setup(GameObject ch, int quality)
        {
            string nm = ch.name.Replace("(Clone)", "").Replace("Character", "");
            var toon = Shader.Find("AgniRoyale/AnimeToonOutline");
            bool useToon = toon != null && toon.isSupported;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            string baseName = "Meera";
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true))
            {
                string rn = r.name; int lod = rn.EndsWith("_LOD2") ? 2 : rn.EndsWith("_LOD1") ? 1 : 0;
                int want = quality <= 0 ? 2 : quality == 1 ? 1 : 0;
                if (ch.GetComponentInChildren<LODGroup>() == null) r.enabled = lod == want;
                int b = rn.IndexOf("_LOD"); if (b > 0) baseName = rn.Substring(0, b);
                var mats = r.sharedMaterials; var outM = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name.ToLower() : "";
                    string slot = mn.Contains("skin") ? "skin" : mn.Contains("hair") ? "hair" : mn.Contains("cloth") ? "clothes" : null;
                    if (slot == null) { outM[i] = mats[i]; continue; }
                    var tex = Resources.Load<Texture2D>("AgniChars/" + baseName + "_" + slot);
                    var m = new Material(useToon ? toon : lit); m.name = "M_" + baseName + "_" + slot;
                    if (tex != null) m.SetTexture("_BaseMap", tex);
                    if (useToon)
                    {
                        m.SetColor("_ShadowTint", slot == "skin" ? new Color(0.96f, 0.74f, 0.66f) : slot == "hair" ? new Color(0.72f, 0.7f, 0.86f) : new Color(0.62f, 0.58f, 0.74f));
                        m.SetFloat("_OutlineWidth", quality >= 2 ? 0.0042f : quality == 1 ? 0.003f : 0f);
                    }
                    else m.SetFloat("_Smoothness", slot == "hair" ? 0.5f : 0.2f);
                    outM[i] = m;
                }
                r.sharedMaterials = outM;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Debug.Log("[AgniLobby] character " + baseName + " toon=" + useToon);
        }
    }

    /// Procedural lobby idle on a Mixamo-named rig: arms relaxed out of the A-pose, breathing, weight shift, slow head look.
    public class AgniIdle : MonoBehaviour
    {
        Transform hips, spine, spine1, spine2, neck, head, lArm, rArm, lFore, rFore;
        Quaternion qHips, qSp, qSp1, qSp2, qNeck, qHead, qL, qR, qLF, qRF; Vector3 pHips;
        float armSignL = 1, armSignR = -1;
        static Transform F(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n || t.name.EndsWith(":" + n) || t.name.EndsWith("_" + n)) return t; return null; }
        void Start()
        {
            hips = F(transform, "Hips"); spine = F(transform, "Spine"); spine1 = F(transform, "Spine1"); spine2 = F(transform, "Spine2");
            neck = F(transform, "Neck"); head = F(transform, "Head"); lArm = F(transform, "LeftArm"); rArm = F(transform, "RightArm"); lFore = F(transform, "LeftForeArm"); rFore = F(transform, "RightForeArm");
            if (hips != null) { qHips = hips.localRotation; pHips = hips.localPosition; }
            if (spine != null) qSp = spine.localRotation; if (spine1 != null) qSp1 = spine1.localRotation; if (spine2 != null) qSp2 = spine2.localRotation;
            if (neck != null) qNeck = neck.localRotation; if (head != null) qHead = head.localRotation;
            // arms: rotate each upper arm about the body's forward axis; keep the sign that brings the hand DOWN
            armSignL = PickDown(lArm, F(transform, "LeftHand")); armSignR = PickDown(rArm, F(transform, "RightHand"));
            if (lArm != null) qL = lArm.localRotation; if (rArm != null) qR = rArm.localRotation;
            if (lFore != null) qLF = lFore.localRotation; if (rFore != null) qRF = rFore.localRotation;
        }
        float PickDown(Transform arm, Transform hand)
        {
            if (arm == null || hand == null) return 0;
            var q0 = arm.rotation; float y0 = hand.position.y;
            arm.rotation = Quaternion.AngleAxis(20, transform.forward) * q0; float yp = hand.position.y; arm.rotation = q0;
            return yp < y0 ? 1f : -1f;
        }
        void LateUpdate()
        {
            float t = Time.time; float br = Mathf.Sin(t * 1.6f), sway = Mathf.Sin(t * 0.55f);
            if (hips != null) { hips.localRotation = qHips; hips.Rotate(transform.forward, sway * 1.6f, Space.World); hips.localPosition = pHips; }
            if (spine1 != null) { spine1.localRotation = qSp1; spine1.Rotate(transform.right, br * 1.2f, Space.World); spine1.Rotate(transform.forward, -sway * 1.2f, Space.World); }
            if (spine2 != null) { spine2.localRotation = qSp2; spine2.Rotate(transform.right, br * 0.8f, Space.World); }
            if (head != null) { head.localRotation = qHead; head.Rotate(Vector3.up, Mathf.Sin(t * 0.31f) * 9f, Space.World); head.Rotate(transform.forward, sway * 2.5f, Space.World); }
            if (lArm != null && armSignL != 0) { lArm.localRotation = qL; lArm.Rotate(transform.forward, armSignL * (32f + br * 1.5f), Space.World); }
            if (rArm != null && armSignR != 0) { rArm.localRotation = qR; rArm.Rotate(transform.forward, armSignR * (32f + br * 1.5f), Space.World); }
            if (lFore != null) { lFore.localRotation = qLF; lFore.Rotate(transform.right, -8f, Space.World); }
            if (rFore != null) { rFore.localRotation = qRF; rFore.Rotate(transform.right, -8f, Space.World); }
        }
    }
}
