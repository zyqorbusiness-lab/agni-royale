// Agni Royale — in-match HUD (landscape 2400×1080 reference, our own layout and art):
// top-left alive/kills/zone, top-right minimap + kill feed, bottom-centre HP (knock bar when down),
// left joystick, right look-drag, FIRE (×2), JUMP, CROUCH, RELOAD, weapon panel with the THROWABLE slot
// (tap = switch, ▸ = next kind, count badge, cook ring on the fire button), PATTI heal, REVIVE (hold), flash white-out.
using System.Collections.Generic;
using Agni.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Agni.Net
{
    public class AgniHud : MonoBehaviour
    {
        AgniLocalPlayer lp; AgniPlayer P => lp.P; Canvas canvas; RectTransform safe;
        Text alive, kills, zone, net, center, ammo, hpText, toast, endTitle, endSub, cookTxt, throwCount, reviveTxt;
        Image hpFill, knockFill, flash, cookRing, throwIcon, slotGunBg, slotThrowBg, hurtArc, reviveFill, healFill, hit;
        RectTransform feed, map, mapZone, mapNext, mapMe, stickBase, stickKnob, squadList; GameObject reviveBtn, endPanel, healBtn, knockPanel, cancelBtn, gaadiBtn; Text healCount, gaadiTxt, speedTxt;
        readonly List<(Text t, float at)> feedLines = new List<(Text, float)>(); readonly List<RectTransform> mapDots = new List<RectTransform>();
        float toastUntil, hitUntil, hurtUntil; public bool FireHeld => fireA.held || fireB.held; public bool ReviveHeld => revive != null && revive.held; public bool StickActive => stickZone != null && stickZone.active;
        Hold fireA, fireB, revive; StickZone stickZone;
        static readonly Dictionary<ThrowKind, Sprite> icons = new Dictionary<ThrowKind, Sprite>();

        public static AgniHud Create(AgniLocalPlayer lp)
        {
            var go = new GameObject("AgniHud"); var h = go.AddComponent<AgniHud>(); h.lp = lp; h.Build(); return h;
        }

        void Build()
        {
            canvas = CanvasMaker.Make("MatchHUD", lp.UICam, 0.5f, 20, out safe); canvas.transform.SetParent(transform, false); CanvasMaker.EnsureEventSystem();
            var s = safe;
            // touch zones first (under everything)
            var look = K.R("LookZone", canvas.transform); look.Fill(); look.anchorMin = new Vector2(0.42f, 0); var li = look.gameObject.AddComponent<Image>(); li.color = new Color(0, 0, 0, 0); look.gameObject.AddComponent<LookZone>().lp = lp;
            var sz = K.R("StickZone", canvas.transform); sz.Fill(); sz.anchorMax = new Vector2(0.42f, 0.75f); var si = sz.gameObject.AddComponent<Image>(); si.color = new Color(0, 0, 0, 0); stickZone = sz.gameObject.AddComponent<StickZone>(); stickZone.lp = lp;
            stickBase = K.Img(s, "StickBase", "circle", new Color(1, 1, 1, 0.12f)).rectTransform.Place(new Vector2(0, 0), new Vector2(330, 250), new Vector2(300, 300), new Vector2(0.5f, 0.5f));
            K.Img(stickBase, "Ring", "ring", new Color(1, 1, 1, 0.35f)).rectTransform.Fill();
            stickKnob = K.Img(stickBase, "Knob", "circle", new Color(1, 1, 1, 0.55f)).rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            stickZone.baseRt = stickBase; stickZone.knob = stickKnob;

            // ---- top-left: alive / kills / zone
            var tl = Chip(s, new Vector2(0, 1), new Vector2(40, -30), new Vector2(560, 64)); alive = K.Txt(tl, "Alive", "", 34, Color.white, TextAnchor.MiddleLeft, true); alive.rectTransform.Fill(24, 0, 0, 0);
            kills = K.Txt(tl, "Kills", "", 34, Pal.Gold, TextAnchor.MiddleRight, true); kills.rectTransform.Fill(0, 0, 24, 0);
            var zc = Chip(s, new Vector2(0, 1), new Vector2(40, -104), new Vector2(560, 56)); zone = K.Txt(zc, "Zone", "", 27, Pal.Cream, TextAnchor.MiddleLeft); zone.rectTransform.Fill(24, 0, 10, 0); zone.fontStyle = FontStyle.Bold;
            squadList = K.R("Squad", s).Place(new Vector2(0, 1), new Vector2(40, -184), new Vector2(380, 260), new Vector2(0, 1));
            net = K.Txt(s, "Net", "", 22, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter); net.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(700, 30), new Vector2(0.5f, 1));

            // ---- top-right: minimap + feed
            map = K.R("Map", s).Place(new Vector2(1, 1), new Vector2(-40, -26), new Vector2(300, 300), new Vector2(1, 1));
            K.Img(map, "Bg", "circle", new Color(0.12f, 0.22f, 0.16f, 0.72f)).rectTransform.Fill();
            K.Img(map, "Rim", "ring", new Color(1, 1, 1, 0.5f)).rectTransform.Fill(-3, -3, -3, -3);
            mapZone = K.Img(map, "Zone", "ring", new Color(1f, 0.55f, 0.25f, 0.95f)).rectTransform; mapNext = K.Img(map, "Next", "ring", new Color(1, 1, 1, 0.9f)).rectTransform;
            mapMe = K.Img(map, "Me", "ic_chevron", Pal.Gold).rectTransform; mapMe.sizeDelta = new Vector2(34, 34);
            feed = K.R("Feed", s).Place(new Vector2(1, 1), new Vector2(-40, -350), new Vector2(720, 220), new Vector2(1, 1));

            // ---- bottom-centre HP
            var hpBg = K.Img(s, "HpBg", "rr_16", new Color(0, 0, 0, 0.55f)); hpBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(620, 40), new Vector2(0.5f, 0));
            hpFill = K.Img(hpBg.transform, "Fill", "rr_16", new Color(0.95f, 0.95f, 0.92f)); hpFill.rectTransform.Fill(4, 4, 4, 4); hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.sprite = K.S("rr_16");
            knockFill = K.Img(hpBg.transform, "Knock", "rr_16", new Color(0.9f, 0.18f, 0.15f)); knockFill.rectTransform.Fill(4, 4, 4, 4); knockFill.type = Image.Type.Filled; knockFill.fillMethod = Image.FillMethod.Horizontal;
            hpText = K.Txt(hpBg.transform, "Hp", "", 24, Pal.Ink, TextAnchor.MiddleCenter, true); hpText.rectTransform.Fill();
            healFill = K.Img(s, "HealBar", "rr_16", new Color(0.4f, 0.9f, 0.5f, 0.9f)); healFill.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 86), new Vector2(300, 14), new Vector2(0.5f, 0)); healFill.type = Image.Type.Filled; healFill.fillMethod = Image.FillMethod.Horizontal;

            // ---- right: weapon panel (gun slot + throwable slot)
            var wp = K.R("Weapons", s).Place(new Vector2(1, 0), new Vector2(-40, 640), new Vector2(560, 130), new Vector2(1, 0));
            var gs = K.Btn(wp, "SlotGun", null, Pal.A(Pal.Navy, 0.8f), 10, () => P.SlotRpc(0), "btn_24"); ((RectTransform)gs.transform).Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(300, 120), new Vector2(0, 0.5f)); slotGunBg = gs.GetComponent<Image>();
            K.Txt(gs.transform, "Name", "AGNI-47", 26, Pal.Cream, TextAnchor.UpperLeft, true).rectTransform.Fill(20, 0, 0, 12);
            ammo = K.Txt(gs.transform, "Ammo", "", 44, Color.white, TextAnchor.LowerRight, true); ammo.rectTransform.Fill(0, 8, 18, 0);
            var ts = K.Btn(wp, "SlotThrow", null, Pal.A(Pal.Navy, 0.8f), 10, () => { if (P.Slot.Value == 1) lp.NextThrowable(); else P.SlotRpc(1); }, "btn_24"); ((RectTransform)ts.transform).Place(new Vector2(0, 0.5f), new Vector2(316, 0), new Vector2(150, 120), new Vector2(0, 0.5f)); slotThrowBg = ts.GetComponent<Image>();
            throwIcon = K.Img(ts.transform, "Icon", null, Color.white); throwIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(84, 84)); throwIcon.preserveAspect = true;
            throwCount = K.Txt(ts.transform, "Count", "", 30, Color.white, TextAnchor.LowerRight, true); throwCount.rectTransform.Fill(0, 4, 12, 0); throwCount.Outline(new Color(0, 0, 0, 0.6f), 2);
            var nx = K.Btn(wp, "NextThrow", null, Pal.A(Pal.Saffron, 0.95f), 10, () => lp.NextThrowable(), "btn_16"); ((RectTransform)nx.transform).Place(new Vector2(0, 0.5f), new Vector2(476, 0), new Vector2(80, 120), new Vector2(0, 0.5f));
            K.Icon(nx.transform, "ic_chevron", new Vector2(44, 44), Color.white);

            // ---- action buttons
            fireA = HoldBtn(s, "Fire", new Vector2(1, 0), new Vector2(-250, 170), 250, Pal.A(Pal.SaffronDeep, 0.9f), "FIRE");
            cookRing = K.Img(fireA.transform, "Cook", "ring", new Color(1f, 0.85f, 0.3f, 1f)); cookRing.rectTransform.Fill(-10, -10, -10, -10); cookRing.type = Image.Type.Filled; cookRing.fillMethod = Image.FillMethod.Radial360; cookRing.fillOrigin = 2;
            cookTxt = K.Txt(fireA.transform, "CookT", "", 30, Color.white, TextAnchor.LowerCenter, true); cookTxt.rectTransform.Fill(0, 26, 0, 0);
            fireB = HoldBtn(s, "FireL", new Vector2(0, 0), new Vector2(150, 560), 150, Pal.A(Pal.SaffronDeep, 0.75f), "");
            K.Icon(fireB.transform, "ring", new Vector2(70, 70), Color.white);
            Act(s, "Jump", "UPAR", new Vector2(-560, 120), 140, () => P.JumpRpc());
            Act(s, "Crouch", "JHUKO", new Vector2(-560, 290), 130, () => P.CrouchRpc());
            Act(s, "Reload", "RELOAD", new Vector2(-430, 420), 120, () => P.ReloadRpc());
            healBtn = Act(s, "Heal", "PATTI", new Vector2(-700, 140), 120, () => P.HealRpc()).gameObject; healCount = K.Txt(healBtn.transform, "N", "", 26, Pal.Gold, TextAnchor.LowerRight, true); healCount.rectTransform.Fill(0, 6, 14, 0);
            cancelBtn = Act(s, "CancelThrow", "RADD", new Vector2(-250, 470), 110, () => P.SlotRpc(0)).gameObject;
            var gb = Act(s, "Gaadi", "BAITHO", new Vector2(-700, 320), 130, () => P.VehicleRpc()); gaadiBtn = gb.gameObject; gaadiTxt = gb.transform.Find("L").GetComponent<Text>(); gaadiBtn.SetActive(false);
            speedTxt = K.Txt(s, "Speed", "", 46, Color.white, TextAnchor.MiddleCenter, true); speedTxt.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(520, 70)); speedTxt.Outline(new Color(0, 0, 0, 0.6f), 3);
            revive = HoldBtn(s, "Revive", new Vector2(0.5f, 0), new Vector2(0, 260), 170, new Color(0.2f, 0.55f, 0.35f, 0.92f), "UTHAO"); reviveBtn = revive.gameObject;
            reviveFill = K.Img(reviveBtn.transform, "Prog", "ring", new Color(0.6f, 1f, 0.6f)); reviveFill.rectTransform.Fill(-8, -8, -8, -8); reviveFill.type = Image.Type.Filled; reviveFill.fillMethod = Image.FillMethod.Radial360;

            // ---- centre: crosshair, hit marker, messages
            var ch = K.Img(canvas.transform, "Cross", "ring", new Color(1, 1, 1, 0.8f)); ch.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            K.Img(ch.transform, "Dot", "circle", new Color(1, 1, 1, 0.9f)).rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            hit = K.Img(canvas.transform, "Hit", "ic_close", Color.white); hit.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58));
            hurtArc = K.Img(canvas.transform, "Hurt", "glow", new Color(0.9f, 0.1f, 0.05f, 0)); hurtArc.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 160), new Vector2(0.5f, -2.2f));
            center = K.Txt(s, "Center", "", 64, Color.white, TextAnchor.MiddleCenter, true); center.rectTransform.Place(new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(1600, 120)); center.Outline(new Color(0, 0, 0, 0.55f), 3);
            toast = K.Txt(s, "Toast", "", 34, Pal.Gold, TextAnchor.MiddleCenter, true); toast.rectTransform.Place(new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(1200, 60)); toast.Outline(new Color(0, 0, 0, 0.6f), 2);
            reviveTxt = K.Txt(s, "ReviveTxt", "", 34, new Color(0.7f, 1f, 0.7f), TextAnchor.MiddleCenter, true); reviveTxt.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 470), new Vector2(1200, 50)); reviveTxt.Outline(new Color(0, 0, 0, 0.6f), 2);
            knockPanel = K.Img(canvas.transform, "KnockTint", "vgrad", new Color(0.55f, 0.02f, 0.02f, 0.45f)).gameObject; ((RectTransform)knockPanel.transform).Fill();
            flash = K.Img(canvas.transform, "Flash", null, new Color(1, 1, 1, 0)); flash.rectTransform.Fill();
            // ---- end of match
            var ep = K.R("End", canvas.transform); ep.Fill(); endPanel = ep.gameObject;
            var band = K.Img(ep, "Band", "hgrad", new Color(1f, 0.55f, 0.15f, 0.9f)); band.rectTransform.Place(new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(2400, 230));
            endTitle = K.Txt(ep, "Title", "", 120, Color.white, TextAnchor.MiddleCenter, true); endTitle.rectTransform.Place(new Vector2(0.5f, 0.72f), new Vector2(0, 10), new Vector2(2000, 180)); endTitle.Outline(new Color(0.5f, 0.12f, 0.02f, 0.9f), 5);
            endSub = K.Txt(ep, "Sub", "", 44, Pal.Cream, TextAnchor.MiddleCenter, true); endSub.rectTransform.Place(new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(2000, 80)); endSub.Outline(new Color(0, 0, 0, 0.6f), 3);
            endPanel.SetActive(false);

            AgniMatch.OnFeed += AddFeed; AgniMatch.OnEnded += OnEnded;
        }
        void OnDestroy() { AgniMatch.OnFeed -= AddFeed; AgniMatch.OnEnded -= OnEnded; }

        RectTransform Chip(RectTransform s, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var r = K.R("Chip", s).Place(anchor, pos, size, anchor); var bg = K.Img(r, "Bg", "rr_24", Pal.A(Pal.Navy, 0.62f)); bg.rectTransform.Fill(); return r;
        }
        Hold HoldBtn(RectTransform s, string n, Vector2 anchor, Vector2 pos, float size, Color c, string label)
        {
            var r = K.R(n, s).Place(anchor, pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = K.S("circle"); bg.color = c; bg.raycastTarget = true;
            K.Img(r, "Rim", "ring", new Color(1, 1, 1, 0.55f)).rectTransform.Fill(-2, -2, -2, -2);
            if (!string.IsNullOrEmpty(label)) { var t = K.Txt(r, "L", label, (int)(size * 0.16f), Color.white, TextAnchor.MiddleCenter, true); t.rectTransform.Fill(); t.Shadow(new Color(0, 0, 0, 0.5f), 0, -3); }
            return r.gameObject.AddComponent<Hold>();
        }
        Button Act(RectTransform s, string n, string label, Vector2 pos, float size, System.Action a)
        {
            var b = K.Btn(s, n, null, Pal.A(Pal.Navy, 0.72f), 10, a, "btn_40"); var r = (RectTransform)b.transform; r.Place(new Vector2(1, 0), pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            b.GetComponent<Image>().sprite = K.S("circle"); b.GetComponent<Image>().type = Image.Type.Simple; var sh = b.transform.Find("Shadow"); if (sh) sh.gameObject.SetActive(false);
            K.Img(r, "Rim", "ring", new Color(1, 1, 1, 0.45f)).rectTransform.Fill(-2, -2, -2, -2);
            var t = K.Txt(r, "L", label, (int)(size * 0.2f), Color.white, TextAnchor.MiddleCenter, true); t.rectTransform.Fill(); return b;
        }

        void Update()
        {
            if (lp == null || P == null) return;
            var m = AgniMatch.Instance; var z = AgniZone.Instance; float now = Time.time;
            alive.text = (m != null ? m.AliveCount.Value : 0) + " ZINDA"; kills.text = "☠ " + P.Kills.Value;
            net.text = AgniNet.Role.ToString().ToUpper() + " · " + AgniNet.Ping() + (m != null ? " · " + m.Humans.Value + " khiladi online" : "");
            // zone line
            if (z != null && z.PhaseIndex.Value >= 0) { float sec = z.SecondsToNext(out bool shr); zone.text = (shr ? "Agni Rekha simat rahi " : "Rekha simtegi ") + Clock(sec) + (z.Inside(P.transform.position) ? "" : "  · BAHAR HO!"); zone.color = z.Inside(P.transform.position) ? Pal.Cream : new Color(1f, 0.55f, 0.4f); }
            else zone.text = "Safe zone jaldi dikhega";
            // centre message
            if (m != null && m.Phase.Value == (byte)MatchPhase.Warmup) { double left = m.StartAt.Value - (AgniNet.NM != null ? AgniNet.NM.ServerTime.Time : 0); center.text = "Match " + Mathf.CeilToInt(Mathf.Max(0, (float)left)) + " sec me shuru"; }
            else if (P.Knocked) center.text = "GIR GAYE!  " + Mathf.CeilToInt(P.Knock.Value / AgniPlayer.BleedPerSec) + "s";
            else if (P.Dead && (m == null || m.Phase.Value != (byte)MatchPhase.Ended)) center.text = "Tum #" + P.Placement.Value + " pe gire";
            else center.text = "";
            knockPanel.SetActive(P.Knocked);
            // HP
            hpFill.fillAmount = P.Hp.Value / AgniPlayer.MaxHp; knockFill.enabled = P.Knocked; knockFill.fillAmount = P.Knock.Value / AgniPlayer.KnockMax;
            hpText.text = P.Knocked ? "" : Mathf.CeilToInt(P.Hp.Value) + " / " + (int)AgniPlayer.MaxHp; hpFill.color = P.Hp.Value < 60 ? new Color(1f, 0.6f, 0.5f) : new Color(0.95f, 0.95f, 0.92f);
            healFill.enabled = P.Healing.Value; if (P.Healing.Value) healFill.fillAmount = Mathf.PingPong(now, 1f);
            healCount.text = P.Medkits.Value.ToString(); healBtn.SetActive(P.Medkits.Value > 0 && P.Alive);
            // vehicle: BAITHO near a free vehicle, UTRO while seated; the driver sees the speed
            var nearV = P.Alive && !P.InVehicle ? AgniVehicle.Nearest(P.transform.position, AgniPlayer.EnterRange) : null;
            gaadiBtn.SetActive(P.Alive && (P.InVehicle || (nearV != null && nearV.FreeSeat(true) >= 0))); gaadiTxt.text = P.InVehicle ? "UTRO" : "BAITHO";
            var rv = P.Vehicle; speedTxt.text = rv != null && P.Driving ? Mathf.RoundToInt(rv.SpeedKmh) + " km/h" : "";
            // weapons
            ammo.text = (P.Reloading.Value ? "<size=30>RELOAD…</size>" : P.Ammo.Value.ToString()) + "<size=28> / " + P.Reserve.Value + "</size>";
            var k = (ThrowKind)P.Sel.Value; int cnt = P.Count(k); if (cnt == 0) { var f = P.FirstOwned(); if (f != ThrowKind.None) { k = f; cnt = P.Count(f); } }
            throwIcon.sprite = Icon(k); throwIcon.color = cnt > 0 ? Color.white : new Color(1, 1, 1, 0.3f); throwCount.text = cnt > 0 ? "×" + cnt : "";
            slotGunBg.color = P.Slot.Value == 0 ? Pal.A(Pal.Saffron, 0.95f) : Pal.A(Pal.Navy, 0.8f); slotThrowBg.color = P.Slot.Value == 1 ? Pal.A(Pal.Saffron, 0.95f) : Pal.A(Pal.Navy, 0.8f);
            cancelBtn.SetActive(P.Slot.Value == 1 && FireHeld);
            // cook timer on the fire button
            double cs = P.CookStart.Value; var d = ThrowMath.Def((ThrowKind)P.Sel.Value);
            if (cs > 0 && AgniNet.NM != null) { float el = (float)(AgniNet.NM.ServerTime.Time - cs); cookRing.enabled = true; cookRing.fillAmount = 1f - Mathf.Clamp01(el / d.fuse); cookRing.color = Color.Lerp(new Color(1, 0.3f, 0.2f), new Color(1, 0.88f, 0.4f), cookRing.fillAmount); cookTxt.text = (d.fuse - el).ToString("0.0"); }
            else { cookRing.enabled = false; cookTxt.text = P.Slot.Value == 1 ? d.name : ""; }
            // revive
            AgniPlayer downed = null; foreach (var o in AgniPlayer.All) if (o != P && o.Knocked && o.Team.Value == P.Team.Value && Vector3.Distance(o.transform.position, P.transform.position) < AgniPlayer.ReviveRange) downed = o;
            reviveBtn.SetActive(downed != null && P.Alive); if (downed != null) reviveFill.fillAmount = downed.ReviveProg.Value;
            reviveTxt.text = P.Knocked && P.ReviveProg.Value > 0 ? "Saathi utha raha hai… " + Mathf.RoundToInt(P.ReviveProg.Value * 100) + "%" : downed != null ? downed.Nick + " ko uthao (hold)" : "";
            // feedback
            toast.enabled = now < toastUntil; hit.enabled = now < hitUntil; var hc = hurtArc.color; hc.a = Mathf.Clamp01((hurtUntil - now) / 0.6f) * 0.75f; hurtArc.color = hc;
            flash.color = new Color(1, 1, 1, lp.FlashAlpha);
            for (int i = feedLines.Count - 1; i >= 0; i--) { var (t, at) = feedLines[i]; float a = Mathf.Clamp01(7f - (now - at)); var c = t.color; c.a = a; t.color = c; if (a <= 0) { Destroy(t.gameObject); feedLines.RemoveAt(i); } }
            UpdateMap(z); UpdateSquad();
        }
        static string Clock(float s) { int t = Mathf.CeilToInt(s); return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }

        void UpdateMap(AgniZone z)
        {
            float R = AgniMatch.ArenaRadius * 1.15f, half = 150f; Vector2 W2M(Vector3 w) => new Vector2(w.x, w.z) / R * half;
            if (z != null) { mapZone.anchoredPosition = W2M(z.CenterNow()); float r = z.RadiusNow() / R * half * 2; mapZone.sizeDelta = new Vector2(r, r); mapNext.anchoredPosition = W2M(z.ToC.Value); float rn = z.ToR.Value / R * half * 2; mapNext.sizeDelta = new Vector2(rn, rn); mapNext.gameObject.SetActive(z.PhaseIndex.Value >= 0); }
            mapMe.anchoredPosition = W2M(P.transform.position); mapMe.localRotation = Quaternion.Euler(0, 0, -lp.Yaw + 90);
            int n = 0;
            foreach (var o in AgniPlayer.All)
            {
                if (o == P || o.Team.Value != P.Team.Value || o.Dead) continue;
                if (mapDots.Count <= n) { var d = K.Img(map, "Mate", "circle", new Color(0.4f, 0.8f, 1f)).rectTransform; d.sizeDelta = new Vector2(18, 18); mapDots.Add(d); }
                mapDots[n].gameObject.SetActive(true); mapDots[n].anchoredPosition = W2M(o.transform.position); n++;
            }
            for (int i = n; i < mapDots.Count; i++) mapDots[i].gameObject.SetActive(false);
        }
        readonly List<(Text name, Image hp)> squadRows = new List<(Text, Image)>();
        void UpdateSquad()
        {
            int i = 0;
            foreach (var o in AgniPlayer.All)
            {
                if (o == P || o.Team.Value != P.Team.Value) continue;
                if (squadRows.Count <= i)
                {
                    var row = K.R("Row" + i, squadList).Place(new Vector2(0, 1), new Vector2(0, -i * 62), new Vector2(380, 56), new Vector2(0, 1));
                    K.Img(row, "Bg", "rr_16", Pal.A(Pal.Navy, 0.5f)).rectTransform.Fill();
                    var t = K.Txt(row, "N", "", 25, Color.white, TextAnchor.UpperLeft, true); t.rectTransform.Fill(14, 0, 8, 6);
                    var hb = K.Img(row, "Hp", "rr_16", Color.white); hb.rectTransform.Place(new Vector2(0, 0), new Vector2(14, 8), new Vector2(350, 10), new Vector2(0, 0)); hb.type = Image.Type.Filled; hb.fillMethod = Image.FillMethod.Horizontal;
                    squadRows.Add((t, hb));
                }
                var (nm, hp) = squadRows[i]; nm.transform.parent.gameObject.SetActive(true);
                nm.text = o.Nick + (o.Dead ? "  ✖" : o.Knocked ? "  GIRA" : ""); nm.color = o.Dead ? new Color(1, 1, 1, 0.4f) : o.Knocked ? new Color(1, 0.6f, 0.5f) : Color.white;
                hp.fillAmount = o.Dead ? 0 : o.Knocked ? o.Knock.Value / AgniPlayer.KnockMax : o.Hp.Value / AgniPlayer.MaxHp; hp.color = o.Knocked ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.9f, 0.95f, 0.9f);
                i++; if (i >= 3) break;
            }
            for (int j = i; j < squadRows.Count; j++) squadRows[j].name.transform.parent.gameObject.SetActive(false);
        }

        // ---------------- events
        void AddFeed(string killer, string victim, string weapon, int type)
        {
            var mine = killer == P.Nick || victim == P.Nick;
            string verb = type == 1 ? " ne gira diya " : type == 2 ? " ne utha liya " : " ne khatam kiya ";
            var t = K.Txt(feed, "Line", "<b>" + killer + "</b>" + verb + "<b>" + victim + "</b>  <size=22>[" + weapon + "]</size>", 26, mine ? Pal.Gold : Color.white, TextAnchor.UpperRight); t.Outline(new Color(0, 0, 0, 0.55f), 2);
            feedLines.Insert(0, (t, Time.time)); while (feedLines.Count > 5) { Destroy(feedLines[feedLines.Count - 1].t.gameObject); feedLines.RemoveAt(feedLines.Count - 1); }
            for (int i = 0; i < feedLines.Count; i++) feedLines[i].t.rectTransform.Place(new Vector2(1, 1), new Vector2(0, -i * 40), new Vector2(720, 40), new Vector2(1, 1));
        }
        void OnEnded(int win)
        {
            endPanel.SetActive(true); bool won = win == P.Team.Value; int place = won ? 1 : Mathf.Max(1, P.Placement.Value);
            endTitle.text = won ? "AGNI VIJAY!" : place <= 5 ? "Bas thoda sa reh gaya!" : place <= 20 ? "Achha khela!" : "Agli baar pakka!";
            endSub.text = "#" + place + " / " + AgniNetConfig.MaxPlayers + "   ·   " + P.Kills.Value + " kill   ·   Lobby me wapas jaa rahe…";
            if (won) AgniCamRig.Trauma = 0.4f;
        }
        public void Toast(string s) { toast.text = s; toastUntil = Time.time + 1.6f; }
        public void HitMarker(float dmg, bool head, bool big) { hitUntil = Time.time + (big ? 0.35f : 0.12f); hit.color = big ? new Color(1f, 0.3f, 0.2f) : head ? new Color(1f, 0.85f, 0.3f) : Color.white; hit.rectTransform.sizeDelta = Vector2.one * (big ? 80 : 58); }
        public void Hurt(Vector3 from, float dmg)
        {
            hurtUntil = Time.time + 0.6f; var to = from - P.transform.position; float a = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - lp.Yaw;
            hurtArc.rectTransform.localRotation = Quaternion.Euler(0, 0, -a);
        }

        public static Sprite Icon(ThrowKind k)
        {
            if (k == ThrowKind.None) k = ThrowKind.Gola;
            if (icons.TryGetValue(k, out var s) && s != null) return s;
            int n = 96; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color32[n * n];
            Color body = ThrowMath.Def(k).tint;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f; Color c = new Color(0, 0, 0, 0);
                    switch (k)
                    {
                        case ThrowKind.Gola: { float e = (u * u) / (0.27f * 0.27f) + ((v + 0.06f) * (v + 0.06f)) / (0.33f * 0.33f); if (e < 1) c = Color.Lerp(body * 1.25f, body * 0.7f, (u + v + 0.5f)); if (Mathf.Abs(v + 0.06f) < 0.025f && e < 1) c = body * 0.6f; if (v > 0.25f && v < 0.36f && Mathf.Abs(u) < 0.07f) c = new Color(0.8f, 0.66f, 0.35f); if (u > 0.05f && u < 0.11f && v > 0.12f && v < 0.38f) c = new Color(0.75f, 0.73f, 0.68f); c.a = c.a > 0 ? 1 : 0; break; }
                        case ThrowKind.Dhuan: { if (Mathf.Abs(u) < 0.2f && Mathf.Abs(v) < 0.34f) { c = Color.Lerp(body * 1.2f, body * 0.75f, u + 0.5f); if (Mathf.Abs(v - 0.12f) < 0.035f) c = new Color(0.95f, 0.95f, 0.93f); c.a = 1; } if (v > 0.34f && v < 0.42f && Mathf.Abs(u) < 0.09f) c = new Color(0.3f, 0.3f, 0.3f, 1); break; }
                        case ThrowKind.Patakha: { if (Mathf.Abs(u) < 0.15f && Mathf.Abs(v + 0.04f) < 0.32f) { c = Color.Lerp(body * 1.25f, body * 0.7f, u + 0.5f); if (Mathf.Abs(Mathf.Abs(v + 0.04f) - 0.22f) < 0.03f) c = new Color(0.95f, 0.75f, 0.3f); c.a = 1; } if (v > 0.28f && v < 0.4f && Mathf.Abs(u - (v - 0.28f) * 0.5f) < 0.02f) c = new Color(0.35f, 0.3f, 0.25f, 1); float dd = new Vector2(u - 0.07f, v - 0.42f).magnitude; if (dd < 0.07f) c = Color.Lerp(new Color(1, 0.95f, 0.6f, 1), new Color(1, 0.55f, 0.2f, 0), dd / 0.07f); break; }
                        case ThrowKind.Deewar: { float r = new Vector2(u, v + 0.45f).magnitude; float ang = Mathf.Atan2(u, v + 0.45f); if (r > 0.5f && r < 0.78f && Mathf.Abs(ang) < 0.75f) { c = Color.Lerp(body * 1.2f, body * 0.7f, (r - 0.5f) / 0.28f); float seg = Mathf.Repeat(ang * 3.4f, 1f); if (seg < 0.08f || Mathf.Abs(r - 0.64f) < 0.012f) c = new Color(1f, 0.55f, 0.2f); c.a = 1; } break; }
                    }
                    px[y * n + x] = c;
                }
            t.SetPixels32(px); t.Apply(); s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100); icons[k] = s; return s;
        }
    }

    public class Hold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool held; Vector3 s0 = Vector3.one;
        public void OnPointerDown(PointerEventData e) { held = true; transform.localScale = s0 * 0.94f; }
        public void OnPointerUp(PointerEventData e) { held = false; transform.localScale = s0; }
        public void OnPointerExit(PointerEventData e) { }
        void OnDisable() { held = false; transform.localScale = s0; }
    }
    public class LookZone : MonoBehaviour, IDragHandler
    {
        public AgniLocalPlayer lp;
        public void OnDrag(PointerEventData e) { if (lp != null) lp.lookDelta += e.delta * (2400f / Mathf.Max(1, Screen.width)) * 0.9f; }
    }
    public class StickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public AgniLocalPlayer lp; public RectTransform baseRt, knob; public bool active; Vector2 home; bool homeSet;
        public void OnPointerDown(PointerEventData e)
        {
            active = true; if (!homeSet) { home = baseRt.anchoredPosition; homeSet = true; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)baseRt.parent, e.position, e.pressEventCamera, out var lpnt);
            baseRt.anchoredPosition = lpnt - ((RectTransform)baseRt.parent).rect.min; OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRt, e.position, e.pressEventCamera, out var p);
            var v = Vector2.ClampMagnitude(p / 130f, 1f); knob.anchoredPosition = v * 130f; if (lp != null) lp.stick = v;
            if (lp != null) lp.sprintHeld = v.magnitude > 0.98f && v.y > 0.8f;
        }
        public void OnPointerUp(PointerEventData e) { active = false; knob.anchoredPosition = Vector2.zero; if (homeSet) baseRt.anchoredPosition = home; if (lp != null) { lp.stick = Vector2.zero; lp.sprintHeld = false; } }
    }
}
