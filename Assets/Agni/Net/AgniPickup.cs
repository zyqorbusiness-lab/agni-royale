// Agni Royale — LOOT v1: things on the floor. AgniPickup = one stack (a gun, a box of goli, a vest…);
// AgniCrate = many stacks (death "peti" or the airdrop "Raahat Peti"). The server owns both; clients only draw them.
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public class AgniPickup : NetworkBehaviour
    {
        public static readonly List<AgniPickup> Live = new List<AgniPickup>();
        public NetworkVariable<byte> Kind = new NetworkVariable<byte>();
        public NetworkVariable<ushort> Amount = new NetworkVariable<ushort>(1);
        bool taken; GameObject view;
        public ItemId Id => (ItemId)Kind.Value;

        public override void OnNetworkSpawn() { Live.Add(this); if (NetworkManager.IsClient) view = AgniLootView.Build(Id, transform); }
        public override void OnNetworkDespawn() { Live.Remove(this); }

        static GameObject prefab;
        public static AgniPickup ServerDrop(ItemId id, int amount, Vector3 pos)
        {
            if (prefab == null) prefab = Resources.Load<GameObject>("AgniNet/AgniPickup"); if (prefab == null || id == ItemId.None) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0)); var pk = go.GetComponent<AgniPickup>();
            pk.Kind.Value = (byte)id; pk.Amount.Value = (ushort)Mathf.Clamp(amount, 1, 65535); go.GetComponent<NetworkObject>().Spawn(true); return pk;
        }
        /// classic open-field loot (outside houses): a few low-tier stacks scattered on the grass
        public static void ServerScatter(float radius, int n)
        {
            var r = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < n; i++)
            {
                var p2 = Random.insideUnitCircle * radius * 0.92f; var pos = AgniMatch.Ground(new Vector3(p2.x, 0, p2.y)) + Vector3.up * 0.02f;
                var id = AgniItems.Roll(0, r); ServerDrop(id, AgniItems.FreshAmount(id, r), pos);
            }
        }

        public void ServerGive(AgniPlayer p, bool manual)
        {
            if (taken || !IsSpawned || p == null) return;
            var drops = new List<LootStack>(); int got = p.ServerTake(Id, Amount.Value, manual, drops);
            foreach (var d in drops) ServerDrop(d.Id, d.amount, transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f)));
            if (got <= 0) { if (manual) p.PickFailRpc(Kind.Value); return; }
            p.PickedRpc(Kind.Value, (ushort)got);
            if (AgniLoot.WholeOnly(Id) || got >= Amount.Value) { taken = true; NetworkObject.Despawn(true); }
            else Amount.Value = (ushort)(Amount.Value - got);
        }
    }

    /// Server-side loot director: fills every house spot at match start, scatters field loot, runs the airdrops.
    public static class AgniLoot
    {
        public static bool WholeOnly(ItemId i) { var c = AgniItems.Cat(i); return c == ItemCat.Gun || c == ItemCat.Vest || c == ItemCat.Helmet || c == ItemCat.Bag || c >= ItemCat.Scope; }
        public static int Spawned;
        static readonly List<double> dropTimes = new List<double>(); static int dropsDone;

        public static void ServerSpawnAll(double matchStart)
        {
            var r = new System.Random(System.Environment.TickCount); Spawned = 0;
            if (AgniVillages.Spots.Count == 0) AgniVillages.Build(null);
            foreach (var s in AgniVillages.Spots)
            {
                if (r.NextDouble() > (s.tier == 2 ? 0.85 : 0.72)) continue;
                var id = AgniItems.Roll(s.tier, r); var j = new Vector3((float)r.NextDouble() * 0.5f - 0.25f, 0, (float)r.NextDouble() * 0.5f - 0.25f);
                AgniPickup.ServerDrop(id, AgniItems.FreshAmount(id, r), s.pos + j); Spawned++;
                if (AgniItems.IsGun(id))
                {   // a gun always comes with a little goli for it
                    var am = AgniItems.Gun(id).ammo; int n = s.tier == 0 ? 1 : 2;
                    for (int k = 0; k < n; k++) { var o = Quaternion.Euler(0, (float)r.NextDouble() * 360, 0) * Vector3.forward * (0.45f + 0.2f * k); AgniPickup.ServerDrop(am, AgniItems.FreshAmount(am, r), s.pos + o); Spawned++; }
                }
            }
            AgniPickup.ServerScatter(AgniMatch.ArenaRadius, 36); Spawned += 36;
            // airdrops: two per match (test flag -agniAirdropAt <sec> makes the first one early)
            dropTimes.Clear(); dropsDone = 0; float first = AgniNetConfig.ArgF("-agniAirdropAt", 80f);
            dropTimes.Add(matchStart + first); dropTimes.Add(matchStart + first + 120f);
            Debug.Log("[AgniLoot] spawned " + Spawned + " stacks on " + AgniVillages.Spots.Count + " house spots; airdrops at +" + first + "s, +" + (first + 120) + "s");
        }

        public static void ServerTick(double now)
        {
            if (dropsDone >= dropTimes.Count || now < dropTimes[dropsDone]) return;
            dropsDone++;
            var z = AgniZone.Instance; Vector3 c = z != null && z.PhaseIndex.Value >= 0 ? z.ToC.Value : Vector3.zero; float rr = z != null && z.PhaseIndex.Value >= 0 ? z.ToR.Value * 0.6f : AgniMatch.ArenaRadius * 0.5f;
            Vector3 at = c;
            for (int tries = 0; tries < 20; tries++)
            {   // open ground, not on a roof
                var o = Random.insideUnitCircle * rr; var g = AgniMatch.Ground(new Vector3(c.x + o.x, 0, c.z + o.y));
                if (g.y < 0.6f) { at = g; break; }
                at = g;
            }
            if (AgniNetConfig.Has("-agniAirdropNearMe")) { foreach (var p in AgniPlayer.All) if (!p.IsBot.Value) { at = AgniMatch.Ground(p.transform.position + p.transform.forward * 9f); break; } }
            var items = new List<LootStack>();
            if (Random.value < 0.5f) { items.Add(new LootStack(ItemId.Bhari, 75)); items.Add(new LootStack(ItemId.GoliRifle, 90)); }
            else { items.Add(new LootStack(ItemId.Shikari, 5)); items.Add(new LootStack(ItemId.Scope8x, 1)); items.Add(new LootStack(ItemId.GoliSniper, 20)); }
            items.Add(Random.value < 0.5f ? new LootStack(ItemId.Kavach3, AgniItems.VestDur(3)) : new LootStack(ItemId.Helmet3, AgniItems.HelmetDur(3)));
            items.Add(new LootStack(ItemId.DawaKit, 2)); items.Add(new LootStack(ItemId.JoshDrink, 2));
            if (Random.value < 0.6f) items.Add(new LootStack(ItemId.Jhola3, 1)); else items.Add(new LootStack(ItemId.Scope4x, 1));
            items.Add(new LootStack(ItemId.Gola, 2));
            AgniCrate.ServerSpawnAirdrop(at, items);
            AgniMatch.Instance?.AirdropRpc(at);
            Debug.Log("[AgniLoot] airdrop #" + dropsDone + " falling to " + at);
        }
    }
}
