// Agni Royale — throwables (our own names): GOLA (frag), DHUAN (smoke), PATAKHA (flash firecracker), AGNI DEEWAR (fired-clay cover wall).
// Server simulates the projectile (Rigidbody) and decides every effect; clients only draw. Arc preview and the
// server use the same launch maths (ThrowMath) so the line you see is the line the grenade flies.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public enum ThrowKind : byte { None = 0, Gola = 1, Dhuan = 2, Patakha = 3, Deewar = 4 }

    public struct ThrowDef
    {
        public ThrowKind kind; public string name, hint; public float speed, upBoost, fuse, radius, maxDamage, bounce; public int carryMax; public bool cookable;
        public Color tint;
    }

    public static class ThrowMath
    {
        public static readonly ThrowKind[] All = { ThrowKind.Gola, ThrowKind.Dhuan, ThrowKind.Patakha, ThrowKind.Deewar };
        public static ThrowDef Def(ThrowKind k)
        {
            switch (k)
            {
                case ThrowKind.Gola: return new ThrowDef { kind = k, name = "GOLA", hint = "Frag — pakad ke rakho = fuse jalta hai", speed = 17f, upBoost = 2.2f, fuse = 4.0f, radius = 7.5f, maxDamage = 185f, bounce = 0.35f, carryMax = 4, cookable = true, tint = new Color(0.42f, 0.47f, 0.28f) };
                case ThrowKind.Dhuan: return new ThrowDef { kind = k, name = "DHUAN", hint = "Smoke — 20 s ka parda", speed = 16f, upBoost = 2.2f, fuse = 1.8f, radius = 6.5f, bounce = 0.25f, carryMax = 3, tint = new Color(0.62f, 0.68f, 0.70f) };
                case ThrowKind.Patakha: return new ThrowDef { kind = k, name = "PATAKHA", hint = "Flash — aankhein chaundhiya do", speed = 17f, upBoost = 2.2f, fuse = 1.7f, radius = 18f, bounce = 0.3f, carryMax = 3, tint = new Color(0.86f, 0.18f, 0.14f) };
                case ThrowKind.Deewar: return new ThrowDef { kind = k, name = "AGNI DEEWAR", hint = "Mitti ki deewar — 800 HP, 20 s", speed = 10.5f, upBoost = 1.2f, fuse = 1.1f, radius = 0f, maxDamage = 0, bounce = 0.0f, carryMax = 6, tint = new Color(0.78f, 0.38f, 0.20f) };
            }
            return new ThrowDef { kind = ThrowKind.None, name = "-" };
        }
        public const float Gravity = 9.81f, Drag = 0.08f, Radius = 0.07f;

        /// hand position + initial velocity for a throw from a body at `pos` aiming yaw/pitch
        public static void Launch(Vector3 pos, float yaw, float pitch, ThrowKind k, float power, out Vector3 origin, out Vector3 vel)
        {
            var d = Def(k); var rot = Quaternion.Euler(Mathf.Clamp(pitch, -80, 80), yaw, 0);
            var fwd = rot * Vector3.forward; var flatR = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            origin = pos + Vector3.up * 1.62f + flatR * 0.28f + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.35f;
            vel = fwd * d.speed * Mathf.Clamp(power, 0.35f, 1f) + Vector3.up * d.upBoost;
        }

        /// Ballistic path with bounces (matches the server Rigidbody closely: gravity, light drag, restitution on hits).
        public static int Simulate(Vector3 p, Vector3 v, ThrowKind k, List<Vector3> pts, out Vector3 land, out Vector3 landNormal, float maxTime = 4f, int layerMask = ~0, Collider ignoreA = null)
        {
            var d = Def(k); pts.Clear(); pts.Add(p); land = p; landNormal = Vector3.up; float t = 0, dt = 1f / 60f; int bounces = 0;
            while (t < maxTime && pts.Count < 400)
            {
                v += Vector3.down * Gravity * dt; v *= 1f - Drag * dt;
                var step = v * dt;
                if (Physics.SphereCast(p, Radius, step.normalized, out var hit, step.magnitude, layerMask, QueryTriggerInteraction.Ignore) && hit.collider != ignoreA)
                {
                    p = hit.point + hit.normal * Radius; pts.Add(p); land = hit.point; landNormal = hit.normal;
                    if (k == ThrowKind.Deewar || bounces >= 3) return bounces;
                    var vn = Vector3.Dot(v, hit.normal) * hit.normal; var vt = v - vn;
                    v = vt * 0.62f - vn * d.bounce; bounces++;
                    if (v.magnitude < 1.2f) return bounces;
                }
                else { p += step; if ((pts.Count & 1) == 0 || t + dt >= maxTime) pts.Add(p); }
                t += dt;
            }
            land = p; return bounces;
        }
    }

    /// Server-known smoke volumes (bots can't see through them).
    public static class AgniSmoke
    {
        struct S { public Vector3 c; public float r, until; }
        static readonly List<S> list = new List<S>();
        public static void Add(Vector3 c, float r, float seconds) => list.Add(new S { c = c, r = r, until = Time.time + seconds });
        public static bool Blocks(Vector3 a, Vector3 b)
        {
            list.RemoveAll(s => s.until < Time.time);
            foreach (var s in list)
            {
                var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(s.c - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                if ((a + ab * t - s.c).sqrMagnitude < s.r * s.r * 0.8f) return true;
            }
            return false;
        }
    }

}
