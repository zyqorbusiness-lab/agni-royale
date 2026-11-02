// AgniShot "run" mode — a full match recorded from the real game, frame by frame (Time.captureFramerate, so the video is
// smooth whatever this machine renders at): intro → loading → GUEST KHELO → lobby (hero + pet) → START → AGNI AIR plane →
// jump → freefall → parachute → land at a village → loot → drive → fight (the bot brain drives our fighter) → zone → result.
//   Unity -batchmode -force-glcore -executeMethod Agni.Net.EditorTools.AgniNetSetup.ShotBatch -agniShotScene Intro
//         -agniShot <dir> -agniShotMode run -agniShotW 1280 -agniShotH 576 -agniCountdown 5 -agniZoneScale 0.25
// Frames go to <dir>/frames/f000000.jpg…; <dir>/segments.txt lists "frame label speed" so the edit can cut / speed up parts.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Agni.UI;
using UnityEngine;

namespace Agni.Net
{
    [DefaultExecutionOrder(10000)]
    public partial class AgniShot
    {
        float perfT; int diagAt = -1; bool rec; int recEvery = 1, recTick, recSaved; string framesDir; StreamWriter segLog;

        void LateUpdate()
        {
            if (!rec) return;
            if (recTick++ % recEvery != 0) return;
            Canvas.ForceUpdateCanvases(); var sw = System.Diagnostics.Stopwatch.StartNew();
            var tex = RenderAll(); long r1 = sw.ElapsedMilliseconds; File.WriteAllBytes(Path.Combine(framesDir, "f" + recSaved.ToString("D6") + ".jpg"), tex.EncodeToJPG(86)); Destroy(tex); recSaved++;
            if (recSaved == -400) { foreach (var c in Camera.allCameras) { var tt = c.targetTexture; var sw2 = System.Diagnostics.Stopwatch.StartNew(); bool ov = Overlay(c); if (!ov) { c.targetTexture = rt; c.Render(); c.targetTexture = tt; } Log("cam " + c.name + " depth " + c.depth + " overlay " + ov + " mask " + c.cullingMask + " far " + c.farClipPlane + " " + sw2.ElapsedMilliseconds + " ms"); } }
            if (recSaved % 30 == 0) { float now = Time.realtimeSinceStartup; Log("perf frame " + recSaved + " render " + r1 + " ms, total " + sw.ElapsedMilliseconds + " ms, wall per saved frame " + ((now - perfT) / 30f).ToString("F2") + " s"); perfT = now; }
        }
        void Seg(string label, int every)
        {
            recEvery = every; recTick = 0;
            string line = recSaved + " " + label + " x" + every + " t=" + Time.time.ToString("F1");
            segLog.WriteLine(line); segLog.Flush(); Log("seg " + line);
        }
        IEnumerator Wait(float s) { float e = Time.time + s; while (Time.time < e) yield return null; }
        static float YawTo(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        IEnumerator Run()
        {
            PlayerPrefs.DeleteKey(AgniProfile.KName); PlayerPrefs.DeleteKey("agni.guestName"); PlayerPrefs.DeleteKey("agni.guest");
            int heroI = System.Array.IndexOf(AgniHeroes.Names, "Meera"); if (heroI < 0) heroI = 0; int petI = 0; for (int q = 0; q < Agni.Net.AgniPets.Names.Length; q++) if (Agni.Net.AgniPets.Names[q] == "Bagha") petI = q;
            Log("hero Meera=" + heroI + " pet " + Agni.Net.AgniPets.Names[petI]);
            PlayerPrefs.SetInt("agni.hero", (heroI + AgniHeroes.Names.Length - 2) % AgniHeroes.Names.Length); PlayerPrefs.SetInt("agni.pet", (petI + Agni.Net.AgniPets.Names.Length - 1) % Agni.Net.AgniPets.Names.Length); PlayerPrefs.Save();
            framesDir = Path.Combine(dir, "frames"); Directory.CreateDirectory(framesDir);
            segLog = new StreamWriter(Path.Combine(dir, "segments.txt"), false);
            Time.captureFramerate = 30; AgniNet.GameClock = true; rec = true; Seg("intro", 1);
            bool fromArena = AgniNetConfig.Has("-agniRunFromArena"); float t0 = Time.time;
            if (!fromArena) {
            // ---- loading screen → GUEST KHELO card
            AgniLoadingScreen ls = null; t0 = Time.time;
            while ((ls = FindAnyObjectByType<AgniLoadingScreen>()) == null && Time.time - t0 < 60) yield return null;
            if (ls == null) { Log("no loading screen"); yield break; }
            Seg("loading", 1); yield return Wait(2.0f);
            while (!ls.PopupOpen && Time.time - t0 < 90) { ls.OnTap(); yield return Wait(0.4f); }
            Seg("guest_card", 1); yield return Wait(3.0f);
            ls.ConfirmName(); Log("guest name " + AgniProfile.Name);
            // ---- lobby: switch hero twice, then the pet, then START
            AgniLobby lobby = null; t0 = Time.time;
            while ((lobby = FindAnyObjectByType<AgniLobby>()) == null && Time.time - t0 < 60) yield return null;
            if (lobby == null) { Log("no lobby"); yield break; }
            Seg("lobby", 1); yield return Wait(3.0f);
            lobby.StepHero(1); yield return Wait(2.2f); lobby.StepHero(1); yield return Wait(2.2f);
            lobby.StepPet(1); yield return Wait(2.6f);
            Seg("start", 1); lobby.OnStart();
            }
            // ---- match: warm-up, then the plane
            t0 = Time.time; AgniLocalPlayer lp = null;
            while ((lp = AgniLocalPlayer.Instance) == null || lp.P == null || AgniMatch.Instance == null) { if (Time.time - t0 > 120) { Log("no match"); yield break; } yield return null; }
            var me = lp.P; var m = AgniMatch.Instance; Seg("warmup", 1); diagAt = recSaved + 8;
            // this box renders on the CPU (no GPU): no realtime shadows and a shorter grass range keep a frame near a second
            var urpA = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset; float shadow0 = urpA != null ? urpA.shadowDistance : 0;
            if (urpA != null && AgniNetConfig.Has("-agniRunNoShadows")) urpA.shadowDistance = 0;
            float detailD = AgniNetConfig.ArgF("-agniRunDetail", 0); if (detailD > 0) foreach (var tr in Terrain.activeTerrains) tr.detailObjectDistance = Mathf.Min(tr.detailObjectDistance, detailD);
            float bmD = AgniNetConfig.ArgF("-agniRunBasemap", -1); if (bmD >= 0) foreach (var tr in Terrain.activeTerrains) tr.basemapDistance = bmD;
            float hideC = AgniNetConfig.ArgF("-agniRunCurtainHide", 0); if (hideC > 0) Agni.Net.AgniZoneView.HideWithin = hideC;
            float dens = AgniNetConfig.ArgF("-agniRunDensity", 0); if (dens > 0) foreach (var tr in Terrain.activeTerrains) tr.detailObjectDensity = Mathf.Min(tr.detailObjectDensity, dens);
            Log("terrains " + Terrain.activeTerrains.Length + " detail dist/dens " + (Terrain.activeTerrains.Length > 0 ? Terrain.activeTerrains[0].detailObjectDistance + "/" + Terrain.activeTerrains[0].detailObjectDensity : "-"));
            while (!me.InPlane) { if (Time.time - t0 > 160) { Log("never boarded"); yield break; } yield return null; }
            Seg("plane", 1);
            var a = m.PlaneA.Value; var dir3 = m.PlaneDir; var perp = new Vector3(-dir3.z, 0, dir3.x); float len = m.PlaneLen;
            lp.Yaw = Mathf.Atan2(dir3.x, dir3.z) * Mathf.Rad2Deg + 150f; lp.Pitch = 14f;
            // landing spot: the village nearest the line, ahead of the plane
            AgniVillages.Village best = null; float bestSide = 1e9f, bestAlong = 0;
            foreach (var v in AgniVillages.Villages)
            {
                var rel = new Vector3(v.c.x, 0, v.c.y) - new Vector3(a.x, 0, a.z); float along = Vector3.Dot(rel, dir3), sideD = Mathf.Abs(Vector3.Dot(rel, perp));
                if (along < 900f || along > len - 300f) continue; if (sideD < bestSide) { bestSide = sideD; best = v; bestAlong = along; }
            }
            Vector3 target = best != null ? new Vector3(best.c.x, 0, best.c.y) : m.PlanePos(m.PlaneT0.Value + len * 0.5f / AgniSkyCfg.PlaneSpeed);
            if (best != null) { var toC = (Vector3.zero - target); toC.y = 0; if (toC.sqrMagnitude > 1) target += toC.normalized * Mathf.Min(best.r * 0.5f, 30f); }
            target = AgniMatch.Ground(target);
            float jumpS = (best != null ? bestAlong : len * 0.5f) - 160f - 0.25f * bestSide;
            Log("target " + (best != null ? best.name : "mid-line") + " at " + target.ToString("F0") + " side " + bestSide.ToString("F0") + " jump at " + jumpS.ToString("F0") + " m of " + len.ToString("F0"));
            while (me.InPlane)
            {
                float s = Vector3.Dot(m.PlanePos(m.NowT) - a, dir3);
                lp.Yaw += 9f * Time.deltaTime; // slow orbit around the plane
                if (m.DoorOpen && s >= jumpS) { Seg("jump", 1); me.JumpRpc(); yield return Wait(0.3f); }
                yield return null;
            }
            // ---- freefall + parachute, steered at the village
            lp.script.on = true; bool chuteSeg = false;
            while (me.InAir)
            {
                var to = target - me.transform.position; to.y = 0; float dist = to.magnitude;
                lp.Yaw = Mathf.MoveTowardsAngle(lp.Yaw, YawTo(me.transform.position, target), 70f * Time.deltaTime);
                lp.Pitch = Mathf.MoveTowards(lp.Pitch, me.Air.Value == (byte)AirState.Chute ? 18f : 32f, 20f * Time.deltaTime);
                lp.script.move = new Vector2(0, Mathf.Clamp01(dist / 40f));
                if (!chuteSeg && me.Air.Value == (byte)AirState.Chute) { chuteSeg = true; Seg("chute", 1); }
                if (Time.time - t0 > 400) break;
                yield return null;
            }
            lp.script.move = Vector2.zero; Seg("landed", 1); Log("landed at " + me.transform.position.ToString("F0") + " " + (target - me.transform.position).magnitude.ToString("F0") + " m from the target");
            yield return Wait(1.2f);
            // ---- loot: walk to the nearest stacks (auto-pickup takes them)
            Seg("loot", 1); float lootEnd = Time.time + 22f; int got = 0; var lastP = me.transform.position; float stuckT = 0, side = 0;
            AgniPickup goal = null;
            while (Time.time < lootEnd && got < 4 && me.Alive)
            {
                if (goal == null || !goal.IsSpawned) { goal = null; float bd = 45f; foreach (var pk in AgniPickup.Live) { if (pk == null) continue; float d = Vector3.Distance(pk.transform.position, me.transform.position); if (d < bd) { bd = d; goal = pk; } } if (goal == null) break; }
                var to = goal.transform.position - me.transform.position; to.y = 0;
                if (to.magnitude < 0.9f) { yield return Wait(0.5f); if (goal == null || !goal.IsSpawned) got++; goal = null; continue; }
                lp.Yaw = Mathf.MoveTowardsAngle(lp.Yaw, YawTo(me.transform.position, goal.transform.position), 240f * Time.deltaTime); lp.Pitch = 12f;
                stuckT += Time.deltaTime; if (stuckT > 0.8f) { if ((me.transform.position - lastP).magnitude < 0.5f) side = side == 0 ? 1 : -side; else side = 0; stuckT = 0; lastP = me.transform.position; }
                lp.script.move = new Vector2(side, side != 0 ? 0.3f : 1f); yield return null;
            }
            lp.script.move = Vector2.zero; Log("loot picked " + got + " ammo " + me.Ammo.Value + " gun " + (ItemId)me.HeldGun.Value);
            // ---- drive: the nearest land vehicle
            AgniVehicle veh = null; float vd = 260f;
            foreach (var v in AgniVehicle.All) { if (v == null || v.kind == VKind.Boat) continue; float d = Vector3.Distance(v.transform.position, me.transform.position); if (d < vd) { vd = d; veh = v; } }
            if (veh != null)
            {
                Seg("to_vehicle", vd > 60 ? 2 : 1); Log("vehicle " + veh.kind + " " + vd.ToString("F0") + " m away");
                float vEnd = Time.time + 45f; stuckT = 0; side = 0; lastP = me.transform.position;
                while (Time.time < vEnd && me.Alive && Vector3.Distance(veh.transform.position, me.transform.position) > AgniPlayer.EnterRange - 0.6f)
                {
                    lp.Yaw = Mathf.MoveTowardsAngle(lp.Yaw, YawTo(me.transform.position, veh.transform.position), 240f * Time.deltaTime); lp.Pitch = 10f;
                    stuckT += Time.deltaTime; if (stuckT > 0.8f) { if ((me.transform.position - lastP).magnitude < 0.5f) side = side == 0 ? 1 : -side; else side = 0; stuckT = 0; lastP = me.transform.position; }
                    lp.script.move = new Vector2(side, side != 0 ? 0.3f : 1f); yield return null;
                }
                lp.script.move = Vector2.zero; me.VehicleRpc(); yield return Wait(0.8f);
                if (me.InVehicle)
                {
                    Seg("drive", 1); var z = AgniZone.Instance; float dEnd = Time.time + 14f;
                    while (Time.time < dEnd && me.Alive && me.InVehicle)
                    {
                        var goalP = z != null ? z.CenterNow() : Vector3.zero;
                        lp.Yaw = Mathf.MoveTowardsAngle(lp.Yaw, YawTo(me.transform.position, goalP), 30f * Time.deltaTime); lp.Pitch = 8f;
                        float steerErr = Mathf.DeltaAngle(veh.transform.eulerAngles.y, YawTo(me.transform.position, goalP));
                        lp.script.move = new Vector2(Mathf.Clamp(steerErr / 35f, -1f, 1f), 1f); yield return null;
                    }
                    lp.script.move = Vector2.zero; yield return Wait(1.5f); me.VehicleRpc(); yield return Wait(1.0f);
                    Log("drove to " + me.transform.position.ToString("F0") + " in vehicle=" + me.InVehicle);
                }
                else Log("could not get in");
            }
            else Log("no vehicle within 260 m");
            // ---- fight: the bot brain drives our fighter until the match ends
            Seg("fight", 2); var brain = new AgniBotBrain(); bool deadSeg = false; float hardEnd = Time.time + 520f;
            while (m != null && m.Phase.Value != (byte)MatchPhase.Ended && Time.time < hardEnd)
            {
                if (me.Alive || me.Knocked)
                {
                    var inp = brain.Think(me, Time.deltaTime);
                    lp.Yaw = Mathf.MoveTowardsAngle(lp.Yaw, inp.yaw, 360f * Time.deltaTime); lp.Pitch = Mathf.Clamp(inp.pitch, -40f, 40f);
                    lp.script.move = inp.move; lp.script.fire = inp.Has(AgniInput.Fire); lp.script.interact = inp.Has(AgniInput.Interact);
                }
                else if (!deadSeg) { deadSeg = true; lp.script = default; Log("we fell, placement #" + me.Placement.Value + " kills " + me.Kills.Value); Seg("dead", 1); yield return Wait(4f); Seg("spectate", 6); }
                yield return null;
            }
            lp.script = default;
            Seg("result", 1); Log("match end: winner team " + (m != null ? m.WinnerTeam.Value : -9) + " ours " + me.Team.Value + " placement " + me.Placement.Value + " kills " + me.Kills.Value);
            yield return Wait(7f);
            if (urpA != null) urpA.shadowDistance = shadow0;
            rec = false; Time.captureFramerate = 0; segLog.WriteLine(recSaved + " end"); segLog.Close();
            Log("frames " + recSaved);
        }
    }
}
