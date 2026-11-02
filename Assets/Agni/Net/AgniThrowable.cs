// split out of AgniThrowables.cs (Unity needs the file name to match the component class)
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    /// The flying throwable (one prefab, kind chosen at spawn).
    public class AgniThrowable : NetworkBehaviour
    {
        public NetworkVariable<byte> Kind = new NetworkVariable<byte>();
        public NetworkVariable<double> FuseEnd = new NetworkVariable<double>();
        [System.NonSerialized] public AgniPlayer thrower; [System.NonSerialized] public float throwYaw;
        Rigidbody rb; bool done; float spawnT; GameObject view; float lastBounceFx;
        // server flight = the SAME integration as ThrowMath.Simulate (gravity, drag, sphere-cast bounces), so the arc preview is the truth
        Vector3 vel, spin; int bounces; bool resting;

        public override void OnNetworkSpawn()
        {
            rb = GetComponent<Rigidbody>(); spawnT = Time.time;
            if (!IsServer) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
            if (NetworkManager.IsClient) view = AgniFx.BuildThrowableView((ThrowKind)Kind.Value, transform);
        }
        public void ServerLaunch(Vector3 v)
        {
            rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            var col = GetComponent<Collider>(); if (col != null) col.enabled = false; // we sweep ourselves; the collider must not block the casts
            vel = v; spin = Random.insideUnitSphere * 460f; bounces = 0; resting = false;
        }
        bool IsThrower(Collider c) => thrower != null && c.transform.IsChildOf(thrower.transform);
        void FixedUpdate()
        {
            if (!IsServer || done || resting || !IsSpawned) return;
            var k = (ThrowKind)Kind.Value; var d = ThrowMath.Def(k);
            float dt = Time.fixedDeltaTime; int sub = Mathf.Max(1, Mathf.CeilToInt(dt * 60f - 0.001f)); float h = dt / sub; var p = transform.position;
            for (int i = 0; i < sub && !resting; i++)
            {
                vel += Vector3.down * ThrowMath.Gravity * h; vel *= 1f - ThrowMath.Drag * h; var step = vel * h; float len = step.magnitude;
                RaycastHit hit = default; bool got = false;
                if (len > 1e-5f)
                    foreach (var hh in Physics.SphereCastAll(p, ThrowMath.Radius, step / len, len, ~0, QueryTriggerInteraction.Ignore))
                        if (!IsThrower(hh.collider) && hh.collider.gameObject != gameObject && (!got || hh.distance < hit.distance)) { hit = hh; got = true; }
                if (!got) { p += step; continue; }
                p = hit.point + hit.normal * ThrowMath.Radius;
                if (k == ThrowKind.Deewar) { transform.position = p; Detonate(hit.point, hit.normal); return; }
                if (bounces >= 3) { resting = true; break; }
                var vn = Vector3.Dot(vel, hit.normal) * hit.normal; var vt = vel - vn;
                float impact = vn.magnitude; vel = vt * 0.62f - vn * d.bounce; bounces++;
                if (Time.time - lastBounceFx > 0.15f && impact > 2.5f) { lastBounceFx = Time.time; BounceFxRpc(p, Mathf.Clamp01(impact / 14f)); }
                if (vel.magnitude < 1.2f) resting = true;
            }
            transform.position = p; if (!resting) transform.Rotate(spin * dt, Space.World);
        }
        void Update()
        {
            if (!IsServer || done || !IsSpawned) return;
            if (AgniNet.Now >= FuseEnd.Value) Detonate(transform.position, Vector3.up);
        }
        void OnCollisionEnter(Collision c)
        {
            if (!IsServer || done) return;
            var k = (ThrowKind)Kind.Value;
            if (k == ThrowKind.Deewar) { var cp = c.GetContact(0); Detonate(cp.point, cp.normal); return; }
            if (Time.time - lastBounceFx > 0.15f && c.relativeVelocity.magnitude > 2.5f) { lastBounceFx = Time.time; BounceFxRpc(transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 14f)); }
        }
        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)] void BounceFxRpc(Vector3 p, float strength) { AgniFx.Clink(p, strength); }

        void Detonate(Vector3 at, Vector3 normal)
        {
            done = true; var k = (ThrowKind)Kind.Value; var d = ThrowMath.Def(k);
            switch (k)
            {
                case ThrowKind.Gola: AgniCombat.ServerExplode(at, d, thrower); break;
                case ThrowKind.Dhuan: AgniSmoke.Add(at, d.radius, 20f); AgniCombat.Instance?.SmokeFxRpc(at); break;
                case ThrowKind.Patakha: AgniCombat.ServerFlash(at, d, thrower); break;
                case ThrowKind.Deewar: AgniWall.ServerDeploy(at, thrower, throwYaw); break;
            }
            NetworkObject.Despawn(true);
        }
    }
}
