// Agni Royale — server-side bot brain. Bots fill empty slots so every match has 50 fighters.
// They roam toward the safe zone, spot enemies (line of sight, smoke blocks it), aim with human-ish lag and error,
// fire in bursts, strafe, throw GOLA at enemies behind cover, drop an AGNI DEEWAR when hurt, revive squad mates and heal.
using UnityEngine;

namespace Agni.Net
{
    public class AgniBotBrain
    {
        public float SpreadMul = Random.Range(1.6f, 3.0f);
        public static bool Passive => AgniNetConfig.Has("-agniBotsPassive"); // test flag: bots stand still and never shoot
        float yaw, pitch, scanT, reactT, burstT, strafeT, strafeDir = 1, stuckT, throwHold, wallCd, nadeCd = 8f, healCd;
        AgniPlayer target, threat; Vector3 goal, lastPos, lastSeen; float lastSeenT = -99; bool goalSet, throwing; float skill = Random.Range(0.35f, 1f);

        public void OnHurt(AgniPlayer by) { threat = by; }

        public AgniInput Think(AgniPlayer me, float dt)
        {
            var inp = new AgniInput { yaw = yaw, pitch = pitch };
            if (Passive) { inp.aim = me.Eye + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 50; return inp; }
            var z = AgniZone.Instance; float now = Time.time;
            bool blind = now < me.blindUntil;
            scanT -= dt; wallCd -= dt; nadeCd -= dt; healCd -= dt;
            if (scanT <= 0) { scanT = 0.3f + Random.value * 0.15f; var t = FindTarget(me); if (t != target) { target = t; reactT = Mathf.Lerp(0.9f, 0.35f, skill); } }
            if (target != null && (target.Dead || target == null)) target = null;

            if (me.Knocked)
            {   // crawl to the nearest standing teammate
                var mate = Nearest(me, p => p.Team.Value == me.Team.Value && p.Alive, 60f);
                if (mate != null) MoveTo(me, mate.transform.position, ref inp, dt);
                return inp;
            }

            // ---- combat
            if (target != null && !blind)
            {
                lastSeen = target.transform.position; lastSeenT = now;
                var aimAt = target.Chest + Random.insideUnitSphere * Mathf.Lerp(0.6f, 0.15f, skill);
                AimAt(me, aimAt, dt, Mathf.Lerp(140f, 420f, skill));
                reactT -= dt; burstT -= dt;
                float dist = Vector3.Distance(me.transform.position, target.transform.position);
                if (me.Slot.Value != 0 && !throwing) me.ServerSlot(0);
                if (reactT <= 0 && !throwing)
                {
                    if (burstT > 0) inp.buttons |= AgniInput.Fire;
                    else if (burstT < -Mathf.Lerp(0.6f, 0.25f, skill)) burstT = Random.Range(0.35f, 0.9f);
                }
                // strafe and keep a fighting distance
                strafeT -= dt; if (strafeT <= 0) { strafeT = Random.Range(0.6f, 1.6f); strafeDir = Random.value < 0.5f ? -1 : 1; }
                inp.move = new Vector2(strafeDir * 0.8f, dist > 28 ? 0.7f : dist < 10 ? -0.5f : 0f);
                // hurt and exposed: drop a wall between us
                if (me.Hp.Value < 110 && wallCd <= 0 && me.Count(ThrowKind.Deewar) > 0 && dist < 40) { wallCd = 12f; StartThrow(me, ThrowKind.Deewar, -25f); }
            }
            else if (!blind && now - lastSeenT < 4f && now - lastSeenT > 1.0f && nadeCd <= 0 && me.Count(ThrowKind.Gola) > 0 && !throwing)
            {   // enemy ducked behind cover: lob a GOLA at the last spot
                float d = Vector3.Distance(me.transform.position, lastSeen);
                if (d > 9f && d < 36f) { nadeCd = Random.Range(10f, 18f); yaw = Yaw(me.transform.position, lastSeen); StartThrow(me, ThrowKind.Gola, SolvePitch(me, lastSeen)); }
            }
            else if (blind) { inp.move = new Vector2(Mathf.Sin(now * 3f), -0.6f); }
            else
            {
                // ---- revive / heal / roam
                var downed = Nearest(me, p => p.Team.Value == me.Team.Value && p.Knocked, 30f);
                if (downed != null)
                {
                    if (Vector3.Distance(downed.transform.position, me.transform.position) < AgniPlayer.ReviveRange * 0.8f) { inp.buttons |= AgniInput.Interact; AimAt(me, downed.transform.position, dt, 200); }
                    else MoveTo(me, downed.transform.position, ref inp, dt);
                }
                else if (me.Hp.Value < 130 && me.Medkits.Value > 0 && !me.Healing.Value && healCd <= 0) { healCd = 6f; me.ServerHeal(); }
                else
                {
                    if (!goalSet || (goal - me.transform.position).sqrMagnitude < 9f || (z != null && !InCircle(goal, z.ToC.Value, z.ToR.Value * 0.9f))) NewGoal(me, z);
                    MoveTo(me, goal, ref inp, dt);
                    if (inp.move.y > 0.5f && z != null && !z.Inside(me.transform.position)) inp.buttons |= AgniInput.Sprint;
                }
            }

            // ---- throwing: hold "fire" while the slot is the throwable, then release
            if (throwing)
            {
                inp.move = Vector2.zero; throwHold -= dt;
                if (me.Slot.Value == 1 && throwHold > 0) inp.buttons |= AgniInput.Fire;
                if (throwHold < -0.2f) { throwing = false; me.ServerSlot(0); }
            }
            inp.yaw = yaw; inp.pitch = pitch; inp.aim = me.Eye + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 60f;
            return inp;
        }

