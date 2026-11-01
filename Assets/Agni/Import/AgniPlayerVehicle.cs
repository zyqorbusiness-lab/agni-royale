// Agni Royale — IMPORT v1: getting in and out of the Blender vehicles (server-authoritative).
// GAADI button / B key -> VehicleRpc -> the server seats the fighter in the nearest vehicle (driver seat first) or drops them at the nearest free exit.
// While seated the server parks the fighter on the seat every tick (CharacterController off); the vehicle reads the driver's stick (AgniVehicle.FixedUpdate).
// Clients draw the rider on the seat of their own interpolated copy of the vehicle (AgniCharacterView) so rider and car never drift apart.
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public partial class AgniPlayer
    {
        public const float EnterRange = 3.2f;
        public NetworkVariable<NetworkObjectReference> Ride = new NetworkVariable<NetworkObjectReference>();
        public NetworkVariable<sbyte> Seat = new NetworkVariable<sbyte>(-1);
        public bool InVehicle => Seat.Value >= 0;
        public bool Driving => Seat.Value == 0;
        public AgniVehicle Vehicle { get { if (Seat.Value < 0) return null; return Ride.Value.TryGet(out NetworkObject no) && no != null ? no.GetComponent<AgniVehicle>() : null; } }
        /// root of a seated fighter: the seat anchor is the hip, the feet hang ~0.5 m below
        public static Vector3 SeatRoot(AgniVehicle v, int seat) => v.SeatPos(seat) - v.transform.up * 0.5f;

        [Rpc(SendTo.Server)] public void VehicleRpc(RpcParams p = default) { if (Own(p)) ServerToggleVehicle(); }
        public void ServerToggleVehicle() { if (InVehicle) ServerExitVehicle(); else ServerEnterVehicle(null); }

        public bool ServerEnterVehicle(AgniVehicle v, int seat = -1)
        {
            if (!IsServer || !Alive || InVehicle || Healing.Value) return false;
            if (v == null) v = AgniVehicle.Nearest(transform.position, EnterRange); if (v == null || !v.IsSpawned) return false;
            if (seat < 0) seat = v.FreeSeat(true); if (seat < 0 || seat >= v.occupants.Length || v.occupants[seat] != null) return false;
            v.occupants[seat] = this; Ride.Value = v.NetworkObject; Seat.Value = (sbyte)seat;
            if (Crouched.Value) ServerCrouch();
            CancelCook(); if (Slot.Value == 1) Slot.Value = 0;
            cc.enabled = false; transform.SetPositionAndRotation(SeatRoot(v, seat), Quaternion.Euler(0, v.transform.eulerAngles.y, 0)); vy = 0;
            Debug.Log("[AgniVehicles] " + PlayerName.Value + " in " + v.kind + " seat " + seat);
            return true;
        }
        public void ServerExitVehicle()
        {
            if (!IsServer || Seat.Value < 0) return;
            var v = Vehicle; int seat = Seat.Value; Vector3 to = transform.position + Vector3.up * 0.5f;
            if (v != null)
            {
                if (seat < v.occupants.Length && v.occupants[seat] == this) v.occupants[seat] = null;
                // the nearest exit with room to stand, else beside the vehicle, else on its roof
                Vector3 best = v.transform.position + v.transform.right * 2.6f + Vector3.up * 1.5f; float bd = 1e9f; bool found = false;
                var cands = new System.Collections.Generic.List<Vector3>();
                if (v.exits != null) foreach (var e in v.exits) if (e != null) cands.Add(e.position);
                cands.Add(v.transform.position + v.transform.right * 2.6f); cands.Add(v.transform.position - v.transform.right * 2.6f); cands.Add(v.transform.position - v.transform.forward * 3.6f);
                var seatP = v.SeatPos(seat);
                foreach (var c in cands)
                {
                    if (!Physics.Raycast(c + Vector3.up * 2.5f, Vector3.down, out var h, 6f, ~(1 << AgniVehicle.VehicleLayer | 1 << AgniVehicle.WaterLayer), QueryTriggerInteraction.Ignore)) continue;
                    var foot = h.point + Vector3.up * 0.05f;
                    if (Physics.CheckCapsule(foot + Vector3.up * 0.45f, foot + Vector3.up * 1.4f, 0.32f, ~(1 << AgniVehicle.WaterLayer), QueryTriggerInteraction.Ignore)) continue;
                    float d = (c - seatP).sqrMagnitude; if (d < bd) { bd = d; best = foot; found = true; }
                }
                if (!found && v.IsBoat) best = v.transform.position + v.transform.right * 2.4f + Vector3.up * 0.2f; // swim off the side
                to = best;
            }
            Ride.Value = default; Seat.Value = -1;
            cc.enabled = false; transform.position = to; cc.enabled = true; vy = 0;
        }
        /// called from ServerTick before walking; true = seated this tick (no walking)
        bool ServerRide(float dt)
        {
            ServerEmoteTick();
            if (Seat.Value < 0) return false;
            var v = Vehicle;
            if (v == null || !v.IsSpawned || !Alive) { ServerExitVehicle(); return Seat.Value >= 0; }
            int seat = Seat.Value; if (seat < v.occupants.Length && v.occupants[seat] != this) v.occupants[seat] = this;
            if (cc.enabled) cc.enabled = false;
            transform.SetPositionAndRotation(SeatRoot(v, seat), Quaternion.Euler(0, v.transform.eulerAngles.y, 0));
            return true;
        }
    }
}
