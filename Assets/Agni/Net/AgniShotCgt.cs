// AgniShot "cgtrun" mode — CGTrader heroes v1 proof: the real match, our fighter with a rifle running on open ground,
// one shot per hero (the hero is switched on the server between shots; the view rebuilds the model like a lobby pick).
//   Unity -batchmode -force-glcore -executeMethod Agni.Net.EditorTools.AgniNetSetup.ShotBatch -agniShotScene AgniArena
//         -agniShot <dir> -agniShotMode cgtrun [-agniCgtHeroes Ishita|Tara|...] -agniShotW 1080 -agniShotH 1350
using System.Collections;
using UnityEngine;

namespace Agni.Net
{
    public partial class AgniShot
    {
        IEnumerator CgtRun()
        {
            yield return WaitPlaying(2.0f); AgniRetarget.debugLog = AgniNetConfig.Has("-agniRetargetLog");
            var lp = AgniLocalPlayer.Instance; var me = lp.P;
            AgniPickup gunPk = null; float gd = 1e9f;
            foreach (var pk in AgniPickup.Live) if (pk != null && AgniItems.IsGun(pk.Id)) { float d = (pk.transform.position - me.transform.position).sqrMagnitude; if (d < gd) { gd = d; gunPk = pk; } }
            if (gunPk != null) gunPk.ServerGive(me, true);
            yield return new WaitForSecondsRealtime(0.5f);
            var open = OpenGround(95f);
            var sunL = RenderSettings.sun; var sunH = sunL != null ? Vector3.ProjectOnPlane(sunL.transform.forward, Vector3.up).normalized : Vector3.forward; if (sunH.sqrMagnitude < 0.01f) sunH = Vector3.forward;
            var list = (AgniNetConfig.Arg("-agniCgtHeroes") ?? "Ishita|Tara|Rhea|Naina|Veer|Aryan").Split('|');
            foreach (var hn in list)
            {
                int hi = System.Array.IndexOf(AgniHeroes.Names, hn); if (hi < 0) { Log("cgtrun unknown hero " + hn); continue; }
                me.Hero.Value = (byte)hi;
                var faceDir = Quaternion.Euler(0, 35, 0) * -sunH; float yaw = Mathf.Atan2(faceDir.x, faceDir.z) * Mathf.Rad2Deg;
                me.ServerTeleport(open, yaw); lp.Yaw = yaw; lp.Pitch = 2; yield return new WaitForSecondsRealtime(1.0f);
                lp.script.on = true; lp.script.move = new Vector2(0, 1); yield return new WaitForSecondsRealtime(1.0f);
                HudOn(false); lp.freeCam = true; lp.Cam.fieldOfView = 38;
                var f = Quaternion.Euler(0, yaw, 0) * Vector3.forward; var r = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                for (int k = 0; k < 3; k++) { var body = me.transform.position; LookFrom(body + f * 3.4f + r * 1.7f + Vector3.up * 1.35f, body + f * 0.5f + Vector3.up * 0.92f); yield return Frames(1); }
                { var body = me.transform.position; LookFrom(body + f * 3.4f + r * 1.7f + Vector3.up * 1.35f, body + f * 0.5f + Vector3.up * 0.92f); }
                yield return Shot("run_" + hn);
                var view = me.GetComponentInChildren<AgniCharacterView>(); var an = view != null ? view.GetComponentInChildren<Animation>() : null; var rt = view != null ? view.GetComponentInChildren<AgniRetarget>() : null;
                Log("cgtrun " + hn + " hero=" + AgniHeroes.Names[me.Hero.Value] + " held=" + (ItemId)me.HeldGun.Value + " run=" + (an != null && an.IsPlaying("Run_Fwd_InPlace")) + " sprint=" + (an != null && an.IsPlaying("Sprint_InPlace")) + " retarget=" + (rt != null));
                lp.freeCam = false; HudOn(true); lp.script.on = false; lp.script.move = Vector2.zero; yield return new WaitForSecondsRealtime(0.4f);
            }
        }
    }
}
