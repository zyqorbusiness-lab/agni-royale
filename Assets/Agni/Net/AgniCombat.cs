// Agni Royale — area effects decided on the server: GOLA blast (falloff + cover), PATAKHA flash (distance, facing,
// line of sight), DHUAN smoke. Clients get one RPC each and draw the effect (AgniFx).
using Unity.Netcode;
using UnityEngine;

namespace Agni.Net
{
    public class AgniCombat : NetworkBehaviour
    {
        public static AgniCombat Instance;
        public override void OnNetworkSpawn() { Instance = this; }
        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        /// GOLA: damage = max · (1 − d/r)^1.25; a solid object between blast and body cuts it to 15 %.
        public static void ServerExplode(Vector3 at, ThrowDef d, AgniPlayer by)
        {
            var c = at + Vector3.up * 0.25f;
            foreach (var p in AgniPlayer.All.ToArray())
            {
                if (p.Dead) continue; float dist = Vector3.Distance(c, p.Chest); if (dist > d.radius) continue;
                float dmg = d.maxDamage * Mathf.Pow(1f - dist / d.radius, 1.25f);
                if (Blocked(c, p.Chest, p.transform)) dmg *= 0.15f;
                if (dmg >= 1f) p.ApplyDamage(dmg, by, "GOLA", false, at);
            }
            foreach (var w in AgniWall.Live.ToArray()) { float dist = Vector3.Distance(c, w.transform.position + Vector3.up); if (w != null && dist < d.radius) w.ServerDamage(d.maxDamage * 1.6f * (1f - dist / d.radius)); }
            Instance?.ExplodeFxRpc(at);
        }
        /// PATAKHA: blind seconds = 0.6 + 3.4 · closeness · facing (looking away ≈ 35 %), nothing through walls.
        public static void ServerFlash(Vector3 at, ThrowDef d, AgniPlayer by)
        {
            var c = at + Vector3.up * 0.3f;
            foreach (var p in AgniPlayer.All)
            {
                if (p.Dead) continue; float dist = Vector3.Distance(c, p.Eye); if (dist > d.radius) continue;
                if (Blocked(c, p.Eye, p.transform)) continue;
                var look = Quaternion.Euler(p.Pitch.Value, p.transform.eulerAngles.y, 0) * Vector3.forward;
                float facing = Mathf.Lerp(0.35f, 1f, (Vector3.Dot(look, (c - p.Eye).normalized) + 1f) * 0.5f);
                float k = Mathf.Clamp01((1f - dist / d.radius) * 1.25f) * facing; if (k < 0.05f) continue;
                float secs = 0.6f + 3.4f * k;
                p.blindUntil = Time.time + secs;
                if (!p.IsBot.Value) p.FlashedRpc(k, secs);
            }
            Instance?.FlashFxRpc(at);
        }
        static bool Blocked(Vector3 a, Vector3 b, Transform target)
        {
            var hits = Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(target)) continue;
                if (h.collider.GetComponentInParent<AgniPlayer>() != null) continue; if (h.collider.GetComponentInParent<AgniThrowable>() != null) continue;
                return true;
            }
            return false;
        }

        [Rpc(SendTo.ClientsAndHost)] public void ExplodeFxRpc(Vector3 at) { AgniFx.Explosion(at); }
        [Rpc(SendTo.ClientsAndHost)] public void FlashFxRpc(Vector3 at) { AgniFx.FlashPop(at); }
        [Rpc(SendTo.ClientsAndHost)] public void SmokeFxRpc(Vector3 at) { AgniFx.SmokeCloud(at, 20f); }
    }
}
