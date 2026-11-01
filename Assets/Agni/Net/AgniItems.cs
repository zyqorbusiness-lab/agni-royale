// Agni Royale — LOOT v1: every item on the island (our own names), guns, ammo, armour, bags, heals, attachments,
// rarity tables per building tier, bag weights. Pure data + maths, shared by server and clients.
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum ItemId : byte
    {
        None = 0,
        Gola = 1, Dhuan = 2, Patakha = 3, Deewar = 4,                    // throwables keep their ThrowKind numbers
        Agni47 = 10, Bijli = 11, Dhamaka = 12, Shikari = 13, Chota = 14, Bhari = 15,
        GoliRifle = 20, GoliSmg = 21, Chharra = 22, GoliSniper = 23,
        Kavach1 = 30, Kavach2 = 31, Kavach3 = 32, Helmet1 = 33, Helmet2 = 34, Helmet3 = 35, Jhola1 = 36, Jhola2 = 37, Jhola3 = 38,
        Patti = 40, DawaKit = 41, JoshDrink = 42,
        Scope2x = 50, Scope4x = 51, Scope8x = 52, Nali = 53, LambaMag = 54, Stock = 55,
    }
    public enum ItemCat : byte { None, Throwable, Gun, Ammo, Vest, Helmet, Bag, Heal, Scope, Muzzle, Mag, Stock }
    public enum Rarity : byte { Common, Uncommon, Rare, Epic }

    public struct ItemDef
    {
        public ItemId id; public string name; public ItemCat cat; public Rarity rarity; public float weight; public int stack; public int level;
        public ItemDef(ItemId id, string name, ItemCat cat, Rarity r, float weight, int stack, int level = 0) { this.id = id; this.name = name; this.cat = cat; rarity = r; this.weight = weight; this.stack = stack; this.level = level; }
    }

    public struct GunDef
    {
        public ItemId id; public string name, model, cls; public ItemId ammo; public int mag, pellets; public float interval, dmg, headMul, spread, fall0, fall1, reload, range, length;
        public bool scopeOk, muzzleOk, magOk, stockOk; public int builtInZoom;
    }

    /// One stack of loot: what and how many (guns: rounds loaded; vest/helmet: durability left).
    public struct LootStack : INetworkSerializable, System.IEquatable<LootStack>
    {
        public byte item; public ushort amount;
        public LootStack(ItemId i, int a) { item = (byte)i; amount = (ushort)Mathf.Clamp(a, 0, 65535); }
        public ItemId Id => (ItemId)item;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter { s.SerializeValue(ref item); s.SerializeValue(ref amount); }
        public bool Equals(LootStack o) => item == o.item && amount == o.amount;
        public override int GetHashCode() => item * 65536 + amount;
    }

    public static class AgniItems
    {
        static readonly ItemDef[] defs = new ItemDef[64];
        static readonly GunDef[] guns = new GunDef[16];
        static AgniItems()
        {
            void D(ItemId i, string n, ItemCat c, Rarity r, float w, int st, int lv = 0) => defs[(int)i] = new ItemDef(i, n, c, r, w, st, lv);
            D(ItemId.Gola, "Gola", ItemCat.Throwable, Rarity.Uncommon, 12, 1); D(ItemId.Dhuan, "Dhuan", ItemCat.Throwable, Rarity.Common, 10, 1);
            D(ItemId.Patakha, "Patakha", ItemCat.Throwable, Rarity.Uncommon, 10, 1); D(ItemId.Deewar, "Agni Deewar", ItemCat.Throwable, Rarity.Rare, 12, 2);
            D(ItemId.Agni47, "AGNI-47", ItemCat.Gun, Rarity.Rare, 0, 1); D(ItemId.Bijli, "BIJLI SMG", ItemCat.Gun, Rarity.Uncommon, 0, 1);
            D(ItemId.Dhamaka, "DHAMAKA-12", ItemCat.Gun, Rarity.Uncommon, 0, 1); D(ItemId.Shikari, "SHIKARI-98", ItemCat.Gun, Rarity.Epic, 0, 1);
            D(ItemId.Chota, "CHOTA 9", ItemCat.Gun, Rarity.Common, 0, 1); D(ItemId.Bhari, "BHARI-100", ItemCat.Gun, Rarity.Epic, 0, 1);
            D(ItemId.GoliRifle, "Rifle Goli", ItemCat.Ammo, Rarity.Common, 0.5f, 30); D(ItemId.GoliSmg, "SMG Goli", ItemCat.Ammo, Rarity.Common, 0.35f, 40);
            D(ItemId.Chharra, "Chharra", ItemCat.Ammo, Rarity.Common, 1.25f, 10); D(ItemId.GoliSniper, "Sniper Goli", ItemCat.Ammo, Rarity.Uncommon, 1f, 10);
            D(ItemId.Kavach1, "Kavach Lv1", ItemCat.Vest, Rarity.Common, 0, 1, 1); D(ItemId.Kavach2, "Kavach Lv2", ItemCat.Vest, Rarity.Uncommon, 0, 1, 2); D(ItemId.Kavach3, "Kavach Lv3", ItemCat.Vest, Rarity.Rare, 0, 1, 3);
            D(ItemId.Helmet1, "Helmet Lv1", ItemCat.Helmet, Rarity.Common, 0, 1, 1); D(ItemId.Helmet2, "Helmet Lv2", ItemCat.Helmet, Rarity.Uncommon, 0, 1, 2); D(ItemId.Helmet3, "Helmet Lv3", ItemCat.Helmet, Rarity.Rare, 0, 1, 3);
            D(ItemId.Jhola1, "Jhola Lv1", ItemCat.Bag, Rarity.Common, 0, 1, 1); D(ItemId.Jhola2, "Jhola Lv2", ItemCat.Bag, Rarity.Uncommon, 0, 1, 2); D(ItemId.Jhola3, "Jhola Lv3", ItemCat.Bag, Rarity.Rare, 0, 1, 3);
            D(ItemId.Patti, "Patti", ItemCat.Heal, Rarity.Common, 2, 5); D(ItemId.DawaKit, "Dawa Kit", ItemCat.Heal, Rarity.Rare, 18, 1); D(ItemId.JoshDrink, "Josh Drink", ItemCat.Heal, Rarity.Uncommon, 4, 1);
            D(ItemId.Scope2x, "Doorbeen 2x", ItemCat.Scope, Rarity.Uncommon, 8, 1, 2); D(ItemId.Scope4x, "Doorbeen 4x", ItemCat.Scope, Rarity.Rare, 10, 1, 4); D(ItemId.Scope8x, "Doorbeen 8x", ItemCat.Scope, Rarity.Epic, 12, 1, 8);
            D(ItemId.Nali, "Thandi Nali", ItemCat.Muzzle, Rarity.Uncommon, 8, 1); D(ItemId.LambaMag, "Lamba Mag", ItemCat.Mag, Rarity.Uncommon, 8, 1); D(ItemId.Stock, "Kandha Stock", ItemCat.Stock, Rarity.Uncommon, 8, 1);

            void G(ItemId i, string model, string cls, ItemId ammo, int mag, float interval, float dmg, float spread, float f0, float f1, float reload, float range, int pellets = 1, float headMul = 1.75f, bool sc = true, bool mz = true, bool mg = true, bool st = false, int zoom = 0, float len = 0.9f)
                => guns[(int)i - 10] = new GunDef { id = i, name = defs[(int)i].name, model = model, cls = cls, ammo = ammo, mag = mag, interval = interval, dmg = dmg, spread = spread, fall0 = f0, fall1 = f1, reload = reload, range = range, pellets = pellets, headMul = headMul, scopeOk = sc, muzzleOk = mz, magOk = mg, stockOk = st, builtInZoom = zoom, length = len };
            G(ItemId.Agni47, "AGNI_47", "Rifle", ItemId.GoliRifle, 30, 0.11f, 34, 0.55f, 50, 150, 2.2f, 320, st: true, len: 1.0f);
            G(ItemId.Bijli, "BIJLI_SMG", "SMG", ItemId.GoliSmg, 32, 0.075f, 22, 0.8f, 20, 70, 1.8f, 200, st: true, len: 0.64f);
            G(ItemId.Dhamaka, "DHAMAKA_12", "Shotgun", ItemId.Chharra, 5, 0.9f, 17, 4.2f, 6, 26, 3.0f, 60, pellets: 8, headMul: 1.4f, sc: false, mg: false, len: 1.0f);
            G(ItemId.Shikari, "SHIKARI_98", "Sniper", ItemId.GoliSniper, 5, 1.4f, 95, 0.12f, 150, 450, 3.2f, 500, headMul: 2.5f, st: true, zoom: 4, len: 1.13f);
            G(ItemId.Chota, "CHOTA", "Pistol", ItemId.GoliSmg, 12, 0.22f, 26, 0.7f, 25, 80, 1.5f, 150, sc: false, len: 0.2f);
            G(ItemId.Bhari, "BHARI_LMG", "LMG", ItemId.GoliRifle, 75, 0.09f, 30, 0.9f, 50, 160, 4.0f, 320, st: false, len: 1.09f);
        }

        public static ItemDef Def(ItemId i) => defs[(int)i];
        public static ItemCat Cat(ItemId i) => (int)i < defs.Length ? defs[(int)i].cat : ItemCat.None;
        public static string Name(ItemId i) => (int)i < defs.Length && defs[(int)i].name != null ? defs[(int)i].name : i.ToString();
        public static bool IsGun(ItemId i) => i >= ItemId.Agni47 && i <= ItemId.Bhari;
        public static GunDef Gun(ItemId i) => guns[Mathf.Clamp((int)i - 10, 0, 5)];
        public static Color RarityColor(Rarity r) { switch (r) { case Rarity.Uncommon: return new Color(0.45f, 0.86f, 0.42f); case Rarity.Rare: return new Color(0.38f, 0.7f, 1f); case Rarity.Epic: return new Color(0.8f, 0.5f, 1f); default: return new Color(0.93f, 0.93f, 0.9f); } }
        public static Color ColorOf(ItemId i) => RarityColor(Def(i).rarity);

        // armour: damage reduction + durability per level
        public static float VestCut(int lv) => lv == 1 ? 0.25f : lv == 2 ? 0.4f : lv == 3 ? 0.55f : 0f;
        public static float HelmetCut(int lv) => lv == 1 ? 0.3f : lv == 2 ? 0.42f : lv == 3 ? 0.55f : 0f;
        public static int VestDur(int lv) => lv == 1 ? 100 : lv == 2 ? 150 : 220;
        public static int HelmetDur(int lv) => lv == 1 ? 80 : lv == 2 ? 150 : 230;
        public static float BagCap(int lv) => lv == 1 ? 170 : lv == 2 ? 220 : lv == 3 ? 270 : 100;
        public static int ScopeZoom(ItemId s) => s == ItemId.Scope2x ? 2 : s == ItemId.Scope4x ? 4 : s == ItemId.Scope8x ? 8 : 0;
        public static int LooseIndex(ItemId i) => i >= ItemId.Scope2x && i <= ItemId.Stock ? (int)i - 50 : -1;
        public static bool Accepts(GunDef g, ItemId a)
        {
            switch (Cat(a)) { case ItemCat.Scope: return g.scopeOk; case ItemCat.Muzzle: return g.muzzleOk; case ItemCat.Mag: return g.magOk; case ItemCat.Stock: return g.stockOk; default: return false; }
        }

        // ---------------------------------------------------------------- loot tables (weights) per building tier
        // tier 0: huts / sheds, tier 1: pakka + chhat houses, tier 2: do-manzila, godown, rooftops
        static readonly (ItemId id, int w)[][] tables = {
            new[] { (ItemId.Chota, 9), (ItemId.Bijli, 7), (ItemId.Dhamaka, 7), (ItemId.Agni47, 3), (ItemId.GoliSmg, 14), (ItemId.Chharra, 10), (ItemId.GoliRifle, 10),
                    (ItemId.Kavach1, 8), (ItemId.Helmet1, 8), (ItemId.Jhola1, 7), (ItemId.Patti, 14), (ItemId.JoshDrink, 4), (ItemId.Dhuan, 4), (ItemId.Gola, 4), (ItemId.Nali, 2), (ItemId.Kavach2, 2), (ItemId.Helmet2, 2) },
            new[] { (ItemId.Agni47, 8), (ItemId.Bijli, 7), (ItemId.Dhamaka, 5), (ItemId.Chota, 3), (ItemId.Shikari, 1), (ItemId.GoliRifle, 13), (ItemId.GoliSmg, 10), (ItemId.Chharra, 6), (ItemId.GoliSniper, 2),
                    (ItemId.Kavach1, 5), (ItemId.Kavach2, 5), (ItemId.Helmet1, 5), (ItemId.Helmet2, 5), (ItemId.Jhola1, 4), (ItemId.Jhola2, 3), (ItemId.Patti, 9), (ItemId.DawaKit, 3), (ItemId.JoshDrink, 5),
                    (ItemId.Scope2x, 4), (ItemId.Scope4x, 1), (ItemId.Nali, 3), (ItemId.LambaMag, 3), (ItemId.Stock, 2), (ItemId.Gola, 4), (ItemId.Dhuan, 3), (ItemId.Patakha, 3), (ItemId.Deewar, 2) },
            new[] { (ItemId.Agni47, 10), (ItemId.Bijli, 5), (ItemId.Shikari, 4), (ItemId.Dhamaka, 3), (ItemId.GoliRifle, 12), (ItemId.GoliSmg, 6), (ItemId.GoliSniper, 5), (ItemId.Chharra, 3),
                    (ItemId.Kavach2, 6), (ItemId.Kavach3, 2), (ItemId.Helmet2, 6), (ItemId.Helmet3, 2), (ItemId.Jhola2, 4), (ItemId.Jhola3, 2), (ItemId.Patti, 6), (ItemId.DawaKit, 5), (ItemId.JoshDrink, 5),
                    (ItemId.Scope2x, 4), (ItemId.Scope4x, 4), (ItemId.Scope8x, 1), (ItemId.Nali, 3), (ItemId.LambaMag, 3), (ItemId.Stock, 3), (ItemId.Gola, 5), (ItemId.Patakha, 3), (ItemId.Deewar, 3) },
        };
        public static ItemId Roll(int tier, System.Random r)
        {
            var t = tables[Mathf.Clamp(tier, 0, 2)]; int sum = 0; foreach (var e in t) sum += e.w;
            int x = r.Next(sum); foreach (var e in t) { if (x < e.w) return e.id; x -= e.w; }
            return t[0].id;
        }
        /// default amount for a fresh stack on the ground
        public static int FreshAmount(ItemId i, System.Random r)
        {
            switch (Cat(i))
            {
                case ItemCat.Gun: return Gun(i).mag;
                case ItemCat.Ammo: return Def(i).stack;
                case ItemCat.Vest: return VestDur(Def(i).level);
                case ItemCat.Helmet: return HelmetDur(Def(i).level);
                case ItemCat.Heal: return i == ItemId.Patti ? 5 : 1;
                case ItemCat.Throwable: return i == ItemId.Deewar ? 2 : 1;
                default: return 1;
            }
        }
    }
}
