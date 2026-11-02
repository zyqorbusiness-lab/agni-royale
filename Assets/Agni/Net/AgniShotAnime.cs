// AgniShot mode "anime" (v0.2.2): proof frames of the smaller, filled, anime India map. Shots come from a JSON list
// (-agniAnimeShots <file>): {"shots":[{"name":"air","cam":[x,y,z],"at":[x,y,z],"fov":55,"w":1080,"h":1350,"tps":false,"yaw":0,"pitch":-8}]}
// tps=true teleports the player to cam (on the ground) and uses the real third-person camera; otherwise a free camera.
using System.Collections;
using System.IO;
using UnityEngine;

namespace Agni.Net
{
    public partial class AgniShot
    {
        [System.Serializable] class AShot { public string name, village; public float[] cam; public float[] at; public float fov = 55; public int w = 1080, h = 1350; public bool tps, hud; public float yaw, pitch = -8, side = 1; }
        [System.Serializable] class AShots { public AShot[] shots; }
        IEnumerator AnimeMap()
        {
            yield return WaitPlaying(3f);
            var lp = AgniLocalPlayer.Instance; var me = lp.P;
            int trees = 0; if (Agni.Import.AgniIndiaTerrain.Tiles != null) foreach (var t in Agni.Import.AgniIndiaTerrain.Tiles) if (t != null) trees += t.terrainData.treeInstanceCount;
            int hamlets = 0; foreach (var v in AgniVillages.Villages) if (v.hamlet) hamlets++;
            int houses = AgniVillages.Root != null ? AgniVillages.Root.GetComponentsInChildren<LODGroup>(true).Length : -1;
            Log("ANIME map: villages=" + AgniVillages.Villages.Count + " hamlets=" + hamlets + " houses(LODGroups)=" + houses + " landmarks=" + (Agni.Import.AgniLandmarks.Root != null ? Agni.Import.AgniLandmarks.Root.childCount : -1)
                + " trees=" + trees + " loot live=" + AgniPickup.Live.Count + " spots=" + AgniVillages.Spots.Count + " vehicles=" + FindObjectsByType<AgniVehicle>(FindObjectsSortMode.None).Length
                + " players=" + AgniPlayer.All.Count + " arenaR=" + AgniMatch.ArenaRadius + " sky=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "none") + " gfx=" + Agni.UI.AgniMahal.Quality);
            var f = AgniNetConfig.Arg("-agniAnimeShots"); if (f == null || !File.Exists(f)) { Log("no -agniAnimeShots file"); yield break; }
            var list = JsonUtility.FromJson<AShots>(File.ReadAllText(f));
            foreach (var s in list.shots)
            {
                SetSize(s.w, s.h);
                var cp = new Vector3(s.cam[0], s.cam[1], s.cam[2]); var at = new Vector3(s.at[0], s.at[1], s.at[2]);
                if (s.tps && !string.IsNullOrEmpty(s.village))
                {   // a lane between two real houses of that village: stand beside the house nearest the chowk, look across at the next one
                    var v = AgniVillages.Villages.Find(x => x.name == s.village);
                    if (v != null)
                    {
                        (Vector2 c, float r) a = (v.c, 0), b = (v.c, 0); float ba = 1e9f, bb = 1e9f;
                        foreach (var fp in AgniVillages.Footprints) { float d = (fp.c - v.c).magnitude; if (d > v.r + 2) continue; if (d < ba) { bb = ba; b = a; ba = d; a = fp; } else if (d < bb) { bb = d; b = fp; } }
                        var toC = (v.c - a.c); if (toC.sqrMagnitude < 1) toC = Vector2.up; toC.Normalize(); var perp = new Vector2(-toC.y, toC.x) * s.side;
                        var stand = a.c + toC * (a.r + 3.5f) + perp * 2.5f; var look = b.c; var dir = look - stand;
                        cp = new Vector3(stand.x, 0, stand.y); s.yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 25f * s.side;
                        Log("village shot " + v.name + " stand " + stand + " houseA " + a.c + " houseB " + b.c);
                    }
                }
                if (s.tps)
                {
                    lp.freeCam = false; cp.y = Agni.Import.AgniIndia.H(cp.x, cp.z) + 0.05f; me.ServerTeleport(cp, s.yaw); lp.Yaw = s.yaw; lp.Pitch = s.pitch;
                    yield return new WaitForSecondsRealtime(2.5f); HudOn(s.hud);
                }
                else { HudOn(false); lp.freeCam = true; LookFrom(cp, at); lp.Cam.farClipPlane = 3000; }
                if (s.name.Contains("_nooutline")) Agni.Import.AgniAnime.ApplyTier(1); else Agni.Import.AgniAnime.ApplyTier(Agni.UI.AgniMahal.Quality);
                QualitySettings.shadows = s.name.Contains("_noshadow") ? ShadowQuality.Disable : ShadowQuality.All;
                if (s.name.Contains("_near1")) lp.Cam.nearClipPlane = 1f; else lp.Cam.nearClipPlane = 0.08f;
                lp.Cam.fieldOfView = s.fov; yield return new WaitForSecondsRealtime(1.2f); yield return Frames(6);
                yield return Shot(s.name);
            }
            lp.freeCam = false; HudOn(true);
        }
    }
}
