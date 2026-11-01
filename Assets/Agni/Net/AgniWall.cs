// split out of AgniThrowables.cs (Unity needs the file name to match the component class)
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    /// AGNI DEEWAR — curved fired-clay wall: rises in 0.35 s, 800 HP, 20 s, max 3 per player (4th removes the oldest).
    public class AgniWall : NetworkBehaviour
    {
        public const float MaxHp = 800f, Life = 20f, Width = 4.2f, Height = 2.25f, Thick = 0.42f; public const int Segs = 5, MaxPerPlayer = 3;
        public NetworkVariable<float> Hp = new NetworkVariable<float>(MaxHp);
        public NetworkVariable<ulong> OwnerPlayer = new NetworkVariable<ulong>();
        static readonly Dictionary<ulong, List<AgniWall>> byOwner = new Dictionary<ulong, List<AgniWall>>();
        public static readonly List<AgniWall> Live = new List<AgniWall>();
        float born, dieAt; AgniWallView view;

        public static void ServerDeploy(Vector3 at, AgniPlayer thrower, float yaw)
        {
            var prefab = Resources.Load<GameObject>("AgniNet/AgniWall"); if (prefab == null) return;
            // settle on the ground under the impact point
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var g, 4f, ~0, QueryTriggerInteraction.Ignore)) at = g.point;
            var go = Instantiate(prefab, at, Quaternion.Euler(0, yaw, 0)); var w = go.GetComponent<AgniWall>();
            ulong owner = thrower != null ? thrower.NetworkObjectId : 0; w.OwnerPlayer.Value = owner;
            go.GetComponent<NetworkObject>().Spawn(true);
            if (!byOwner.TryGetValue(owner, out var l)) byOwner[owner] = l = new List<AgniWall>();
            l.Add(w); while (l.Count > MaxPerPlayer) { var old = l[0]; l.RemoveAt(0); if (old != null && old.IsSpawned) old.Break(false); }
        }

        public override void OnNetworkSpawn()
        {
            born = Time.time; dieAt = Time.time + Life; Live.Add(this);
            AgniWallView.BuildColliders(transform);
            if (NetworkManager.IsClient) { view = gameObject.AddComponent<AgniWallView>(); Hp.OnValueChanged += (a, b) => view.SetDamage(1f - b / MaxHp, b < a); }
        }
        public override void OnNetworkDespawn() { Live.Remove(this); foreach (var l in byOwner.Values) l.Remove(this); }
        void Update() { if (IsServer && IsSpawned && Time.time > dieAt) Break(false); }
        public void ServerDamage(float dmg)
        {
            if (!IsServer || !IsSpawned) return;
            Hp.Value = Mathf.Max(0, Hp.Value - dmg);
            if (Hp.Value <= 0) Break(true);
        }
        void Break(bool shattered) { BreakFxRpc(transform.position, transform.rotation, shattered); if (IsSpawned) NetworkObject.Despawn(true); }
        [Rpc(SendTo.ClientsAndHost)] void BreakFxRpc(Vector3 p, Quaternion r, bool shattered) { AgniFx.WallBreak(p, r, shattered); }
    }
}
