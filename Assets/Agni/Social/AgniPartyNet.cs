// split out of AgniParty.cs (Unity needs the file name to match the component class)
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Agni.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Social
{
    /// The replicated party (spawned by the leader/host in the lobby).
    public class AgniPartyNet : NetworkBehaviour
    {
        public static AgniPartyNet Current;
        public NetworkList<PartyMemberNet> Members;
        public NetworkVariable<FixedString32Bytes> Code = new NetworkVariable<FixedString32Bytes>();
        public NetworkVariable<FixedString128Bytes> MockSquad = new NetworkVariable<FixedString128Bytes>(); // demo friends who "accepted" (names, '|' separated)
        public static event Action Changed;

        void Awake() { Members = new NetworkList<PartyMemberNet>(); }
        public override void OnNetworkSpawn()
        {
            Current = this;
            Members.OnListChanged += _ => Changed?.Invoke();
            MockSquad.OnValueChanged += (a, b) => Changed?.Invoke();
            if (IsServer)
            {
                foreach (var kv in AgniNet.Clients) AddMember(kv.Value);
                NetworkManager.OnClientConnectedCallback += OnJoin; NetworkManager.OnClientDisconnectCallback += OnLeave;
            }
            Changed?.Invoke();
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) { NetworkManager.OnClientConnectedCallback -= OnJoin; NetworkManager.OnClientDisconnectCallback -= OnLeave; }
            if (Current == this) Current = null; Changed?.Invoke();
        }
        void OnJoin(ulong id) { if (AgniNet.Clients.TryGetValue(id, out var c)) AddMember(c); }
        void OnLeave(ulong id) { for (int i = Members.Count - 1; i >= 0; i--) if (Members[i].clientId == id) Members.RemoveAt(i); }
        void AddMember(AgniClientInfo c)
        {
            for (int i = 0; i < Members.Count; i++) if (Members[i].clientId == c.clientId) return;
            bool lead = c.clientId == NetworkManager.ServerClientId;
            Members.Add(new PartyMemberNet { clientId = c.clientId, name = c.name, playerId = c.playerId, ready = lead, leader = lead });
        }
        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready, RpcParams p = default)
        {
            for (int i = 0; i < Members.Count; i++) if (Members[i].clientId == p.Receive.SenderClientId) { var m = Members[i]; m.ready = ready || m.leader; Members[i] = m; }
        }
        [Rpc(SendTo.ClientsAndHost)]
        public void GoingToMatchRpc() { LanPartyService.RaiseGoing(); }
    }
}
