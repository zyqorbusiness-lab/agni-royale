// Agni Royale — this phone's own fighter: third-person camera, touch + keyboard/mouse input (sent to the server),
// throw arc preview with landing marker, flash blindness, hit markers and the match HUD (AgniHud).
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Agni.Net
{
    public class AgniLocalPlayer : MonoBehaviour
    {
        public static AgniLocalPlayer Instance;
        public AgniPlayer P { get; private set; }
        public Camera Cam { get; private set; }
        public Camera UICam { get; private set; }
        public float Yaw, Pitch;
        public AgniHud Hud { get; private set; }

        // touch / button state written by the HUD controls
        [System.NonSerialized] public Vector2 stick, lookDelta; [System.NonSerialized] public bool fireHeld, interactHeld, sprintHeld;
        // test automation (AgniShot): when set, overrides the human input
        public struct Scripted { public bool on; public Vector2 move; public bool fire, interact; }
        [System.NonSerialized] public Scripted script;
        [System.NonSerialized] public bool freeCam; // proof driver owns the camera

        float sendT; LineRenderer arc; LineRenderer marker; readonly List<Vector3> pts = new List<Vector3>();
        float flashUntil, flashLen, flashK; float camDist = 3.0f; Vector3 camVel; AgniPlayer spectate;
        public AgniLootHud LootHud { get; private set; }
        // ---- scope / aim-down-sights (client view only; the server gets the Aim bit for spread + walk speed)
        [System.NonSerialized] public bool aimToggle, aimHeld; public bool Aiming => (aimToggle || aimHeld) && P != null && P.Alive && P.Slot.Value == 0 && P.HasGunInHand;
        public float Zoom { get { if (!Aiming) return 1f; var v = P.Inv.Value; var sc = v.Att(v.act, ItemCat.Scope); int z = AgniItems.ScopeZoom(sc); if (z == 0) z = AgniItems.Gun(P.Held).builtInZoom; return z == 0 ? 1.3f : z; } }
        float zoomNow = 1f;
        // ---- client-side movement prediction (pure clients only; the host simulates for real)
        CharacterController ghost; float pvy; readonly List<(float t, Vector3 p)> hist = new List<(float, Vector3)>(); bool ghostInit;
        public bool Predicting => P != null && !P.IsServer && !P.Dead && ghost != null && ghostInit;
        public Vector3 ViewPos => Predicting ? ghost.transform.position : P.transform.position;
        public float PredictError { get; private set; }
        public float FlashAlpha { get { if (Time.time > flashUntil) return 0; float left = flashUntil - Time.time, hold = flashLen * 0.35f; return flashK * (left > flashLen - hold ? 1f : Mathf.Clamp01(left / (flashLen - hold))); } }

        public static void Create(AgniPlayer p)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("AgniLocalPlayer"); var lp = go.AddComponent<AgniLocalPlayer>(); lp.P = p; Instance = lp;
            lp.Yaw = p.transform.eulerAngles.y; lp.Build();
        }
        void Build()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.CompareTag("MainCamera")) c.gameObject.SetActive(false);
            var camGo = new GameObject("PlayerCamera"); camGo.tag = "MainCamera"; Cam = camGo.AddComponent<Camera>(); Cam.fieldOfView = 62; Cam.nearClipPlane = 0.08f; Cam.farClipPlane = 900;
            Cam.clearFlags = CameraClearFlags.Skybox; camGo.AddComponent<AudioListener>();
            var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urp.renderPostProcessing = false; urp.renderShadows = true;
            var uiGo = new GameObject("UICamera"); uiGo.transform.SetParent(camGo.transform, false); UICam = uiGo.AddComponent<Camera>();
            UICam.clearFlags = CameraClearFlags.Depth; UICam.cullingMask = 1 << 5; UICam.nearClipPlane = 0.1f; UICam.farClipPlane = 10f;
            var ud = uiGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ud.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay; urp.cameraStack.Add(UICam); Cam.cullingMask &= ~(1 << 5);
            Hud = AgniHud.Create(this); LootHud = AgniLootHud.Create(this);
            if (!P.IsServer)
            {
                var gg = new GameObject("PredictGhost"); gg.transform.SetParent(transform, false); gg.transform.position = P.transform.position;
                ghost = gg.AddComponent<CharacterController>(); ghost.height = 1.8f; ghost.radius = 0.35f; ghost.center = new Vector3(0, 0.9f, 0); ghost.stepOffset = 0.45f; ghost.slopeLimit = 50; ghost.skinWidth = 0.05f; ghost.minMoveDistance = 0;
                var own = P.GetComponent<CharacterController>(); if (own != null) Physics.IgnoreCollision(ghost, own);
            }
            arc = NewLine("ThrowArc", 0.07f); marker = NewLine("ThrowMarker", 0.08f); marker.loop = true; arc.enabled = marker.enabled = false;
            arc.textureMode = LineTextureMode.Tile;
        }
        LineRenderer NewLine(string n, float w)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false); var l = go.AddComponent<LineRenderer>(); l.material = AgniFx.LineMaterial(); l.widthMultiplier = w; l.useWorldSpace = true;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.numCapVertices = 2; return l;
        }
        void OnDestroy() { if (Instance == this) Instance = null; if (Cam != null) Destroy(Cam.gameObject); if (Hud != null) Destroy(Hud.gameObject); }

        void Update()
        {
            if (P == null) { Destroy(gameObject); return; }
            ReadDevices();
            float sens = 0.12f * Agni.UI.AgniSettings.Sensitivity / Mathf.Pow(zoomNow, 0.75f);
            Yaw += lookDelta.x * sens; Pitch = Mathf.Clamp(Pitch - lookDelta.y * sens, -70f, 70f); lookDelta = Vector2.zero;
            // input → server at 30 Hz
            sendT -= Time.deltaTime;
            if (sendT <= 0)
            {
                sendT = 1f / 30f;
                var inp = new AgniInput { yaw = Yaw, pitch = Pitch, move = script.on ? script.move : stick, aim = AimPoint() };
                bool fire = script.on ? script.fire : fireHeld; bool inter = script.on ? script.interact : interactHeld;
                if (fire) inp.buttons |= AgniInput.Fire; if (inter) inp.buttons |= AgniInput.Interact; if (sprintHeld) inp.buttons |= AgniInput.Sprint; if (Aiming) inp.buttons |= AgniInput.Aim;
                P.SendInput(inp); lastSent = inp;
            }
            Predict(Time.deltaTime);
            UpdateArc();
        }

        public Vector3 AimPoint()
        {
            if (Cam == null) return P.Eye + Quaternion.Euler(Pitch, Yaw, 0) * Vector3.forward * 100;
            var ray = new Ray(Cam.transform.position, Cam.transform.forward);
            var hits = Physics.RaycastAll(ray, 320f, ~0, QueryTriggerInteraction.Ignore); float best = 320f; Vector3 pt = ray.GetPoint(320f);
            foreach (var h in hits) { if (h.collider.transform.IsChildOf(P.transform) || h.collider.transform.IsChildOf(transform)) continue; if (h.distance < best && h.distance > 1.2f) { best = h.distance; pt = h.point; } }
            return pt;
        }

        void ReadDevices()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current; var ms = Mouse.current;
            if (kb != null && !script.on)
            {
                var k = Vector2.zero; if (kb.wKey.isPressed) k.y += 1; if (kb.sKey.isPressed) k.y -= 1; if (kb.dKey.isPressed) k.x += 1; if (kb.aKey.isPressed) k.x -= 1;
                if (k != Vector2.zero || Hud == null || !Hud.StickActive) stick = Vector2.ClampMagnitude(k, 1);
                sprintHeld = kb.leftShiftKey.isPressed;
                if (kb.spaceKey.wasPressedThisFrame) { P.JumpRpc(); PredictJump(); }
                if (kb.cKey.wasPressedThisFrame) P.CrouchRpc();
                if (kb.rKey.wasPressedThisFrame) P.ReloadRpc();
                if (kb.hKey.wasPressedThisFrame) P.HealRpc();
                if (kb.digit1Key.wasPressedThisFrame) P.SlotRpc(0);
                if (kb.digit2Key.wasPressedThisFrame) P.SlotRpc(1);
                if (kb.gKey.wasPressedThisFrame) NextThrowable();
                if (kb.qKey.wasPressedThisFrame) P.SwapGunRpc();
                if (kb.fKey.wasPressedThisFrame && LootHud != null) LootHud.PickFirst();
                if (kb.tabKey.wasPressedThisFrame && LootHud != null) LootHud.ToggleBag();
                if (kb.vKey.wasPressedThisFrame) aimToggle = !aimToggle;
                if (kb.bKey.wasPressedThisFrame) P.VehicleRpc(); // get in / out of the nearest vehicle
                if (kb.nKey.wasPressedThisFrame) P.EmoteRpc(P.Emote.Value != 0 ? (byte)0 : (byte)(AgniEmotes.Pref + 1)); // emote on / off
                interactHeld = kb.eKey.isPressed || (Hud != null && Hud.ReviveHeld);
                if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            }
            if (ms != null && !script.on && !Application.isMobilePlatform)
            {
                if (Cursor.lockState == CursorLockMode.Locked) { lookDelta += ms.delta.ReadValue(); fireHeld = ms.leftButton.isPressed || (Hud != null && Hud.FireHeld); aimHeld = ms.rightButton.isPressed; }
                else { if (Hud != null) fireHeld = Hud.FireHeld; if (ms.rightButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked; }
            }
            else if (Hud != null && !script.on) { fireHeld = Hud.FireHeld; interactHeld = Hud.ReviveHeld; }
#endif
        }
        public void NextThrowable()
        {
            var cur = (ThrowKind)P.Sel.Value; int i = System.Array.IndexOf(ThrowMath.All, cur);
            for (int n = 1; n <= 4; n++) { var k = ThrowMath.All[(i + n + 4) % 4]; if (P.Count(k) > 0) { P.SelectThrowRpc((byte)k); return; } }
        }

        void LateUpdate()
        {
            if (P == null || Cam == null || freeCam) return;
            var target = P;
            if (P.Dead)
            {   // spectate: a standing team mate if any
                if (spectate == null || spectate.Dead) { spectate = null; foreach (var o in AgniPlayer.All) if (o != P && o.Team.Value == P.Team.Value && !o.Dead) { spectate = o; break; } }
                if (spectate != null) target = spectate;
            }
            float height = target.Knocked ? 0.9f : target.Crouched.Value ? 1.25f : 1.6f;
            var basePos = target == P ? ViewPos : target.transform.position;
            var rideV = target.InVehicle ? target.Vehicle : null; // in a vehicle: orbit the vehicle, a little higher and further back
            if (rideV != null) { basePos = rideV.transform.position; height = rideV.kind == VKind.Bike ? 1.7f : rideV.kind == VKind.Monster ? 3.2f : 2.2f; }
            var am = AgniMatch.Instance; bool onBoard = target.InPlane && am != null && am.HasPlane;
            if (onBoard) { basePos = am.PlanePos(am.NowT); height = 2f; }
            var pivot = basePos + Vector3.up * height;
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            zoomNow = Mathf.MoveTowards(zoomNow, target == P ? Zoom : 1f, Time.deltaTime * 14f * Mathf.Max(1f, zoomNow));
            Cam.fieldOfView = 62f / zoomNow;
            float dist = zoomNow > 1.05f ? Mathf.Lerp(camDist, 1.15f, Mathf.Clamp01((zoomNow - 1f) * 2f)) : camDist;
            if (rideV != null && zoomNow <= 1.05f) dist = rideV.kind == VKind.Monster ? 9.5f : rideV.kind == VKind.Bike ? 4.6f : 6.8f;
            if (onBoard) dist = 46f; else if (target.Airborne && zoomNow <= 1.05f) dist = target.Air.Value == (byte)AirState.Chute ? 8.5f : 6.5f;
            var want = pivot + rot * new Vector3(zoomNow > 1.05f ? 0.45f : 0.62f, 0.12f, -dist);
            // keep the camera out of walls
            var dir = want - pivot; float len = dir.magnitude;
            if (!onBoard) foreach (var h in SortedCast(pivot, dir / len, len)) { if (h.collider.transform.IsChildOf(target.transform) || h.collider.transform.IsChildOf(transform) || (rideV != null && h.collider.transform.IsChildOf(rideV.transform))) continue; want = pivot + dir / len * Mathf.Max(0.3f, h.distance - 0.1f); break; }
            Cam.transform.SetPositionAndRotation(want, rot);
            // shake (trauma²)
            if (AgniCamRig.Trauma > 0)
            {
                float s = AgniCamRig.Trauma * AgniCamRig.Trauma; float t = Time.time * 28f;
                Cam.transform.position += Cam.transform.right * (Mathf.PerlinNoise(t, 0) - 0.5f) * 0.5f * s + Cam.transform.up * (Mathf.PerlinNoise(0, t) - 0.5f) * 0.5f * s;
                Cam.transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 3) - 0.5f) * 6 * s, (Mathf.PerlinNoise(5, t) - 0.5f) * 6 * s, (Mathf.PerlinNoise(t, t) - 0.5f) * 8 * s);
                AgniCamRig.Trauma = Mathf.Max(0, AgniCamRig.Trauma - Time.deltaTime * 1.4f);
            }
        }

        // ---------------- throw arc preview (same maths as the server)
        void UpdateArc()
        {
            bool show = P.Alive && P.Slot.Value == 1 && (script.on ? script.fire : fireHeld) && P.Count((ThrowKind)P.Sel.Value) > 0;
            arc.enabled = marker.enabled = show; if (!show) return;
            var k = (ThrowKind)P.Sel.Value;
            ThrowMath.Launch(P.transform.position, Yaw, Pitch, k, 1f, out var o, out var v);
            ThrowMath.Simulate(o, v, k, pts, out var land, out var n, 3.5f, ~0, P.GetComponent<Collider>());
            // skip the first 0.6 m so the line starts in front of the hand
            int start = 0; while (start < pts.Count - 2 && (pts[start] - o).sqrMagnitude < 0.36f) start++;
            arc.positionCount = pts.Count - start; for (int i = start; i < pts.Count; i++) arc.SetPosition(i - start, pts[i]);
            var col = k == ThrowKind.Gola ? new Color(1f, 0.93f, 0.8f) : k == ThrowKind.Dhuan ? new Color(0.85f, 0.92f, 0.95f) : k == ThrowKind.Patakha ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.75f, 0.55f);
            arc.startColor = new Color(col.r, col.g, col.b, 0.55f); arc.endColor = new Color(col.r, col.g, col.b, 0.95f);
            // landing ring: blast radius for GOLA, cloud size for DHUAN, wall footprint for DEEWAR
            float r = k == ThrowKind.Gola ? ThrowMath.Def(k).radius * 0.5f : k == ThrowKind.Dhuan ? 3f : k == ThrowKind.Patakha ? 1.2f : 0f;
            if (k == ThrowKind.Deewar)
            {
                marker.loop = false; marker.positionCount = 13; var q = Quaternion.Euler(0, Yaw, 0);
                for (int i = 0; i < 13; i++) { float th = Mathf.Lerp(-33, 33, i / 12f) * Mathf.Deg2Rad; var lp = new Vector3(Mathf.Sin(th) * 3.4f, 0, Mathf.Cos(th) * 3.4f - 3.4f); var wp = land + q * lp; wp.y = AgniFx.GroundY(wp) + 0.08f; marker.SetPosition(i, wp); }
            }
            else
            {
                marker.loop = true; marker.positionCount = 40; var up = n.sqrMagnitude > 0.1f ? n : Vector3.up; var t1 = Vector3.Cross(up, Vector3.forward); if (t1.sqrMagnitude < 0.01f) t1 = Vector3.Cross(up, Vector3.right); t1.Normalize(); var t2 = Vector3.Cross(up, t1);
                for (int i = 0; i < 40; i++) { float a = i / 40f * Mathf.PI * 2; marker.SetPosition(i, land + up * 0.06f + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * r); }
            }
            marker.startColor = marker.endColor = new Color(col.r, col.g * 0.85f, col.b * 0.7f, 0.85f);
        }

        // ---------------- feedback from the server
        public void OnFlashed(float k, float secs) { flashK = Mathf.Clamp01(k * 1.1f); flashLen = secs; flashUntil = Time.time + secs; AgniSynth.Play2D(AgniSynth.Ring, 0.5f * k); }
        public void OnHitConfirm(float dmg, bool head, bool knockOrKill) { Hud?.HitMarker(dmg, head, knockOrKill); }
        public void OnHurt(Vector3 from, float dmg) { Hud?.Hurt(from, dmg); }
        public void OnPicked(ItemId k, int n) { LootHud?.OnPicked(k, n); Agni.UI.AgniSfx.Play("sfx_reward_coin", 0.45f); }
        public void OnPickFail(ItemId k) { LootHud?.OnPickFail(k); }
        public void OnTeleported(float yaw) { Yaw = yaw; Pitch = 0; SnapGhost(); }

        static RaycastHit[] SortedCast(Vector3 o, Vector3 d, float len) { var hs = Physics.SphereCastAll(o, 0.18f, d, len, ~0, QueryTriggerInteraction.Ignore); System.Array.Sort(hs, (a, b) => a.distance.CompareTo(b.distance)); return hs; }

        // ---------------- client-side prediction: run the same walk maths on a ghost capsule, then pull it softly
        // toward where the server says we were (compared against our own history at that moment), snap if far off.
        AgniInput lastSent;
        void SnapGhost() { if (ghost == null) return; ghost.enabled = false; ghost.transform.position = P.transform.position; ghost.enabled = true; pvy = 0; hist.Clear(); ghostInit = true; }
        public void PredictJump() { if (Predicting && ghost.isGrounded && !P.Crouched.Value && P.Alive) pvy = AgniPlayer.JumpV; }
        void Predict(float dt)
        {
            if (ghost == null) return;
            if (!ghostInit || P.Dead || P.InVehicle || P.InAir) { SnapGhost(); return; }
            var inp = lastSent; inp.move = script.on ? script.move : stick; inp.yaw = Yaw;
            bool fire = script.on ? script.fire : fireHeld; bool sprint = sprintHeld;
            var m = AgniMatch.Instance; if (m != null && m.Phase.Value == (byte)MatchPhase.Ended) inp.move = Vector2.zero;
            float speed = P.Knocked ? AgniPlayer.CrawlSpeed : P.Crouched.Value ? AgniPlayer.CrouchSpeed : (sprint && inp.move.y > 0.5f && !fire) ? AgniPlayer.SprintSpeed : AgniPlayer.RunSpeed;
            if (P.Healing.Value) speed *= 0.45f; if (Aiming && !P.Knocked) speed *= 0.6f;
            var mv = Vector2.ClampMagnitude(inp.move, 1f); var wish = Quaternion.Euler(0, Yaw, 0) * new Vector3(mv.x, 0, mv.y) * speed;
            ghost.height = P.Crouched.Value ? 1.2f : 1.8f; ghost.center = Vector3.up * ghost.height * 0.5f;
            if (ghost.isGrounded && pvy < 0) pvy = -2f; pvy -= AgniPlayer.Gravity * dt;
            ghost.Move((wish + Vector3.up * pvy) * dt);
            float now = Time.time; hist.Add((now, ghost.transform.position)); while (hist.Count > 0 && now - hist[0].t > 1.5f) hist.RemoveAt(0);
            // the server pose we see is about one round trip + the interpolation buffer old
            float lag = Mathf.Clamp(AgniNet.RttSeconds() + 0.1f, 0.05f, 1.2f); Vector3 then = hist[0].p;
            for (int i = hist.Count - 1; i >= 0; i--) if (hist[i].t <= now - lag) { then = hist[i].p; break; }
            var err = P.transform.position - then; PredictError = err.magnitude;
            if (err.sqrMagnitude > 3f * 3f) { SnapGhost(); return; }
            var fix = err * Mathf.Clamp01(dt * 3.5f); if (fix.sqrMagnitude > 1e-8f) { ghost.Move(fix); for (int i = 0; i < hist.Count; i++) hist[i] = (hist[i].t, hist[i].p + fix); }
        }
    }
}
