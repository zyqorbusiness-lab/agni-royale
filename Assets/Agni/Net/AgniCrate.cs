// Agni Royale — LOOT v1: a box of many stacks (death "peti" or the airdrop "Raahat Peti"). Server owns it.
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public class AgniCrate : NetworkBehaviour
    {
        public static readonly List<AgniCrate> Live = new List<AgniCrate>();
        public NetworkList<LootStack> Items = new NetworkList<LootStack>();
        public NetworkVariable<byte> Kind = new NetworkVariable<byte>();          // 0 death peti, 1 airdrop
        public NetworkVariable<FixedString32Bytes> Label = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<bool> Landed = new NetworkVariable<bool>();
        [System.NonSerialized] public Vector3 landAt; GameObject view;
        public const float FallSpeed = 6f;

        public override void OnNetworkSpawn()
        {
            Live.Add(this); var bc = gameObject.AddComponent<BoxCollider>();
            if (Kind.Value == 1) { bc.size = Vector3.one * 1.4f; bc.center = Vector3.up * 0.7f; } else { bc.size = new Vector3(0.98f, 0.56f, 0.58f); bc.center = Vector3.up * 0.28f; }
            if (NetworkManager.IsClient) view = AgniLootView.BuildCrate(this);
        }
        public override void OnNetworkDespawn() { Live.Remove(this); }

        void Update()
        {
            if (!IsServer || !IsSpawned || Landed.Value) return;
            var p = transform.position; p = Vector3.MoveTowards(p, landAt, FallSpeed * Time.deltaTime); transform.position = p;
            if ((p - landAt).sqrMagnitude < 0.0004f) { Landed.Value = true; AgniMatch.Instance?.AirdropLandedRpc(landAt); }
        }

        static GameObject prefab;
        static AgniCrate Make(Vector3 pos, byte kind, string label, List<LootStack> items)
        {
            if (prefab == null) prefab = Resources.Load<GameObject>("AgniNet/AgniCrate"); if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0)); var c = go.GetComponent<AgniCrate>();
            c.Kind.Value = kind; c.Label.Value = label; go.GetComponent<NetworkObject>().Spawn(true);
            foreach (var s in items) c.Items.Add(s);
            return c;
        }
        public static AgniCrate ServerSpawnDeath(Vector3 ground, string owner, List<LootStack> items)
        {
            var c = Make(ground, 0, (owner.Length > 22 ? owner.Substring(0, 22) : owner) + " ki Peti", items); if (c != null) c.Landed.Value = true; return c;
        }
        public static AgniCrate ServerSpawnAirdrop(Vector3 ground, List<LootStack> items)
        {
            var c = Make(ground + Vector3.up * 150f, 1, "Raahat Peti", items); if (c != null) c.landAt = ground; return c;
        }

        public void ServerGive(AgniPlayer p, int index)
        {
            if (!Landed.Value || index < 0 || index >= Items.Count) return;
            var s = Items[index]; var drops = new List<LootStack>(); int got = p.ServerTake(s.Id, s.amount, true, drops);
            foreach (var d in drops) AgniPickup.ServerDrop(d.Id, d.amount, transform.position + transform.right * 1.0f + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f)));
            if (got <= 0) { p.PickFailRpc(s.item); return; }
            p.PickedRpc(s.item, (ushort)got);
            if (AgniLoot.WholeOnly(s.Id) || got >= s.amount) Items.RemoveAt(index); else Items[index] = new LootStack(s.Id, s.amount - got);
            if (Items.Count == 0 && Kind.Value == 0) NetworkObject.Despawn(true);
        }
    }

}
