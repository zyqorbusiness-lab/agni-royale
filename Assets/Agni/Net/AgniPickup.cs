// Agni Royale — loot on the ground (server spawns, walk over to pick up). Throwables, ammo, "Patti" medkits.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum LootKind : byte { Gola = 1, Dhuan = 2, Patakha = 3, Deewar = 4, Ammo = 10, Patti = 11 }

    public class AgniPickup : NetworkBehaviour
    {
        public static readonly List<AgniPickup> Live = new List<AgniPickup>();
        public NetworkVariable<byte> Kind = new NetworkVariable<byte>();
        public NetworkVariable<byte> Amount = new NetworkVariable<byte>(1);
        bool taken; GameObject view;

        public override void OnNetworkSpawn() { Live.Add(this); if (NetworkManager.IsClient) view = AgniFx.BuildLootView((LootKind)Kind.Value, transform); }
        public override void OnNetworkDespawn() { Live.Remove(this); }

        public static void ServerScatter(float radius, int n)
        {
            var prefab = Resources.Load<GameObject>("AgniNet/AgniPickup"); if (prefab == null) return;
            var kinds = new[] { LootKind.Gola, LootKind.Gola, LootKind.Dhuan, LootKind.Patakha, LootKind.Deewar, LootKind.Deewar, LootKind.Ammo, LootKind.Ammo, LootKind.Ammo, LootKind.Patti };
            for (int i = 0; i < n; i++)
            {
                var p2 = Random.insideUnitCircle * radius * 0.95f; var pos = AgniMatch.Ground(new Vector3(p2.x, 0, p2.y)) + Vector3.up * 0.05f;
                var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0)); var pk = go.GetComponent<AgniPickup>();
                var k = kinds[Random.Range(0, kinds.Length)]; pk.Kind.Value = (byte)k; pk.Amount.Value = (byte)(k == LootKind.Ammo ? 60 : k == LootKind.Deewar ? 2 : 1);
                go.GetComponent<NetworkObject>().Spawn(true);
            }
        }
        public static void ServerDrop(LootKind k, int amount, Vector3 pos)
        {
            var prefab = Resources.Load<GameObject>("AgniNet/AgniPickup"); var go = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var pk = go.GetComponent<AgniPickup>(); pk.Kind.Value = (byte)k; pk.Amount.Value = (byte)amount; go.GetComponent<NetworkObject>().Spawn(true);
        }

        public static void ServerTryPick(AgniPlayer p)
        {
            if (!p.Alive) return;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var pk = Live[i]; if (pk == null || pk.taken || !pk.IsSpawned) continue;
                if ((pk.transform.position - p.transform.position).sqrMagnitude > 1.8f * 1.8f) continue;
                if (pk.Give(p)) { pk.taken = true; p.PickedRpc(pk.Kind.Value, pk.Amount.Value); pk.NetworkObject.Despawn(true); }
            }
        }
        bool Give(AgniPlayer p)
        {
            var k = (LootKind)Kind.Value;
            if ((byte)k <= 4) { var tk = (ThrowKind)(byte)k; if (p.Count(tk) >= ThrowMath.Def(tk).carryMax) return false; p.ServerAddThrowable(tk, Amount.Value); return true; }
            if (k == LootKind.Ammo) { if (p.Reserve.Value >= 240) return false; p.Reserve.Value = Mathf.Min(240, p.Reserve.Value + Amount.Value); return true; }
            if (k == LootKind.Patti) { if (p.Medkits.Value >= AgniPlayer.MedkitMax) return false; p.Medkits.Value++; return true; }
            return false;
        }
        public static string Label(LootKind k, int n)
        {
            switch (k) { case LootKind.Ammo: return "+" + n + " goli"; case LootKind.Patti: return "+1 Patti (medkit)"; default: return "+" + n + " " + ThrowMath.Def((ThrowKind)(byte)k).name; }
        }
    }

}
