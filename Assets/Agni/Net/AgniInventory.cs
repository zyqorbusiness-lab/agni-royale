// Agni Royale — LOOT v1: the fighter's bag (server-authoritative; the owner sees it through an owner-only NetworkVariable),
// pickup rules (manual + auto), drops, armour maths, heals, gun slots + attachments, death crate.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public struct AgniInv : INetworkSerializable, System.IEquatable<AgniInv>
    {
        public byte g0, g1, act;                       // guns (ItemId) in slot 0/1 + which slot is in hand
        public ushort m0, m1;                          // rounds loaded (the in-hand gun's live count is AgniPlayer.Ammo)
        public byte s0, z0, x0, k0, s1, z1, x1, k1;    // scope / muzzle / mag / stock fitted on each gun
        public ushort aR, aS, aC, aN;                  // ammo: rifle, smg, chharra, sniper
        public byte vest, helmet, bag; public ushort vestDur, helmetDur;
        public byte patti, dawa, josh;
        public byte l0, l1, l2, l3, l4, l5;            // loose attachments in the bag: 2x 4x 8x nali mag stock

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref g0); s.SerializeValue(ref g1); s.SerializeValue(ref act); s.SerializeValue(ref m0); s.SerializeValue(ref m1);
            s.SerializeValue(ref s0); s.SerializeValue(ref z0); s.SerializeValue(ref x0); s.SerializeValue(ref k0); s.SerializeValue(ref s1); s.SerializeValue(ref z1); s.SerializeValue(ref x1); s.SerializeValue(ref k1);
            s.SerializeValue(ref aR); s.SerializeValue(ref aS); s.SerializeValue(ref aC); s.SerializeValue(ref aN);
            s.SerializeValue(ref vest); s.SerializeValue(ref helmet); s.SerializeValue(ref bag); s.SerializeValue(ref vestDur); s.SerializeValue(ref helmetDur);
            s.SerializeValue(ref patti); s.SerializeValue(ref dawa); s.SerializeValue(ref josh);
            s.SerializeValue(ref l0); s.SerializeValue(ref l1); s.SerializeValue(ref l2); s.SerializeValue(ref l3); s.SerializeValue(ref l4); s.SerializeValue(ref l5);
        }
        public bool Equals(AgniInv o) =>
            g0 == o.g0 && g1 == o.g1 && act == o.act && m0 == o.m0 && m1 == o.m1 && s0 == o.s0 && z0 == o.z0 && x0 == o.x0 && k0 == o.k0 && s1 == o.s1 && z1 == o.z1 && x1 == o.x1 && k1 == o.k1 &&
            aR == o.aR && aS == o.aS && aC == o.aC && aN == o.aN && vest == o.vest && helmet == o.helmet && bag == o.bag && vestDur == o.vestDur && helmetDur == o.helmetDur &&
            patti == o.patti && dawa == o.dawa && josh == o.josh && l0 == o.l0 && l1 == o.l1 && l2 == o.l2 && l3 == o.l3 && l4 == o.l4 && l5 == o.l5;
        public override int GetHashCode() => g0 ^ (g1 << 8) ^ (aR << 4) ^ (aS << 12) ^ vest ^ (helmet << 2) ^ (bag << 6) ^ patti ^ (dawa << 3);

        public ItemId Gun(int i) => (ItemId)(i == 0 ? g0 : g1);
        public void SetGun(int i, ItemId g) { if (i == 0) g0 = (byte)g; else g1 = (byte)g; }
        public int Mag(int i) => i == 0 ? m0 : m1;
        public void SetMag(int i, int v) { if (i == 0) m0 = (ushort)Mathf.Max(0, v); else m1 = (ushort)Mathf.Max(0, v); }
        public ItemId Att(int gun, ItemCat c)
        {
            switch (c) { case ItemCat.Scope: return (ItemId)(gun == 0 ? s0 : s1); case ItemCat.Muzzle: return (ItemId)(gun == 0 ? z0 : z1); case ItemCat.Mag: return (ItemId)(gun == 0 ? x0 : x1); case ItemCat.Stock: return (ItemId)(gun == 0 ? k0 : k1); }
            return ItemId.None;
        }
        public void SetAtt(int gun, ItemCat c, ItemId v)
        {
            byte b = (byte)v;
            switch (c) { case ItemCat.Scope: if (gun == 0) s0 = b; else s1 = b; break; case ItemCat.Muzzle: if (gun == 0) z0 = b; else z1 = b; break; case ItemCat.Mag: if (gun == 0) x0 = b; else x1 = b; break; case ItemCat.Stock: if (gun == 0) k0 = b; else k1 = b; break; }
        }
        public int AmmoOf(ItemId a) => a == ItemId.GoliRifle ? aR : a == ItemId.GoliSmg ? aS : a == ItemId.Chharra ? aC : a == ItemId.GoliSniper ? aN : 0;
        public void SetAmmo(ItemId a, int v)
        {
            ushort u = (ushort)Mathf.Clamp(v, 0, 9999);
            if (a == ItemId.GoliRifle) aR = u; else if (a == ItemId.GoliSmg) aS = u; else if (a == ItemId.Chharra) aC = u; else if (a == ItemId.GoliSniper) aN = u;
        }
        public int Loose(int k) => k == 0 ? l0 : k == 1 ? l1 : k == 2 ? l2 : k == 3 ? l3 : k == 4 ? l4 : l5;
        public void SetLoose(int k, int v) { byte b = (byte)Mathf.Clamp(v, 0, 99); if (k == 0) l0 = b; else if (k == 1) l1 = b; else if (k == 2) l2 = b; else if (k == 3) l3 = b; else if (k == 4) l4 = b; else l5 = b; }
        public int Heals(ItemId h) => h == ItemId.Patti ? patti : h == ItemId.DawaKit ? dawa : h == ItemId.JoshDrink ? josh : 0;
        public void SetHeals(ItemId h, int v) { byte b = (byte)Mathf.Clamp(v, 0, 99); if (h == ItemId.Patti) patti = b; else if (h == ItemId.DawaKit) dawa = b; else if (h == ItemId.JoshDrink) josh = b; }
        public int MagSize(int i) { var g = Gun(i); if (g == ItemId.None) return 0; int m = AgniItems.Gun(g).mag; return Att(i, ItemCat.Mag) != ItemId.None ? Mathf.RoundToInt(m * 1.5f) : m; }
    }

    public partial class AgniPlayer
    {
        public NetworkVariable<AgniInv> Inv = new NetworkVariable<AgniInv>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<byte> HeldGun = new NetworkVariable<byte>();      // the gun in hand (0 = fists), seen by everyone
        public NetworkVariable<byte> Armour = new NetworkVariable<byte>();       // vest level | helmet level << 4 (everyone: hit colours)
        public NetworkVariable<byte> HealItem = new NetworkVariable<byte>();
        public NetworkVariable<double> HealEnd = new NetworkVariable<double>();
        public NetworkVariable<float> HealLen = new NetworkVariable<float>();
        public NetworkVariable<float> Boost = new NetworkVariable<float>();      // Josh Drink regen left (HP)
        public NetworkVariable<bool> AutoPick = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public ItemId Held => (ItemId)HeldGun.Value;
        public bool HasGunInHand => HeldGun.Value != 0;
        public const float PickRange = 2.6f;

        [Rpc(SendTo.Server)] public void PickRpc(ulong netId, byte index, RpcParams p = default) { if (Own(p)) ServerPickManual(netId, index); }
        [Rpc(SendTo.Server)] public void DropRpc(byte item, ushort amount, RpcParams p = default) { if (Own(p)) ServerDrop((ItemId)item, amount); }
        [Rpc(SendTo.Server)] public void UseHealRpc(byte item, RpcParams p = default) { if (Own(p)) ServerHeal((ItemId)item); }
        [Rpc(SendTo.Server)] public void SwapGunRpc(RpcParams p = default) { if (Own(p)) ServerSwapGun(); }
        [Rpc(SendTo.Server)] public void AutoPickRpc(bool on, RpcParams p = default) { if (Own(p)) AutoPick.Value = on; }
        [Rpc(SendTo.Server)] public void AttachRpc(byte item, byte gun, RpcParams p = default) { if (Own(p)) ServerAttachLoose((ItemId)item, gun); }

        // ------------------------------------------------------------ weights
        public float BagWeight(AgniInv v)
        {
            float w = 0;
            foreach (var a in new[] { ItemId.GoliRifle, ItemId.GoliSmg, ItemId.Chharra, ItemId.GoliSniper }) w += v.AmmoOf(a) * AgniItems.Def(a).weight;
            foreach (var h in new[] { ItemId.Patti, ItemId.DawaKit, ItemId.JoshDrink }) w += v.Heals(h) * AgniItems.Def(h).weight;
            foreach (var k in ThrowMath.All) w += Count(k) * AgniItems.Def((ItemId)(byte)k).weight;
            for (int i = 0; i < 6; i++) w += v.Loose(i) * AgniItems.Def((ItemId)(50 + i)).weight;
            return w;
        }
        public float BagCap(AgniInv v) => AgniItems.BagCap(v.bag);

        // ------------------------------------------------------------ in-hand gun bookkeeping
        /// after any change to guns/ammo: publish the in-hand gun, its live mag and its reserve
        void SyncHeld(ref AgniInv v, bool loadMag)
        {
            var g = v.Gun(v.act);
            if (g == ItemId.None && v.Gun(1 - v.act) != ItemId.None) { v.act = (byte)(1 - v.act); g = v.Gun(v.act); loadMag = true; }
            HeldGun.Value = (byte)g;
            if (loadMag) Ammo.Value = g == ItemId.None ? 0 : v.Mag(v.act);
            Reserve.Value = g == ItemId.None ? 0 : v.AmmoOf(AgniItems.Gun(g).ammo);
        }
        void StoreLiveMag(ref AgniInv v) { if (v.Gun(v.act) != ItemId.None) v.SetMag(v.act, Ammo.Value); }
        public void ServerSwapGun()
        {
            if (!Alive) return; var v = Inv.Value; if (v.Gun(1 - v.act) == ItemId.None) { if (Slot.Value != 0) ServerSlot(0); return; }
            StoreLiveMag(ref v); v.act = (byte)(1 - v.act); Reloading.Value = false; Inv.Value = v; SyncHeld(ref v, true); Inv.Value = v; Slot.Value = 0; CancelCook(); ReloadFxRpc();
        }
        int CurMagSize() { var v = Inv.Value; return v.MagSize(v.act); }

        // ------------------------------------------------------------ pickups
        public void ServerPickManual(ulong netId, byte index)
        {
            if (!Alive || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(netId, out var no)) return;
            if ((no.transform.position - transform.position).sqrMagnitude > (PickRange + 0.8f) * (PickRange + 0.8f)) return;
            var pk = no.GetComponent<AgniPickup>(); if (pk != null) { pk.ServerGive(this, true); return; }
            var cr = no.GetComponent<AgniCrate>(); if (cr != null) cr.ServerGive(this, index);
        }
        /// Give 'amount' of an item; returns how much was taken. Replaced gear (old vest, old gun…) goes into 'drops'.
        public int ServerTake(ItemId id, int amount, bool manual, List<LootStack> drops)
        {
            var v = Inv.Value; var cat = AgniItems.Cat(id); int taken = 0; float free = BagCap(v) - BagWeight(v);
            switch (cat)
            {
                case ItemCat.Throwable:
                {
                    var tk = (ThrowKind)(byte)id; int room = ThrowMath.Def(tk).carryMax - Count(tk); float w = AgniItems.Def(id).weight;
                    taken = Mathf.Min(amount, room, Mathf.FloorToInt(free / w)); if (taken > 0) ServerAddThrowable(tk, taken); return taken;
                }
                case ItemCat.Gun:
                {
                    int slot = v.g0 == 0 ? 0 : v.g1 == 0 ? 1 : -1;
                    if (slot < 0 && (v.g0 == (byte)id || v.g1 == (byte)id))
                    {   // same gun already carried: just strip its goli into the bag
                        var am = AgniItems.Gun(id).ammo; int g = Mathf.Min(amount, Mathf.FloorToInt(free / AgniItems.Def(am).weight));
                        if (!manual && g <= 0) return 0; v.SetAmmo(am, v.AmmoOf(am) + Mathf.Max(0, g)); SyncHeld(ref v, false); Inv.Value = v; return 1;
                    }
                    if (slot < 0)
                    {
                        if (!manual) return 0;
                        slot = v.act; StoreLiveMag(ref v); var old = v.Gun(slot);
                        drops.Add(new LootStack(old, v.Mag(slot)));
                        foreach (var c in new[] { ItemCat.Scope, ItemCat.Muzzle, ItemCat.Mag, ItemCat.Stock })
                        {   // attachments move to the new gun when they fit, else back into the bag (or the floor)
                            var a = v.Att(slot, c); if (a == ItemId.None) continue; v.SetAtt(slot, c, ItemId.None);
                            if (AgniItems.Accepts(AgniItems.Gun(id), a)) v.SetAtt(slot, c, a); else { int li = AgniItems.LooseIndex(a); v.SetLoose(li, v.Loose(li) + 1); }
                        }
                    }
                    bool first = v.g0 == 0 && v.g1 == 0;
                    v.SetGun(slot, id); v.SetMag(slot, amount);
                    foreach (var c in new[] { ItemCat.Scope, ItemCat.Muzzle, ItemCat.Mag, ItemCat.Stock }) if (!AgniItems.Accepts(AgniItems.Gun(id), v.Att(slot, c))) v.SetAtt(slot, c, ItemId.None);
                    AutoFitLoose(ref v, slot);
                    if (first || slot == v.act) { v.act = (byte)slot; Inv.Value = v; SyncHeld(ref v, true); Slot.Value = 0; } else SyncHeld(ref v, false);
                    Inv.Value = v; return 1;
                }
                case ItemCat.Ammo:
                {
                    if (!manual && !UsesAmmo(v, id) && free < BagCap(v) * 0.5f) return 0;
                    taken = Mathf.Min(amount, Mathf.FloorToInt(free / AgniItems.Def(id).weight));
                    if (taken <= 0) return 0; v.SetAmmo(id, v.AmmoOf(id) + taken); SyncHeld(ref v, false); Inv.Value = v; return taken;
                }
                case ItemCat.Vest:
                case ItemCat.Helmet:
                case ItemCat.Bag:
                {
                    int lv = AgniItems.Def(id).level; int cur = cat == ItemCat.Vest ? v.vest : cat == ItemCat.Helmet ? v.helmet : v.bag;
                    int curDur = cat == ItemCat.Vest ? v.vestDur : cat == ItemCat.Helmet ? v.helmetDur : 0;
                    bool better = lv > cur || (manual && lv == cur && amount > curDur && cat != ItemCat.Bag);
                    if (!better) return 0;
                    if (cat == ItemCat.Bag && cur > 0 && BagWeight(v) > AgniItems.BagCap(lv)) return 0; // smaller bag can't hold what we carry
                    if (cur > 0) drops.Add(new LootStack(Lv(cat, cur), curDur));
                    if (cat == ItemCat.Vest) { v.vest = (byte)lv; v.vestDur = (ushort)amount; } else if (cat == ItemCat.Helmet) { v.helmet = (byte)lv; v.helmetDur = (ushort)amount; } else v.bag = (byte)lv;
                    Inv.Value = v; Armour.Value = (byte)(v.vest | (v.helmet << 4)); return 1;
                }
                case ItemCat.Heal:
                {
                    int cap = id == ItemId.Patti ? 20 : id == ItemId.DawaKit ? 5 : 8;
                    if (!manual && v.Heals(id) >= cap) return 0;
                    taken = Mathf.Min(amount, Mathf.FloorToInt(free / AgniItems.Def(id).weight)); if (taken <= 0) return 0;
                    v.SetHeals(id, v.Heals(id) + taken); Inv.Value = v; Medkits.Value = (byte)Mathf.Min(255, v.dawa + v.patti + v.josh); return taken;
                }
                default:
                {   // attachments: fit a gun if possible, else into the bag (manual) — auto only when it fits an empty slot or is a better scope
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int gi = pass == 0 ? v.act : 1 - v.act; var g = v.Gun(gi); if (g == ItemId.None || !AgniItems.Accepts(AgniItems.Gun(g), id)) continue;
                        var cur = v.Att(gi, cat);
                        if (cur == ItemId.None) { v.SetAtt(gi, cat, id); FixMag(ref v, gi); Inv.Value = v; return 1; }
                        if (cat == ItemCat.Scope && AgniItems.ScopeZoom(id) > AgniItems.ScopeZoom(cur) && (manual || pass == 0))
                        { v.SetAtt(gi, cat, id); int li0 = AgniItems.LooseIndex(cur); if (free >= AgniItems.Def(cur).weight) v.SetLoose(li0, v.Loose(li0) + 1); else drops.Add(new LootStack(cur, 1)); Inv.Value = v; return 1; }
                    }
                    if (!manual) return 0;
                    if (free < AgniItems.Def(id).weight) return 0;
                    int li = AgniItems.LooseIndex(id); v.SetLoose(li, v.Loose(li) + 1); Inv.Value = v; return 1;
                }
            }
        }
        static ItemId Lv(ItemCat c, int lv) => (ItemId)((c == ItemCat.Vest ? 30 : c == ItemCat.Helmet ? 33 : 36) + lv - 1);
        static bool UsesAmmo(AgniInv v, ItemId a) => (v.g0 != 0 && AgniItems.Gun(v.Gun(0)).ammo == a) || (v.g1 != 0 && AgniItems.Gun(v.Gun(1)).ammo == a);
        void AutoFitLoose(ref AgniInv v, int gi)
        {
            var g = v.Gun(gi); if (g == ItemId.None) return; var gd = AgniItems.Gun(g);
            for (int k = 5; k >= 0; k--)
            {
                if (v.Loose(k) == 0) continue; var a = (ItemId)(50 + k); var c = AgniItems.Cat(a);
                if (!AgniItems.Accepts(gd, a) || v.Att(gi, c) != ItemId.None) continue;
                v.SetAtt(gi, c, a); v.SetLoose(k, v.Loose(k) - 1);
            }
        }
        void FixMag(ref AgniInv v, int gi) { if (gi == v.act && Ammo.Value > v.MagSize(gi)) Ammo.Value = v.MagSize(gi); }
        public void ServerAttachLoose(ItemId a, byte gun)
        {
            var v = Inv.Value; int li = AgniItems.LooseIndex(a); if (li < 0 || v.Loose(li) == 0 || gun > 1) return; var g = v.Gun(gun); if (g == ItemId.None || !AgniItems.Accepts(AgniItems.Gun(g), a)) return;
            var c = AgniItems.Cat(a); var cur = v.Att(gun, c); v.SetLoose(li, v.Loose(li) - 1); v.SetAtt(gun, c, a);
            if (cur != ItemId.None) { int lc = AgniItems.LooseIndex(cur); v.SetLoose(lc, v.Loose(lc) + 1); }
            Inv.Value = v;
        }

        /// auto pickup: every 0.25 s, walk-over items that pass the auto rules
        public void ServerAutoPick()
        {
            if (!Alive || !AutoPick.Value) return;
            var drops = new List<LootStack>();
            for (int i = AgniPickup.Live.Count - 1; i >= 0; i--)
            {
                var pk = AgniPickup.Live[i]; if (pk == null || !pk.IsSpawned) continue;
                var d = pk.transform.position - transform.position; if (d.x * d.x + d.z * d.z > 1.6f * 1.6f || Mathf.Abs(d.y) > 1.6f) continue;
                pk.ServerGive(this, false);
            }
        }

        public void ServerDrop(ItemId id, int amount)
        {
            if (!Alive) return; var v = Inv.Value; var cat = AgniItems.Cat(id); var at = transform.position + transform.forward * 0.8f;
            switch (cat)
            {
                case ItemCat.Ammo: amount = Mathf.Min(amount, v.AmmoOf(id)); if (amount <= 0) return; v.SetAmmo(id, v.AmmoOf(id) - amount); Inv.Value = v; SyncHeld(ref v, false); break;
                case ItemCat.Heal: amount = Mathf.Min(amount, v.Heals(id)); if (amount <= 0) return; v.SetHeals(id, v.Heals(id) - amount); Inv.Value = v; break;
                case ItemCat.Throwable: { var tk = (ThrowKind)(byte)id; amount = Mathf.Min(amount, Count(tk)); if (amount <= 0) return; ServerAddThrowable(tk, -amount); break; }
                case ItemCat.Gun:
                {
                    int slot = v.Gun(v.act) == id ? v.act : v.Gun(1 - v.act) == id ? 1 - v.act : -1; if (slot < 0) return;
                    StoreLiveMag(ref v); amount = v.Mag(slot); v.SetGun(slot, ItemId.None); v.SetMag(slot, 0);
                    foreach (var c in new[] { ItemCat.Scope, ItemCat.Muzzle, ItemCat.Mag, ItemCat.Stock }) { var a = v.Att(slot, c); if (a != ItemId.None) { v.SetAtt(slot, c, ItemId.None); int li = AgniItems.LooseIndex(a); v.SetLoose(li, v.Loose(li) + 1); } }
                    Inv.Value = v; SyncHeld(ref v, true); Inv.Value = v; break;
                }
                case ItemCat.Vest: if (v.vest == 0) return; amount = v.vestDur; id = Lv(cat, v.vest); v.vest = 0; v.vestDur = 0; Inv.Value = v; Armour.Value = (byte)(v.vest | (v.helmet << 4)); break;
                case ItemCat.Helmet: if (v.helmet == 0) return; amount = v.helmetDur; id = Lv(cat, v.helmet); v.helmet = 0; v.helmetDur = 0; Inv.Value = v; Armour.Value = (byte)(v.vest | (v.helmet << 4)); break;
                case ItemCat.Bag: return; // the bag stays on your back in v1
                default: { int li = AgniItems.LooseIndex(id); if (li < 0 || v.Loose(li) == 0) return; v.SetLoose(li, v.Loose(li) - 1); Inv.Value = v; amount = 1; break; }
            }
            AgniPickup.ServerDrop(id, amount, AgniMatch.Ground(at) + Vector3.up * 0.02f);
        }

        // ------------------------------------------------------------ heals
        public void ServerHeal() { var v = Inv.Value; ServerHeal(Hp.Value < 120 && v.dawa > 0 ? ItemId.DawaKit : v.patti > 0 && Hp.Value < 150 ? ItemId.Patti : v.dawa > 0 ? ItemId.DawaKit : ItemId.JoshDrink); }
        public void ServerHeal(ItemId h)
        {
            var v = Inv.Value; if (!Alive || Healing.Value || v.Heals(h) == 0) return;
            if (h == ItemId.Patti && Hp.Value >= 150) return; if (h == ItemId.DawaKit && Hp.Value >= MaxHp) return;
            float len = h == ItemId.Patti ? 3f : h == ItemId.DawaKit ? 5.5f : 2f;
            HealItem.Value = (byte)h; HealLen.Value = len; HealEnd.Value = AgniNet.Now + len; Healing.Value = true; healT = len;
        }
        void FinishHeal()
        {
            var h = (ItemId)HealItem.Value; var v = Inv.Value; if (v.Heals(h) == 0) return;
            v.SetHeals(h, v.Heals(h) - 1); Inv.Value = v; Medkits.Value = (byte)Mathf.Min(255, v.dawa + v.patti + v.josh);
            if (h == ItemId.Patti) Hp.Value = Mathf.Min(150f, Hp.Value + 20f);
            else if (h == ItemId.DawaKit) Hp.Value = Mathf.Min(MaxHp, Hp.Value + 100f);
            else Boost.Value = Mathf.Min(100f, Boost.Value + 40f);
        }

        // ------------------------------------------------------------ armour
        /// cut incoming damage by vest (body) or helmet (head); wears the armour down; returns the damage that reaches HP
        float ArmourCut(float dmg, bool head)
        {
            var v = Inv.Value; bool changed = false;
            if (head && v.helmet > 0) { float cut = AgniItems.HelmetCut(v.helmet); int wear = Mathf.CeilToInt(dmg * 0.6f); v.helmetDur = (ushort)Mathf.Max(0, v.helmetDur - wear); if (v.helmetDur == 0) v.helmet = 0; dmg *= 1 - cut; changed = true; }
            else if (!head && v.vest > 0) { float cut = AgniItems.VestCut(v.vest); int wear = Mathf.CeilToInt(dmg * 0.5f); v.vestDur = (ushort)Mathf.Max(0, v.vestDur - wear); if (v.vestDur == 0) v.vest = 0; dmg *= 1 - cut; changed = true; }
            if (changed) { Inv.Value = v; Armour.Value = (byte)(v.vest | (v.helmet << 4)); }
            return dmg;
        }

        // ------------------------------------------------------------ death crate (tin trunk with everything you carried)
        void ServerDeathCrate()
        {
            var v = Inv.Value; StoreLiveMag(ref v); var L = new List<LootStack>();
            for (int i = 0; i < 2; i++)
            {
                var g = v.Gun(i); if (g == ItemId.None) continue; L.Add(new LootStack(g, v.Mag(i)));
                foreach (var c in new[] { ItemCat.Scope, ItemCat.Muzzle, ItemCat.Mag, ItemCat.Stock }) { var a = v.Att(i, c); if (a != ItemId.None) L.Add(new LootStack(a, 1)); }
            }
            foreach (var a in new[] { ItemId.GoliRifle, ItemId.GoliSmg, ItemId.Chharra, ItemId.GoliSniper }) if (v.AmmoOf(a) > 0) L.Add(new LootStack(a, v.AmmoOf(a)));
            if (v.vest > 0) L.Add(new LootStack(Lv(ItemCat.Vest, v.vest), v.vestDur));
            if (v.helmet > 0) L.Add(new LootStack(Lv(ItemCat.Helmet, v.helmet), v.helmetDur));
            if (v.bag > 0) L.Add(new LootStack(Lv(ItemCat.Bag, v.bag), 1));
            foreach (var h in new[] { ItemId.Patti, ItemId.DawaKit, ItemId.JoshDrink }) if (v.Heals(h) > 0) L.Add(new LootStack(h, v.Heals(h)));
            foreach (var k in ThrowMath.All) if (Count(k) > 0) L.Add(new LootStack((ItemId)(byte)k, Count(k)));
            for (int i = 0; i < 6; i++) if (v.Loose(i) > 0) L.Add(new LootStack((ItemId)(50 + i), v.Loose(i)));
            Inv.Value = default; HeldGun.Value = 0; Ammo.Value = 0; Reserve.Value = 0; Packs.Value = 0; Armour.Value = 0;
            if (L.Count > 0) AgniCrate.ServerSpawnDeath(AgniMatch.Ground(transform.position), Nick, L);
        }

        /// fresh match: empty hands (classic), or a starter for bots / test kit
        /// move without touching life/inventory (tests, future vehicles/zip lines)
        public void ServerTeleport(Vector3 pos, float yaw)
        {
            var c = GetComponent<CharacterController>(); c.enabled = false; transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); c.enabled = true;
            input.yaw = yaw; input.move = Vector2.zero; if (!IsBot.Value) TeleportedRpc(yaw);
        }
        void ServerResetInventory()
        {
            var v = new AgniInv(); HeldGun.Value = 0; Ammo.Value = 0; Reserve.Value = 0; Armour.Value = 0; Boost.Value = 0; Healing.Value = false;
            if (IsBot.Value)
            {
                var g = new[] { ItemId.Chota, ItemId.Bijli, ItemId.Chota, ItemId.Dhamaka, ItemId.Bijli }[Random.Range(0, 5)]; var gd = AgniItems.Gun(g);
                v.g0 = (byte)g; v.SetAmmo(gd.ammo, gd.mag * 2); v.patti = 2; if (Random.value < 0.4f) { v.vest = 1; v.vestDur = 100; }
                v.act = 0; Inv.Value = v; SyncHeld(ref v, true); v.SetMag(0, gd.mag); Ammo.Value = gd.mag;
            }
            else if (AgniNetConfig.Has("-agniKit"))
            {
                v.g0 = (byte)ItemId.Agni47; v.g1 = (byte)ItemId.Shikari; v.s0 = (byte)ItemId.Scope2x; v.s1 = (byte)ItemId.Scope8x; v.aR = 90; v.aN = 10; v.vest = 2; v.vestDur = 150; v.helmet = 2; v.helmetDur = 150; v.bag = 2; v.patti = 3; v.dawa = 1; v.josh = 1;
                v.act = 0; Inv.Value = v; SyncHeld(ref v, true); Ammo.Value = 30;
            }
            Inv.Value = v; Armour.Value = (byte)(v.vest | (v.helmet << 4)); Medkits.Value = (byte)(v.patti + v.dawa + v.josh);
        }
    }

    /// NetworkVariable / NetworkList serializers for our own structs, registered explicitly (the code generator does it too
    /// when it sees them; doing it here as well costs nothing and covers partial classes / builds where it did not).
    public static class AgniNetTypes
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Init()
        {
            NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<AgniInv>();
            NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<AgniInv>();
            NetworkVariableSerializationTypedInitializers.InitializeSerializer_UnmanagedINetworkSerializable<LootStack>();
            NetworkVariableSerializationTypedInitializers.InitializeEqualityChecker_UnmanagedIEquatable<LootStack>();
        }
        /// self-test: write + read an inventory through the NetworkVariable serializer (logged at host start)
        public static string SelfTest()
        {
            try
            {
                var v = new AgniInv { g0 = 10, aR = 77, vestDur = 150 };
                using var w = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
                NetworkVariableSerialization<AgniInv>.Write(w, ref v);
                using var r = new FastBufferReader(w, Unity.Collections.Allocator.Temp);
                var o = new AgniInv(); NetworkVariableSerialization<AgniInv>.Read(r, ref o);
                return o.Equals(v) ? "ok " + w.Length + " bytes" : "MISMATCH";
            }
            catch (System.Exception e) { return "FAIL " + e.GetType().Name + ": " + e.Message; }
        }
    }
}
