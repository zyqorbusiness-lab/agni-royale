// Agni Royale — how a fighter looks on every client: the anime character + combat clips (anims_combat, our own),
// rifle in the right hand, throwable in hand while aiming a throw, spine bend for aim pitch, death/knock poses,
// teammate name tags. THROW HOOKS: PlayPullPin() / PlayThrow() raise OnPullPin / OnThrowRelease events and drive a
// procedural over-arm throw on the right arm; if an "Anim_Throw" clip is ever added to Resources/AgniNet/Anims it is used instead.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agni.Net
{
    public class AgniCharacterView : MonoBehaviour
    {
        public static event Action<AgniPlayer, ThrowKind> OnPullPin, OnThrowRelease;   // hooks for SFX/VFX/animation
        AgniPlayer p; Transform model; Animation anim; bool hasClips;
        Transform hips, spine1, spine2, rArm, rFore, rHand, lArm, head; Quaternion qR, qRF;
        Vector3 lastPos; float speed, lastShot = -9, throwT = -9, pinT = -9, deathT = -9; ThrowKind heldKind; GameObject gun, held; TextMesh nameTag; static Material gunMat;
        static readonly string[] Clips = { "Rifle_Idle", "Run_Fwd_InPlace", "Sprint_InPlace", "Crouch_Idle", "Crouch_Walk_InPlace", "Prone_Crawl_InPlace", "Fall_Loop", "Death_Fall", "Fire_Auto", "Reload", "Throw" };

        public static AgniCharacterView Create(AgniPlayer p)
        {
            var go = new GameObject("View"); go.transform.SetParent(p.transform, false);
            var v = go.AddComponent<AgniCharacterView>(); v.p = p; v.Build(); return v;
        }
        static Transform F(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n || t.name.EndsWith(":" + n)) return t; return null; }

        void Build()
        {
            var prefab = Resources.Load<GameObject>("AgniChars/Meera");
            int q = Agni.UI.AgniMahal.Quality; if (q > 1) q = 1; // in-match: LOD1 at most (50 fighters)
            if (prefab != null)
            {
                var ch = Instantiate(prefab, transform); ch.name = "Model"; model = ch.transform;
                Agni.UI.AgniCharacterLook.Setup(ch, q);
                var an = ch.GetComponent<Animator>(); if (an != null) Destroy(an);
                float h = Height(ch); if (h > 0.01f) ch.transform.localScale = Vector3.one * (1.72f / h);
                hips = F(model, "Hips"); spine1 = F(model, "Spine1"); spine2 = F(model, "Spine2"); rArm = F(model, "RightArm"); rFore = F(model, "RightForeArm"); rHand = F(model, "RightHand"); lArm = F(model, "LeftArm"); head = F(model, "Head");
                var rig = ch.transform.Find("Meera_Rig"); // clip FBXs import with Meera_Rig as their root, so the Animation lives on that node
                anim = (rig != null ? rig.gameObject : ch).AddComponent<Animation>(); anim.playAutomatically = false; anim.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (var c in Clips)
                {
                    AnimationClip clip = null; foreach (var cl in Resources.LoadAll<AnimationClip>("AgniNet/Anims/Anim_" + c)) if (!cl.name.StartsWith("__preview__")) { clip = cl; break; }
                    if (clip == null) continue;
                    if (!clip.legacy) { Debug.LogWarning("[AgniView] clip not legacy: " + c); continue; }
                    anim.AddClip(clip, c); hasClips = true;
                }
                if (hasClips)
                {
                    foreach (var c in new[] { "Fire_Auto", "Reload", "Throw" }) { var st = anim[c]; if (st != null && spine1 != null) { st.layer = 1; st.AddMixingTransform(spine1); st.weight = 1; } }
                    foreach (AnimationState st in anim) if (st.layer == 0) st.wrapMode = st.name == "Death_Fall" ? WrapMode.ClampForever : WrapMode.Loop;
                    anim.Play("Rifle_Idle");
                }
                else ch.AddComponent<Agni.UI.AgniIdle>();
                if (rArm != null) qR = rArm.localRotation; if (rFore != null) qRF = rFore.localRotation;
            }
            else
            {   // fallback capsule
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(cap.GetComponent<Collider>()); cap.transform.SetParent(transform, false); cap.transform.localPosition = Vector3.up * 0.9f; model = cap.transform;
            }
            lastPos = p.transform.position;
            // team mates get a soft blue name nameTag (enemies none)
            var local = AgniPlayer.Local;
            if (local != null && local != p && local.Team.Value == p.Team.Value) MakeTag();
            p.Team.OnValueChanged += (a, b) => { var l = AgniPlayer.Local; if (nameTag == null && l != null && l != p && l.Team.Value == b) MakeTag(); if (nameTag != null && l != null && l.Team.Value != b) { Destroy(nameTag.gameObject); nameTag = null; } };
        }
        public void RefreshTag() { var l = AgniPlayer.Local; if (nameTag == null && l != null && l != p && l.Team.Value == p.Team.Value) MakeTag(); }
        void MakeTag()
        {
            var go = new GameObject("NameTag"); go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up * 2.15f;
            nameTag = go.AddComponent<TextMesh>(); nameTag.text = p.Nick; nameTag.font = Agni.UI.K.Body; nameTag.GetComponent<MeshRenderer>().sharedMaterial = nameTag.font.material;
            nameTag.fontSize = 48; nameTag.characterSize = 0.035f; nameTag.anchor = TextAnchor.MiddleCenter; nameTag.color = new Color(0.55f, 0.85f, 1f);
        }
        byte shownGun = 255; float punchT = -9; Vector3 palmOffset = Vector3.zero; bool HasGun => shownGun != 0 && shownGun != 255;
        ulong shownSkin;
        void AttachGun(ItemId id)
        {
            shownGun = (byte)id; if (gun != null) Destroy(gun); gun = null;
            if (id == ItemId.None || rHand == null) return;
            shownSkin = p.GunSkins.Value; gun = AgniLootView.GunModel(id, transform, false, AgniGunSkins.Unpack(p.GunSkins.Value, AgniItems.Gun(id).model));
            if (gun != null) { gun.name = "Held_" + id; gun.transform.localScale = Vector3.one; }
        }
        /// bend the arms so the hands come up to a shoulder-aim (the rifle clips are hip-ish); keeps the grip at the right palm
        void AimPose(Vector3 aimDir)
        {
            if (rArm == null || rFore == null || anim == null) return;
            // point the right upper arm forward-down and the forearm along the aim, then let the gun follow the palm
            var fwd = aimDir; var right = transform.right;
            var shoulder = rArm.position; var wantHand = shoulder + fwd * 0.30f - right * 0.08f - Vector3.up * 0.10f;
            var curHand = rHand.position;
            var upper = (rFore.position - shoulder); var want = Vector3.Slerp(upper.normalized, (wantHand - shoulder).normalized, 0.85f);
            rArm.rotation = Quaternion.FromToRotation(upper.normalized, want) * rArm.rotation;
            var fore = (rHand.position - rFore.position); var wantFore = Vector3.Slerp(fore.normalized, (fwd * 0.9f - right * 0.15f).normalized, 0.8f);
            rFore.rotation = Quaternion.FromToRotation(fore.normalized, wantFore) * rFore.rotation;
            if (lArm != null)
            {   // support hand reaches toward the handguard
                var lFore = lArm.childCount > 0 ? lArm.GetChild(0) : null;
                var guard = rHand.position + fwd * 0.35f;
                var lu = (lFore != null ? lFore.position : lArm.position + Vector3.down * 0.25f) - lArm.position;
                lArm.rotation = Quaternion.FromToRotation(lu.normalized, Vector3.Slerp(lu.normalized, (guard - lArm.position).normalized, 0.8f)) * lArm.rotation;
                if (lFore != null && lFore.childCount > 0) { var lh = lFore.GetChild(0); var lf = lh.position - lFore.position; lFore.rotation = Quaternion.FromToRotation(lf.normalized, Vector3.Slerp(lf.normalized, (guard - lFore.position).normalized, 0.85f)) * lFore.rotation; }
            }
        }
        static float Height(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return 0; var b = rs[0].bounds; foreach (var r in rs) if (r.enabled) b.Encapsulate(r.bounds); return b.size.y; }

        // ------------- hooks called by AgniPlayer RPCs
        public void OnShot() { lastShot = Time.time; }
        public void PlayPunch() { punchT = Time.time; }
        public void PlayReload() { if (hasClips && anim["Reload"] != null) anim.CrossFade("Reload", 0.1f); }
        public void PlayJump() { }
        public void PlayPullPin(ThrowKind k) { pinT = Time.time; heldKind = k; OnPullPin?.Invoke(p, k); }
        public void PlayThrow(ThrowKind k)
        {
            throwT = Time.time; heldKind = k; OnThrowRelease?.Invoke(p, k);
            if (hasClips && anim["Throw"] != null) anim.CrossFade("Throw", 0.05f);
        }
        public void OnLife(LifeState from, LifeState to)
        {
            if (to == LifeState.Dead) { deathT = Time.time; if (hasClips && anim["Death_Fall"] != null) anim.CrossFade("Death_Fall", 0.1f); if (nameTag != null) nameTag.gameObject.SetActive(false); }
            else if (to == LifeState.Alive && model != null) { model.gameObject.SetActive(true); if (nameTag != null) nameTag.gameObject.SetActive(true); }
        }

        void Update()
        {
            if (p == null || model == null) return;
            var pos = transform.position; var d = pos - lastPos; d.y = 0; lastPos = pos;
            speed = Mathf.Lerp(speed, d.magnitude / Mathf.Max(0.0001f, Time.deltaTime), 0.2f);
            if (p.Dead) { if (deathT > 0 && Time.time - deathT > 4f) model.gameObject.SetActive(false); return; }
            bool holdingThrow = p.Slot.Value == 1 || Time.time - throwT < 0.6f;
            if (shownGun != p.HeldGun.Value || shownSkin != p.GunSkins.Value) AttachGun((ItemId)p.HeldGun.Value);
            if (gun != null) gun.SetActive(!holdingThrow && !p.Knocked && !p.Driving);
            if (holdingThrow && rHand != null && (held == null || heldKind != (ThrowKind)p.Sel.Value) && Time.time - throwT > 0.45f)
            {
                if (held != null) Destroy(held); heldKind = (ThrowKind)p.Sel.Value;
                held = AgniFx.BuildThrowableView(heldKind, rHand); held.transform.localPosition = Vector3.zero; held.transform.localScale = Vector3.one / Mathf.Max(0.0001f, rHand.lossyScale.x);
            }
            if (held != null) held.SetActive(holdingThrow && (Time.time - throwT > 0.45f || Time.time - throwT < 0.27f));
            if (!holdingThrow && held != null) { Destroy(held); held = null; }
            if (!hasClips) return;
            string want;
            if (p.Knocked) want = "Prone_Crawl_InPlace";
            else if (p.Crouched.Value) want = speed > 0.4f ? "Crouch_Walk_InPlace" : "Crouch_Idle";
            else if (p.InVehicle) want = "Rifle_Idle"; // seated: upper body idles, legs are bent into the seat in LateUpdate
            else want = speed > 6.1f ? "Sprint_InPlace" : speed > 0.5f ? "Run_Fwd_InPlace" : "Rifle_Idle";
            if (anim[want] == null) want = "Rifle_Idle";
            if (!anim.IsPlaying(want)) anim.CrossFade(want, 0.18f);
            var st = anim[want]; if (st != null && want.Contains("InPlace")) st.speed = Mathf.Clamp(speed / (want.StartsWith("Sprint") ? 6f : want.StartsWith("Crouch") ? 1f : want.StartsWith("Prone") ? 0.5f : 3.6f), 0.5f, 1.6f);
            if (anim["Fire_Auto"] != null) { bool firing = Time.time - lastShot < 0.18f && !holdingThrow; if (firing && !anim.IsPlaying("Fire_Auto")) anim.CrossFade("Fire_Auto", 0.05f); else if (!firing && anim.IsPlaying("Fire_Auto")) anim.Blend("Fire_Auto", 0, 0.15f); }
        }

        void LateUpdate()
        {
            if (nameTag != null) { var c = AgniLocalPlayer.Instance != null ? AgniLocalPlayer.Instance.Cam : Camera.main; if (c != null) nameTag.transform.rotation = c.transform.rotation; } // billboard (was mirrored)
            if (p == AgniPlayer.Local && AgniLocalPlayer.Instance != null && !p.Dead)
            {   // this phone's fighter: draw it where prediction says (no wait for the server) and turn it with the local camera yaw
                var lp = AgniLocalPlayer.Instance; transform.SetPositionAndRotation(lp.ViewPos, Quaternion.Euler(0, lp.Yaw, 0));
            }
            else if (transform.localPosition != Vector3.zero) { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; }
            if (p == null || model == null || p.Dead || p.Knocked) return;
            if (p.InVehicle) { var veh = p.Vehicle; if (veh != null) { transform.SetPositionAndRotation(AgniPlayer.SeatRoot(veh, p.Seat.Value), veh.transform.rotation); SitPose(veh); } }
            // aim pitch: bend the upper spine (remote players visibly look up/down)
            float pitch = (p == AgniPlayer.Local && AgniLocalPlayer.Instance != null) ? AgniLocalPlayer.Instance.Pitch : p.Pitch.Value;
            if (spine2 != null) spine2.Rotate(transform.right, Mathf.Clamp(pitch, -50, 50) * 0.45f, Space.World);
            if (spine1 != null) spine1.Rotate(transform.right, Mathf.Clamp(pitch, -50, 50) * 0.25f, Space.World);
            // gun: lives on the view root at world scale 1 (bone scale no longer matters); its grip (FBX origin) sits in the right palm
            // and the barrel (FBX -Z) points where the fighter aims. Arms are bent into a shoulder-aim pose around it.
            if (gun != null && gun.activeSelf && rHand != null)
            {
                var aimDir = Quaternion.Euler(Mathf.Clamp(pitch, -60, 60), transform.eulerAngles.y, 0) * Vector3.forward;
                AimPose(aimDir);
                var ax = gun.GetComponent<AgniLootView.GunAxes>(); gun.transform.SetPositionAndRotation(rHand.position + rHand.rotation * palmOffset, Quaternion.LookRotation(aimDir, Vector3.up) * (ax != null ? ax.fix : Quaternion.identity));
            }
            float pt = Time.time - punchT;
            if (rArm != null && !HasGun && pt < 0.4f) { float k = pt < 0.12f ? pt / 0.12f : 1 - (pt - 0.12f) / 0.28f; rArm.Rotate(transform.right, -70f * k, Space.World); if (rFore != null) rFore.Rotate(transform.right, 50f * k, Space.World); }
            // procedural over-arm throw on the right arm (hook-driven; replaced by an Anim_Throw clip when one exists)
            if (rArm == null || (anim != null && anim["Throw"] != null)) return;
            float tc = Time.time - throwT, tp = Time.time - pinT;
            float raise = 0, swing = 0;
            if (p.Slot.Value == 1 && tc > 0.6f) raise = Mathf.Clamp01(tp / 0.25f);                 // holding / cooking: arm cocked back
            if (tc < 0.6f) { raise = tc < 0.12f ? 1 : Mathf.Clamp01(1 - (tc - 0.12f) / 0.35f); swing = tc < 0.12f ? tc / 0.12f : Mathf.Clamp01(1 - (tc - 0.2f) / 0.4f); }
            if (raise <= 0 && swing <= 0) return;
            var right = transform.right; var fwd = transform.forward;
            rArm.Rotate(fwd, -95f * raise, Space.World);                    // lift the upper arm up beside the head
            rArm.Rotate(right, (-35f * raise) + 110f * swing, Space.World); // cock back, then whip forward
            if (rFore != null) rFore.Rotate(right, -70f * raise * (1 - swing), Space.World);
            if (spine2 != null) spine2.Rotate(Vector3.up, 18f * raise - 30f * swing, Space.World);
        }

        Transform lUp, lLow, rUp, rLow, lFoot, rFoot; bool legsFound;
        /// seated: thighs forward, shins down (car seats) or astride (bike / buggy pillion); hands rest near the wheel for the driver
        void SitPose(AgniVehicle veh)
        {
            if (!legsFound) { legsFound = true; lUp = F(model, "LeftUpLeg"); lLow = F(model, "LeftLeg"); rUp = F(model, "RightUpLeg"); rLow = F(model, "RightLeg"); }
            var right = transform.right; var fwd = transform.forward; bool astride = veh.kind == VKind.Bike;
            float hip = astride ? -70f : -86f, knee = astride ? 95f : 88f, spread = astride ? 16f : 6f;
            if (lUp != null) { lUp.Rotate(right, hip, Space.World); lUp.Rotate(fwd, -spread, Space.World); }
            if (rUp != null) { rUp.Rotate(right, hip, Space.World); rUp.Rotate(fwd, spread, Space.World); }
            if (lLow != null) lLow.Rotate(right, knee, Space.World);
            if (rLow != null) rLow.Rotate(right, knee, Space.World);
            if (p.Driving && rArm != null && lArm != null && (gun == null || !gun.activeSelf))
            {   // both arms forward to the handlebar / wheel
                rArm.Rotate(right, -55f, Space.World); lArm.Rotate(right, -55f, Space.World);
                rArm.Rotate(fwd, astride ? 25f : 40f, Space.World); lArm.Rotate(fwd, astride ? -25f : -40f, Space.World);
            }
        }
    }
}
