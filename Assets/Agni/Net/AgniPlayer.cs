// Agni Royale — networked fighter (humans and bots share this). SERVER-AUTHORITATIVE:
// the owner only sends input (move/look/aim/buttons); the server moves the CharacterController, fires the gun,
// throws, applies damage, knocks, revives and kills. NetworkTransform carries the result to every client.
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public struct AgniInput : INetworkSerializable
    {
        public Vector2 move; public float yaw, pitch; public Vector3 aim; public ushort buttons;
        public const ushort Fire = 1, Sprint = 2, Interact = 4, Aim = 8;
        public bool Has(ushort b) => (buttons & b) != 0;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref move); s.SerializeValue(ref yaw); s.SerializeValue(ref pitch); s.SerializeValue(ref aim); s.SerializeValue(ref buttons); }
    }
    public enum LifeState : byte { Alive = 0, Knocked = 1, Dead = 2 }
    public enum HitKind : byte { World = 0, Body = 1, Head = 2, Wall = 3, None = 4 }

    [RequireComponent(typeof(CharacterController))]
    public partial class AgniPlayer : NetworkBehaviour
    {
        public static readonly List<AgniPlayer> All = new List<AgniPlayer>();
        public static AgniPlayer Local; // this device's own human fighter
        public const float MaxHp = 200f, KnockMax = 100f, BleedPerSec = 4f, ReviveTime = 5f, ReviveRange = 2.3f, HealTime = 3f, HealAmount = 75f;
        public const int MagSize = 30, MedkitMax = 5;
        public const float RunSpeed = 5.6f, SprintSpeed = 6.6f, CrouchSpeed = 2.5f, CrawlSpeed = 1.0f, JumpV = 6.6f, Gravity = 20f;

        public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<int> Team = new NetworkVariable<int>(-1);
        public NetworkVariable<bool> IsBot = new NetworkVariable<bool>();
        public NetworkVariable<float> Hp = new NetworkVariable<float>(MaxHp);
        public NetworkVariable<float> Knock = new NetworkVariable<float>(KnockMax);
        public NetworkVariable<byte> Life = new NetworkVariable<byte>();
        public NetworkVariable<int> Kills = new NetworkVariable<int>();
        public NetworkVariable<int> Ammo = new NetworkVariable<int>(0);
        public NetworkVariable<int> Reserve = new NetworkVariable<int>(90);
        public NetworkVariable<byte> Slot = new NetworkVariable<byte>();          // 0 gun, 1 throwable
        public NetworkVariable<byte> Sel = new NetworkVariable<byte>((byte)ThrowKind.Gola);
        public NetworkVariable<int> Packs = new NetworkVariable<int>();           // throwable counts, 8 bits per kind
        public NetworkVariable<byte> Medkits = new NetworkVariable<byte>();
        public NetworkVariable<bool> Crouched = new NetworkVariable<bool>();
        public NetworkVariable<bool> Reloading = new NetworkVariable<bool>();
        public NetworkVariable<bool> Healing = new NetworkVariable<bool>();
        public NetworkVariable<double> CookStart = new NetworkVariable<double>(-1);
        public NetworkVariable<float> ReviveProg = new NetworkVariable<float>();
        public NetworkVariable<float> Pitch = new NetworkVariable<float>();
        public NetworkVariable<int> Placement = new NetworkVariable<int>();

        public bool Alive => Life.Value == (byte)LifeState.Alive;
        public bool Knocked => Life.Value == (byte)LifeState.Knocked;
        public bool Dead => Life.Value == (byte)LifeState.Dead;
        public string Nick => PlayerName.Value.ToString();
        public int Count(ThrowKind k) => (Packs.Value >> (8 * ((int)k - 1))) & 0xFF;
        public Vector3 Eye => transform.position + Vector3.up * (Knocked ? 0.6f : Crouched.Value ? 1.15f : 1.55f);
        public Vector3 Chest => transform.position + Vector3.up * (Knocked ? 0.45f : Crouched.Value ? 0.85f : 1.2f);

        // ---- server state
        [System.NonSerialized] public AgniInput input; [System.NonSerialized] public AgniBotBrain brain;
        CharacterController cc; float vy, fireCd, bloom, reloadT, healT, pickT, punchArm; bool cooking; double cookT; ushort lastButtons;
        AgniPlayer reviveTarget; AgniPlayer lastKnocker; [System.NonSerialized] public float blindUntil; [System.NonSerialized] public float lastHurt;
        [System.NonSerialized] public AgniCharacterView view;

        void Awake() { cc = GetComponent<CharacterController>(); }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (NetworkManager.IsClient) view = AgniCharacterView.Create(this);
            if (IsOwner && !IsBot.Value && NetworkManager.IsClient) { Local = this; AgniLocalPlayer.Create(this); }
            Life.OnValueChanged += (a, b) => { if (view != null) view.OnLife((LifeState)a, (LifeState)b); };
            if (!IsServer) cc.enabled = true; // keep the capsule for client-side raycasts (arc preview, camera)
        }
        public override void OnNetworkDespawn() { All.Remove(this); if (Local == this) Local = null; }

        // ============ input from the owner
        public void SendInput(AgniInput i) { if (IsServer) input = i; else InputRpc(i); }
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void InputRpc(AgniInput i, RpcParams p = default) { if (p.Receive.SenderClientId == OwnerClientId) input = i; }
        [Rpc(SendTo.Server)] public void JumpRpc(RpcParams p = default) { if (Own(p)) ServerJump(); }
        [Rpc(SendTo.Server)] public void CrouchRpc(RpcParams p = default) { if (Own(p)) ServerCrouch(); }
        [Rpc(SendTo.Server)] public void ReloadRpc(RpcParams p = default) { if (Own(p)) ServerReload(); }
        [Rpc(SendTo.Server)] public void HealRpc(RpcParams p = default) { if (Own(p)) ServerHeal(); }
        [Rpc(SendTo.Server)] public void SlotRpc(byte slot, RpcParams p = default) { if (Own(p)) ServerSlot(slot); }
        [Rpc(SendTo.Server)] public void SelectThrowRpc(byte kind, RpcParams p = default) { if (Own(p)) ServerSelect((ThrowKind)kind); }
        bool Own(RpcParams p) => p.Receive.SenderClientId == OwnerClientId;

        // ============ server simulation
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) ServerTick(Time.deltaTime);
        }

        void ServerTick(float dt)
        {
            if (IsBot.Value && brain != null) input = brain.Think(this, dt);
            if (Dead) return;
            var m = AgniMatch.Instance; bool frozen = m != null && m.Phase.Value == (byte)MatchPhase.Ended;
            var inp = input; if (frozen) { inp.move = Vector2.zero; inp.buttons = 0; }
            transform.rotation = Quaternion.Euler(0, inp.yaw, 0);
            if (Mathf.Abs(Mathf.DeltaAngle(Pitch.Value, inp.pitch)) > 1f) Pitch.Value = inp.pitch;
            bool fireHeld = inp.Has(AgniInput.Fire);

            // --- move
            float speed = Knocked ? CrawlSpeed : Crouched.Value ? CrouchSpeed : (inp.Has(AgniInput.Sprint) && inp.move.y > 0.5f && !fireHeld) ? SprintSpeed : RunSpeed;
            if (Healing.Value) speed *= 0.45f; if (inp.Has(AgniInput.Aim) && !Knocked) speed *= 0.6f; if (reviveTarget != null) speed = 0;
            var mv = Vector2.ClampMagnitude(inp.move, 1f);
            var wish = Quaternion.Euler(0, inp.yaw, 0) * new Vector3(mv.x, 0, mv.y) * speed;
            if (cc.isGrounded && vy < 0) vy = -2f; vy -= Gravity * dt;
            cc.Move((wish + Vector3.up * vy) * dt);
            if (transform.position.y < -40f) { cc.enabled = false; transform.position = AgniMatch.SafeSpot(transform.position); cc.enabled = true; vy = 0; }

            // --- heal
            if (Healing.Value)
            {
                healT -= dt; if (fireHeld || !Alive) { Healing.Value = false; }
                else if (healT <= 0) { Healing.Value = false; FinishHeal(); }
            }
            // --- Josh Drink: slow regen
            if (Boost.Value > 0 && Alive) { float add = Mathf.Min(Boost.Value, 3.5f * dt); Boost.Value -= add; Hp.Value = Mathf.Min(MaxHp, Hp.Value + add); }
            // --- knocked: bleed out
            if (Knocked)
            {
                Knock.Value -= BleedPerSec * dt;
                if (Knock.Value <= 0) Die(lastKnocker, "Khoon beh gaya");
                CancelCook(); return;
            }
            // --- gun / throwable
            fireCd -= dt; bloom = Mathf.Max(0, bloom - dt * 2.2f);
            if (Reloading.Value) { reloadT -= dt; if (reloadT <= 0) FinishReload(); }
            if (Slot.Value == 0)
            {
                if (fireHeld && !Healing.Value && !Reloading.Value && fireCd <= 0)
                {
                    if (!HasGunInHand) Punch(inp);
                    else if (Ammo.Value > 0) FireGun(inp); else if (Reserve.Value > 0) ServerReload();
                }
            }
            else ThrowLogic(inp, fireHeld);
            // --- revive a knocked teammate (hold interact)
            ReviveLogic(inp, dt);
            // --- loot
            pickT -= dt; if (pickT <= 0) { pickT = 0.25f; ServerAutoPick(); }
            lastButtons = inp.buttons;
        }

        public void ServerJump() { if (!Alive || Crouched.Value || !cc.isGrounded) return; vy = JumpV; JumpFxRpc(); }
        public void ServerCrouch() { if (!Alive) return; Crouched.Value = !Crouched.Value; cc.height = Crouched.Value ? 1.2f : 1.8f; cc.center = Vector3.up * cc.height * 0.5f; }
        public void ServerReload()
        {
            if (!Alive || Reloading.Value || !HasGunInHand || Ammo.Value >= CurMagSize() || Reserve.Value <= 0) return;
            Reloading.Value = true; reloadT = AgniItems.Gun(Held).reload; ReloadFxRpc();
        }
        void FinishReload()
        {
            Reloading.Value = false; if (!HasGunInHand) return; var v = Inv.Value; var am = AgniItems.Gun(Held).ammo;
            int need = CurMagSize() - Ammo.Value, take = Mathf.Min(need, v.AmmoOf(am)); Ammo.Value += take; v.SetAmmo(am, v.AmmoOf(am) - take); Inv.Value = v; Reserve.Value = v.AmmoOf(am);
        }
        void Punch(AgniInput inp)
        {   // empty hands (classic start): a quick jab, 1.7 m reach
            fireCd = 0.45f; var eye = Eye; var dir = Quaternion.Euler(Mathf.Clamp(inp.pitch, -30, 30), inp.yaw, 0) * Vector3.forward;
            foreach (var h in Physics.SphereCastAll(eye, 0.28f, dir, 1.6f, ~0, QueryTriggerInteraction.Collide))
            {
                var pl = h.collider.GetComponentInParent<AgniPlayer>(); if (pl == null || pl == this) continue;
                pl.ApplyDamage(16f, this, "MUKKA", false, transform.position); break;
            }
            PunchFxRpc();
        }
        public void ServerSlot(byte s) { if (s > 1) return; if (s == 1 && Count((ThrowKind)Sel.Value) == 0) { var k = FirstOwned(); if (k == ThrowKind.None) return; Sel.Value = (byte)k; } CancelCook(); Slot.Value = s; }
        public void ServerSelect(ThrowKind k) { if (k == ThrowKind.None || Count(k) == 0) return; CancelCook(); Sel.Value = (byte)k; Slot.Value = 1; }
        public ThrowKind FirstOwned() { foreach (var k in ThrowMath.All) if (Count(k) > 0) return k; return ThrowKind.None; }
        public void ServerAddThrowable(ThrowKind k, int n)
        {
            int cur = Count(k), max = ThrowMath.Def(k).carryMax, v = Mathf.Clamp(cur + n, 0, max), sh = 8 * ((int)k - 1);
            Packs.Value = (Packs.Value & ~(0xFF << sh)) | (v << sh);
        }

        void FireGun(AgniInput inp)
        {
            var g = AgniItems.Gun(Held); var v = Inv.Value; int act = v.act;
            bool muzzle = v.Att(act, ItemCat.Muzzle) != ItemId.None, stock = v.Att(act, ItemCat.Stock) != ItemId.None, aiming = inp.Has(AgniInput.Aim);
            Ammo.Value--; fireCd = g.interval;
            var eye = Eye; var baseDir = (inp.aim - eye); if (baseDir.sqrMagnitude < 0.01f) baseDir = Quaternion.Euler(inp.pitch, inp.yaw, 0) * Vector3.forward; baseDir.Normalize();
            float moving = Mathf.Clamp01(new Vector2(inp.move.x, inp.move.y).magnitude);
            float spread = (g.spread + moving * 1.3f * (stock ? 0.7f : 1f) + bloom) * (Crouched.Value ? 0.65f : 1f) * (aiming ? 0.45f : 1f) * (IsBot.Value && brain != null ? brain.SpreadMul : 1f);
            if (g.id == ItemId.Shikari && !aiming) spread += 1.6f;           // a bolt rifle from the hip is a guess
            if (Time.time < blindUntil) spread += 6f;
            bloom = Mathf.Min(2.4f, bloom + (g.pellets > 1 ? 0.6f : 0.28f) * (muzzle ? 0.7f : 1f));
            for (int pi = 0; pi < g.pellets; pi++)
            {
                float sp = g.pellets > 1 ? g.spread * (aiming ? 0.8f : 1f) : spread;
                var dir = Quaternion.AngleAxis(Random.Range(-sp, sp), Vector3.up) * Quaternion.AngleAxis(Random.Range(-sp, sp) * 0.7f, Vector3.Cross(baseDir, Vector3.up)) * baseDir;
                var hits = Physics.RaycastAll(eye, dir, g.range, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                Vector3 end = eye + dir * g.range; HitKind hk = HitKind.None;
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    var pl = h.collider.GetComponentInParent<AgniPlayer>();
                    if (h.collider.isTrigger && (pl == null || h.collider.name != "Head")) continue;
                    if (pl != null && pl.Dead) continue;
                    end = h.point;
                    if (pl != null)
                    {
                        bool head = h.collider.name == "Head"; hk = head ? HitKind.Head : HitKind.Body;
                        float dmg = g.dmg * (head ? g.headMul : 1f) * Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(g.fall0, g.fall1, h.distance));
                        pl.ApplyDamage(dmg, this, g.name, head, transform.position);
                    }
                    else { var w = h.collider.GetComponentInParent<AgniWall>(); if (w != null) { hk = HitKind.Wall; w.ServerDamage(g.dmg); } else hk = HitKind.World; }
                    break;
                }
                if (pi < 3) ShotFxRpc(eye + dir * 0.6f, end, (byte)hk);
            }
        }

        void ThrowLogic(AgniInput inp, bool held)
        {
            var k = (ThrowKind)Sel.Value; if (Count(k) == 0) { CancelCook(); var f = FirstOwned(); if (f == ThrowKind.None) { Slot.Value = 0; return; } Sel.Value = (byte)f; return; }
            var d = ThrowMath.Def(k); double now = NetworkManager.ServerTime.Time;
            if (held && !cooking && fireCd <= 0 && !Healing.Value) { cooking = true; cookT = now; CookStart.Value = d.cookable ? now : -1; CookFxRpc((byte)k); }
            if (!cooking) return;
            if (d.cookable && now - cookT >= d.fuse) { DoThrow(inp, k, 0f, true); return; } // cooked too long: goes off in hand
            if (!held) DoThrow(inp, k, d.cookable ? (float)(now - cookT) : 0f, false);
        }
        void DoThrow(AgniInput inp, ThrowKind k, float cooked, bool inHand)
        {
            cooking = false; CookStart.Value = -1; fireCd = 0.6f; ServerAddThrowable(k, -1);
            var d = ThrowMath.Def(k);
            ThrowMath.Launch(transform.position, inp.yaw, inp.pitch, k, 1f, out var origin, out var vel);
            if (inHand) { origin = Chest + transform.forward * 0.3f; vel = Vector3.zero; }
            var prefab = Resources.Load<GameObject>("AgniNet/AgniThrowable");
            var go = Instantiate(prefab, origin, Quaternion.identity); var t = go.GetComponent<AgniThrowable>();
            t.Kind.Value = (byte)k; t.thrower = this; t.throwYaw = inp.yaw;
            t.FuseEnd.Value = NetworkManager.ServerTime.Time + Mathf.Max(0.02f, d.fuse - cooked);
            go.GetComponent<NetworkObject>().Spawn(true); t.ServerLaunch(vel);
            ThrowFxRpc((byte)k);
            if (Count(k) == 0) { var f = FirstOwned(); if (f != ThrowKind.None) Sel.Value = (byte)f; else Slot.Value = 0; }
        }
        void CancelCook() { if (cooking) { cooking = false; CookStart.Value = -1; } }

        void ReviveLogic(AgniInput inp, float dt)
        {
            AgniPlayer best = null;
            if (inp.Has(AgniInput.Interact) && Alive && !Healing.Value)
            {
                float bd = ReviveRange;
                foreach (var o in All) if (o != this && o.Knocked && o.Team.Value == Team.Value) { float dd = Vector3.Distance(o.transform.position, transform.position); if (dd < bd) { bd = dd; best = o; } }
            }
            if (best != reviveTarget) { if (reviveTarget != null) reviveTarget.ReviveProg.Value = 0; reviveTarget = best; }
            if (reviveTarget == null) return;
            reviveTarget.ReviveProg.Value += dt / ReviveTime;
            if (reviveTarget.ReviveProg.Value >= 1f)
            {
                var r = reviveTarget; reviveTarget = null; r.ReviveProg.Value = 0; r.Life.Value = (byte)LifeState.Alive; r.Hp.Value = 40f; r.Knock.Value = KnockMax;
                AgniMatch.Instance?.Feed(Nick, r.Nick, "uthaya", 2);
            }
        }

        // ============ damage (server)
        public void ApplyDamage(float dmg, AgniPlayer by, string weapon, bool head, Vector3 from)
        {
            if (!IsServer || Dead) return;
            var m = AgniMatch.Instance; if (m == null || m.Phase.Value != (byte)MatchPhase.Playing) return;
            if (by != null && by != this && by.Team.Value == Team.Value) return; // no friendly fire
            lastHurt = Time.time;
            if (by != null && by != this) { by.HitConfirmRpc(dmg, head, false); if (brain != null) brain.OnHurt(by); }
            HurtRpc(from, dmg);
            if (Knocked) { Knock.Value -= dmg; if (Knock.Value <= 0) Die(by ?? lastKnocker, weapon); return; }
            dmg = ArmourCut(dmg, head);
            Hp.Value -= dmg; Healing.Value = false;
            if (Hp.Value > 0) return;
            Hp.Value = 0;
            if (TeammateStanding()) { Life.Value = (byte)LifeState.Knocked; Knock.Value = KnockMax; lastKnocker = by; CancelCook(); Reloading.Value = false; if (Crouched.Value) ServerCrouch(); m.Feed(by != null ? by.Nick : "Agni Rekha", Nick, weapon, 1); if (by != null) by.HitConfirmRpc(0, head, true); AgniMatch.Instance.CheckTeam(Team.Value); }
            else Die(by, weapon);
        }
        bool TeammateStanding() { foreach (var o in All) if (o != this && o.Team.Value == Team.Value && o.Alive) return true; return false; }
        public void Die(AgniPlayer by, string weapon)
        {
            if (Dead) return;
            Life.Value = (byte)LifeState.Dead; Hp.Value = 0; Knock.Value = 0; Healing.Value = false; CancelCook();
            if (by != null && by != this && by.Team.Value != Team.Value) { by.Kills.Value++; by.HitConfirmRpc(0, false, true); }
            ServerDeathCrate();
            var m = AgniMatch.Instance; if (m != null) { m.Feed(by != null ? by.Nick : "Agni Rekha", Nick, weapon, 0); m.OnDeath(this); }
            cc.enabled = false; // corpse does not block
        }
        public void ServerResetForMatch(Vector3 pos, float yaw)
        {
            cc.enabled = false; transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); cc.enabled = true; vy = 0;
            Life.Value = (byte)LifeState.Alive; Hp.Value = MaxHp; Knock.Value = KnockMax; ServerResetInventory();
            Crouched.Value = false; cc.height = 1.8f; cc.center = Vector3.up * 0.9f; Reloading.Value = false; Healing.Value = false; Slot.Value = 0; CookStart.Value = -1;
            input.yaw = yaw; input.pitch = 0; input.move = Vector2.zero; input.buttons = 0; input.aim = pos + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 50f + Vector3.up * 1.5f;
            if (!IsBot.Value) TeleportedRpc(yaw);
        }

        // ============ client-side feedback (RPCs from the server)
        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)] void ShotFxRpc(Vector3 from, Vector3 to, byte hit) { if (view != null) view.OnShot(); AgniFx.Shot(from, to, (HitKind)hit, this == Local); }
        [Rpc(SendTo.ClientsAndHost)] void ThrowFxRpc(byte kind) { if (view != null) view.PlayThrow((ThrowKind)kind); }
        [Rpc(SendTo.ClientsAndHost)] void CookFxRpc(byte kind) { if (view != null) view.PlayPullPin((ThrowKind)kind); if (this == Local) AgniFx.PinPull(transform.position); }
        [Rpc(SendTo.ClientsAndHost)] void ReloadFxRpc() { if (view != null) view.PlayReload(); }
        [Rpc(SendTo.ClientsAndHost)] void JumpFxRpc() { if (view != null) view.PlayJump(); }
        [Rpc(SendTo.Owner)] public void HitConfirmRpc(float dmg, bool head, bool knockOrKill) { if (this == Local) AgniLocalPlayer.Instance?.OnHitConfirm(dmg, head, knockOrKill); }
        [Rpc(SendTo.Owner)] public void HurtRpc(Vector3 from, float dmg) { if (this == Local) AgniLocalPlayer.Instance?.OnHurt(from, dmg); }
        [Rpc(SendTo.Owner)] public void FlashedRpc(float intensity, float seconds) { if (this == Local) AgniLocalPlayer.Instance?.OnFlashed(intensity, seconds); }
        [Rpc(SendTo.Owner)] public void PickedRpc(byte kind, ushort amount) { if (this == Local) AgniLocalPlayer.Instance?.OnPicked((ItemId)kind, amount); }
        [Rpc(SendTo.Owner)] public void PickFailRpc(byte kind) { if (this == Local) AgniLocalPlayer.Instance?.OnPickFail((ItemId)kind); }
        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)] void PunchFxRpc() { if (view != null) view.PlayPunch(); }
        [Rpc(SendTo.Owner)] public void TeleportedRpc(float yaw) { if (this == Local) AgniLocalPlayer.Instance?.OnTeleported(yaw); }
    }
}