        void StartThrow(AgniPlayer me, ThrowKind k, float p) { me.ServerSelect(k); pitch = p; throwing = true; throwHold = k == ThrowKind.Gola ? Random.Range(0.4f, 1.4f) : 0.25f; }

        static bool InCircle(Vector3 p, Vector3 c, float r) { p.y = c.y = 0; return (p - c).sqrMagnitude < r * r; }
        void NewGoal(AgniPlayer me, AgniZone z)
        {
            Vector3 c = z != null ? z.ToC.Value : Vector3.zero; float r = z != null ? z.ToR.Value * 0.8f : AgniMatch.ArenaRadius * 0.6f;
            var o = Random.insideUnitCircle * r; goal = new Vector3(c.x + o.x, 0, c.z + o.y);
            // prefer something near-ish so bots spread out and meet
            goal = Vector3.Lerp(me.transform.position, goal, Random.Range(0.25f, 0.7f)); goal.y = 0; goalSet = true;
        }
        void MoveTo(AgniPlayer me, Vector3 dest, ref AgniInput inp, float dt)
        {
            var pos = me.transform.position; var to = dest - pos; to.y = 0;
            float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            // simple whisker avoidance at knee height
            var origin = pos + Vector3.up * 0.5f; var fwd = Quaternion.Euler(0, want, 0) * Vector3.forward;
            if (Physics.SphereCast(origin, 0.3f, fwd, out _, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var l = Quaternion.Euler(0, want - 60, 0) * Vector3.forward; var r = Quaternion.Euler(0, want + 60, 0) * Vector3.forward;
                bool lf = !Physics.SphereCast(origin, 0.3f, l, out _, 2.2f, ~0, QueryTriggerInteraction.Ignore), rf = !Physics.SphereCast(origin, 0.3f, r, out _, 2.2f, ~0, QueryTriggerInteraction.Ignore);
                want += lf && !rf ? -60 : rf && !lf ? 60 : (strafeDir * 75);
            }
            yaw = Mathf.MoveTowardsAngle(yaw, want, 360f * dt); pitch = Mathf.MoveTowards(pitch, 0, 90 * dt);
            inp.move = new Vector2(0, to.magnitude > 1.5f ? 1f : 0f);
            stuckT += dt; if (stuckT > 1.2f) { stuckT = 0; if ((pos - lastPos).sqrMagnitude < 0.5f) { goalSet = false; strafeDir = -strafeDir; me.ServerJump(); } lastPos = pos; }
        }
        void AimAt(AgniPlayer me, Vector3 p, float dt, float degPerSec)
        {
            var d = p - me.Eye; float wy = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; float wp = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, wy, degPerSec * dt); pitch = Mathf.MoveTowards(pitch, wp, degPerSec * 0.6f * dt);
        }
        static float Yaw(Vector3 a, Vector3 b) { var d = b - a; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        static float SolvePitch(AgniPlayer me, Vector3 dest)
        {
            var d = ThrowMath.Def(ThrowKind.Gola); var o = me.transform.position + Vector3.up * 1.62f; var dd = dest - o; float dx = new Vector2(dd.x, dd.z).magnitude, dy = dd.y;
            float best = 20, err = 1e9f;
            for (float p = -10; p <= 50; p += 2)
            {
                float pr = p * Mathf.Deg2Rad; float vx = d.speed * Mathf.Cos(pr), vyy = d.speed * Mathf.Sin(pr) + d.upBoost; // p = elevation up
                float t = dx / Mathf.Max(0.1f, vx); float y = vyy * t - 0.5f * ThrowMath.Gravity * t * t; float e = Mathf.Abs(y - dy);
                if (e < err) { err = e; best = p; }
            }
            return -best; // Unity pitch: negative = look up
        }
        AgniPlayer FindTarget(AgniPlayer me)
        {
            AgniPlayer best = null; float bd = 75f * 75f;
            if (threat != null && !threat.Dead && Visible(me, threat)) return threat;
            foreach (var p in AgniPlayer.All)
            {
                if (p == me || p.Dead || p.Team.Value == me.Team.Value) continue;
                float d = (p.transform.position - me.transform.position).sqrMagnitude; if (d >= bd) continue;
                // field of view 200° (they "hear" behind them up close)
                var to = (p.transform.position - me.transform.position).normalized; float dot = Vector3.Dot(to, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
                if (dot < -0.2f && d > 15f * 15f) continue;
                if (!Visible(me, p)) continue; bd = d; best = p;
            }
            return best;
        }
        static bool Visible(AgniPlayer me, AgniPlayer p)
        {
            var a = me.Eye; var b = p.Chest;
            if (AgniSmoke.Blocks(a, b)) return false;
            var hits = Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) { if (h.collider.transform.IsChildOf(me.transform) || h.collider.transform.IsChildOf(p.transform)) continue; var o = h.collider.GetComponentInParent<AgniPlayer>(); if (o != null) continue; return false; }
            return true;
        }
        static AgniPlayer Nearest(AgniPlayer me, System.Func<AgniPlayer, bool> f, float max)
        {
            AgniPlayer best = null; float bd = max * max;
            foreach (var p in AgniPlayer.All) { if (p == me || !f(p)) continue; float d = (p.transform.position - me.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = p; } }
            return best;
        }
    }
}
