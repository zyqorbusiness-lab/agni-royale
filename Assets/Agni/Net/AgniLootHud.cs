// Agni Royale — LOOT v1 HUD (our own layout): nearby-loot list with tap-to-pick + AUTO toggle, two gun slots with
// ammo, armour / helmet / bag chips with durability, heal button with a timer ring, scope button + scope overlay,
// the BAG screen (worn gear, guns + attachment slots, everything in the bag with USE / DROP / FIT), loot name tags,
// pickup feed and the airdrop banner + distance marker. Icons are rendered at runtime from our own 3D loot models.
using System.Collections.Generic;
using Agni.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Agni.Net
{
    public class AgniLootHud : MonoBehaviour
    {
        AgniLocalPlayer lp; AgniPlayer P => lp.P; Canvas canvas; RectTransform safe;
        RectTransform listRoot, tagRoot, feedRoot; Text listHead, autoTxt, bagWeight, airTxt, airMark, scopeTxt, healTxt, healCount, healTimer; Image healRing, autoBg, scopeOverlay, healIcon;
        readonly List<Row> rows = new List<Row>(); readonly List<Text> tags = new List<Text>(); readonly List<(Text t, float at)> feed = new List<(Text, float)>();
        GunSlot[] gunSlots = new GunSlot[2]; Gear[] gear = new Gear[3]; GameObject bagPanel, scopeBtn, scopeRoot; RectTransform bagContent;
        Vector3 airAt; float airUntil; bool airLanded; ItemId healPick = ItemId.None;
        class Row { public RectTransform rt; public Image icon, bg; public Text name, amt; public ulong net; public byte idx; public ItemId id; }
        class GunSlot { public RectTransform rt; public Image bg, icon; public Text name, ammo, scope; }
        class Gear { public Image icon, bar, barBg; public Text lv; }

        public static AgniLootHud Create(AgniLocalPlayer lp) { var go = new GameObject("AgniLootHud"); var h = go.AddComponent<AgniLootHud>(); h.lp = lp; h.Build(); return h; }

        void Build()
        {
            canvas = CanvasMaker.Make("LootHUD", lp.UICam, 0.45f, 21, out safe); canvas.transform.SetParent(transform, false); CanvasMaker.EnsureEventSystem();
            // hide the old single-gun slot + PATTI button of the match HUD (replaced here)
            if (lp.Hud != null) foreach (var t in lp.Hud.GetComponentsInChildren<Transform>(true)) if (t.name == "SlotGun" || t.name == "Heal" || t.name == "HealBar") t.gameObject.SetActive(false);
            tagRoot = K.R("Tags", canvas.transform); tagRoot.Fill(); tagRoot.SetAsFirstSibling();
            var so = K.R("ScopeRoot", canvas.transform); so.Fill(); so.SetSiblingIndex(1);
            scopeOverlay = K.Img(so, "Lens", null, Color.black); scopeOverlay.sprite = ScopeSprite(); scopeOverlay.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 1200));
            var lb = K.Img(so, "L", null, Color.black); lb.rectTransform.anchorMin = Vector2.zero; lb.rectTransform.anchorMax = new Vector2(0.5f, 1); lb.rectTransform.offsetMin = Vector2.zero; lb.rectTransform.offsetMax = new Vector2(-598, 0);
            var rb = K.Img(so, "R", null, Color.black); rb.rectTransform.anchorMin = new Vector2(0.5f, 0); rb.rectTransform.anchorMax = Vector2.one; rb.rectTransform.offsetMin = new Vector2(598, 0); rb.rectTransform.offsetMax = Vector2.zero;
            scopeRoot = so.gameObject; scopeRoot.SetActive(false);

            // ---- nearby list (right-centre)
            listRoot = K.R("Nearby", safe).Place(new Vector2(1, 0.5f), new Vector2(-330, 170), new Vector2(560, 560), new Vector2(1, 1));
            var head = K.Img(listRoot, "Head", "rr_16", Pal.A(Pal.Navy, 0.85f)); head.rectTransform.Place(new Vector2(0, 1), Vector2.zero, new Vector2(560, 58), new Vector2(0, 1));
            listHead = K.Txt(head.transform, "T", "PAAS ME", 28, Pal.Gold, TextAnchor.MiddleLeft, true); listHead.rectTransform.Fill(20, 0, 160, 0);
            var auto = K.Btn(head.transform, "Auto", null, Pal.A(new Color(0.15f, 0.45f, 0.3f), 1f), 10, ToggleAuto, "btn_16"); ((RectTransform)auto.transform).Place(new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(150, 46), new Vector2(1, 0.5f));
            autoBg = auto.GetComponent<Image>(); var sh0 = auto.transform.Find("Shadow"); if (sh0) sh0.gameObject.SetActive(false);
            autoTxt = K.Txt(auto.transform, "L", "AUTO ✓", 24, Color.white, TextAnchor.MiddleCenter, true); autoTxt.rectTransform.Fill();
            for (int i = 0; i < 6; i++)
            {
                var r = new Row(); r.rt = K.R("Row" + i, listRoot).Place(new Vector2(0, 1), new Vector2(0, -66 - i * 82), new Vector2(560, 76), new Vector2(0, 1));
                r.bg = r.rt.gameObject.AddComponent<Image>(); r.bg.sprite = K.S("rr_16"); r.bg.type = Image.Type.Sliced; r.bg.color = new Color(0.04f, 0.07f, 0.12f, 0.78f); r.bg.raycastTarget = true;
                var b = r.rt.gameObject.AddComponent<Button>(); var rr = r; b.onClick.AddListener(() => Pick(rr));
                var ib = K.Img(r.rt, "IconBg", "rr_16", new Color(1, 1, 1, 0.08f)); ib.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(92, 64), new Vector2(0, 0.5f));
                r.icon = K.Img(r.rt, "Icon", null, Color.white); r.icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(88, 60), new Vector2(0, 0.5f)); r.icon.preserveAspect = true;
                r.name = K.Txt(r.rt, "Name", "", 27, Color.white, TextAnchor.MiddleLeft, true); r.name.rectTransform.Fill(116, 0, 120, 0);
                r.amt = K.Txt(r.rt, "Amt", "", 26, Pal.Cream, TextAnchor.MiddleRight, true); r.amt.rectTransform.Fill(0, 0, 18, 0);
                var bar = K.Img(r.rt, "Rar", null, Color.white); bar.rectTransform.Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 64), new Vector2(0, 0.5f)); r.name.gameObject.AddComponent<RarBar>().bar = bar;
                rows.Add(r); r.rt.gameObject.SetActive(false);
            }

            // ---- two gun slots (above the throwable slot of the match HUD)
            for (int i = 0; i < 2; i++)
            {
                var g = new GunSlot(); int gi = i;
                var b = K.Btn(safe, "Gun" + i, null, Pal.A(Pal.Navy, 0.8f), 10, () => TapGun(gi), "btn_24"); g.rt = (RectTransform)b.transform; g.bg = b.GetComponent<Image>();
                var sh = b.transform.Find("Shadow"); if (sh) sh.gameObject.SetActive(false);
                g.rt.Place(new Vector2(1, 0), new Vector2(-40 - (1 - i) * 290, 780), new Vector2(280, 132), new Vector2(1, 0));
                g.icon = K.Img(g.rt, "Icon", null, Color.white); g.icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(230, 70)); g.icon.preserveAspect = true;
                g.name = K.Txt(g.rt, "Name", "", 22, Pal.Cream, TextAnchor.UpperLeft, true); g.name.rectTransform.Fill(14, 0, 0, 8);
                g.ammo = K.Txt(g.rt, "Ammo", "", 34, Color.white, TextAnchor.LowerRight, true); g.ammo.rectTransform.Fill(0, 6, 14, 0); g.ammo.Outline(new Color(0, 0, 0, 0.6f), 2);
                g.scope = K.Txt(g.rt, "Scope", "", 20, Pal.Gold, TextAnchor.LowerLeft, true); g.scope.rectTransform.Fill(14, 8, 0, 0);
                gunSlots[i] = g;
            }

            // ---- worn gear chips (left of the HP bar)
            string[] gl = { "VEST", "HELMET", "JHOLA" };
            for (int i = 0; i < 3; i++)
            {
                var r = K.R("Gear" + i, safe).Place(new Vector2(0.5f, 0), new Vector2(-560 + i * 92, 30), new Vector2(84, 84), new Vector2(0.5f, 0));
                var bg = K.Img(r, "Bg", "rr_16", new Color(0, 0, 0, 0.55f)); bg.rectTransform.Fill();
                var ge = new Gear(); ge.icon = K.Img(r, "Icon", null, Color.white); ge.icon.rectTransform.Fill(6, 14, 6, 4); ge.icon.preserveAspect = true;
                ge.lv = K.Txt(r, "Lv", "", 22, Color.white, TextAnchor.UpperRight, true); ge.lv.rectTransform.Fill(0, 0, 6, 2); ge.lv.Outline(new Color(0, 0, 0, 0.7f), 2);
                ge.barBg = K.Img(r, "BarBg", null, new Color(1, 1, 1, 0.18f)); ge.barBg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 5), new Vector2(70, 7), new Vector2(0.5f, 0));
                ge.bar = K.Img(ge.barBg.transform, "Bar", null, new Color(0.5f, 0.9f, 1f)); ge.bar.rectTransform.Fill(); ge.bar.type = Image.Type.Filled; ge.bar.fillMethod = Image.FillMethod.Horizontal; ge.bar.sprite = K.S("rr_16");
                var cap = K.Txt(r, "Cap", gl[i], 15, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft, true); cap.rectTransform.Fill(6, 0, 0, 3);
                gear[i] = ge;
            }

            // ---- heal button with timer ring
            var hb = K.Btn(safe, "HealBtn", null, Pal.A(new Color(0.08f, 0.32f, 0.2f), 0.9f), 10, () => { if (P.Healing.Value) return; if (healPick == ItemId.None) P.HealRpc(); else P.UseHealRpc((byte)healPick); }, "btn_40");
            var hrt = (RectTransform)hb.transform; hrt.Place(new Vector2(1, 0), new Vector2(-700, 140), new Vector2(124, 124), new Vector2(0.5f, 0.5f)); hb.GetComponent<Image>().sprite = K.S("circle"); hb.GetComponent<Image>().type = Image.Type.Simple;
            var hs = hb.transform.Find("Shadow"); if (hs) hs.gameObject.SetActive(false);
            healIcon = K.Img(hrt, "Icon", null, Color.white); healIcon.rectTransform.Fill(18, 26, 18, 14); healIcon.preserveAspect = true;
            healRing = K.Img(hrt, "Ring", "ring", new Color(0.55f, 1f, 0.6f)); healRing.rectTransform.Fill(-8, -8, -8, -8); healRing.type = Image.Type.Filled; healRing.fillMethod = Image.FillMethod.Radial360; healRing.fillOrigin = 2;
            healCount = K.Txt(hrt, "N", "", 26, Pal.Gold, TextAnchor.LowerRight, true); healCount.rectTransform.Fill(0, 2, 6, 0); healCount.Outline(new Color(0, 0, 0, 0.7f), 2);
            healTxt = K.Txt(hrt, "Name", "", 19, Color.white, TextAnchor.LowerCenter, true); healTxt.rectTransform.Fill(0, -30, 0, 0); healTxt.Outline(new Color(0, 0, 0, 0.7f), 2);
            healTimer = K.Txt(hrt, "T", "", 40, Color.white, TextAnchor.MiddleCenter, true); healTimer.rectTransform.Fill(); healTimer.Outline(new Color(0, 0, 0, 0.8f), 3);
            var cyc = K.Btn(safe, "HealNext", null, Pal.A(Pal.Saffron, 0.95f), 10, CycleHeal, "btn_16"); ((RectTransform)cyc.transform).Place(new Vector2(1, 0), new Vector2(-628, 196), new Vector2(46, 46), new Vector2(0.5f, 0.5f)); K.Icon(cyc.transform, "ic_chevron", new Vector2(28, 28), Color.white);

            // ---- scope button + bag button + swap
            var sb = K.Btn(safe, "ScopeBtn", null, Pal.A(Pal.Navy, 0.75f), 10, () => lp.aimToggle = !lp.aimToggle, "btn_40"); scopeBtn = sb.gameObject;
            ((RectTransform)sb.transform).Place(new Vector2(1, 0), new Vector2(-110, 420), new Vector2(118, 118), new Vector2(0.5f, 0.5f)); sb.GetComponent<Image>().sprite = K.S("circle"); sb.GetComponent<Image>().type = Image.Type.Simple; var ss = sb.transform.Find("Shadow"); if (ss) ss.gameObject.SetActive(false);
            K.Img(sb.transform, "Rim", "ring", new Color(1, 1, 1, 0.5f)).rectTransform.Fill(-2, -2, -2, -2);
            scopeTxt = K.Txt(sb.transform, "L", "NISHANA", 20, Color.white, TextAnchor.MiddleCenter, true); scopeTxt.rectTransform.Fill();
            var bagB = K.Btn(safe, "BagBtn", null, Pal.A(Pal.Navy, 0.8f), 10, ToggleBag, "btn_24"); ((RectTransform)bagB.transform).Place(new Vector2(1, 1), new Vector2(-360, -26), new Vector2(110, 110), new Vector2(1, 1));
            var bs = bagB.transform.Find("Shadow"); if (bs) bs.gameObject.SetActive(false);
            K.Icon(bagB.transform, "ic_bag", new Vector2(64, 64), Color.white).rectTransform.anchoredPosition = new Vector2(0, 8);
            var bl = K.Txt(bagB.transform, "L", "BAG", 20, Pal.Gold, TextAnchor.LowerCenter, true); bl.rectTransform.Fill(0, 4, 0, 0);

            // ---- pickup feed + airdrop banner
            feedRoot = K.R("Feed", safe).Place(new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(900, 200), new Vector2(0.5f, 0));
            airTxt = K.Txt(safe, "Air", "", 40, new Color(1f, 0.82f, 0.5f), TextAnchor.MiddleCenter, true); airTxt.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 60), new Vector2(0.5f, 1)); airTxt.Outline(new Color(0.35f, 0.05f, 0.02f, 0.85f), 3);
            airMark = K.Txt(tagRoot, "AirMark", "", 26, new Color(1f, 0.55f, 0.4f), TextAnchor.MiddleCenter, true); airMark.Outline(new Color(0, 0, 0, 0.75f), 2);
            for (int i = 0; i < 10; i++) { var t = K.Txt(tagRoot, "Tag" + i, "", 24, Color.white, TextAnchor.MiddleCenter, true); t.Outline(new Color(0, 0, 0, 0.85f), 2); t.gameObject.SetActive(false); tags.Add(t); }
            BuildBag();
            AgniMatch.OnAirdrop += OnAirdrop;
        }
        void OnDestroy() { AgniMatch.OnAirdrop -= OnAirdrop; }
        void OnAirdrop(Vector3 at, bool landed) { airAt = at; airLanded = landed; airUntil = Time.time + (landed ? 60f : 90f); if (!landed) { Toast("RAAHAT PETI aa rahi hai!", new Color(1f, 0.6f, 0.4f)); AgniSfx.Play("sfx_reward_coin", 0.6f); } }

        // ------------------------------------------------------------ actions
        void ToggleAuto() { P.AutoPickRpc(!P.AutoPick.Value); }
        void TapGun(int i) { var v = P.Inv.Value; if (v.Gun(i) == ItemId.None) return; if (P.Slot.Value != 0) P.SlotRpc(0); if (v.act != i) P.SwapGunRpc(); }
        void CycleHeal() { var order = new[] { ItemId.None, ItemId.Patti, ItemId.DawaKit, ItemId.JoshDrink }; int k = System.Array.IndexOf(order, healPick); var v = P.Inv.Value; for (int n = 1; n <= 4; n++) { var c = order[(k + n) % 4]; if (c == ItemId.None || v.Heals(c) > 0) { healPick = c; break; } } }
        void Pick(Row r) { P.PickRpc(r.net, r.idx); }
        public void PickFirst() { if (rows.Count > 0 && rows[0].rt.gameObject.activeSelf) Pick(rows[0]); }
        public void ToggleBag() { bagPanel.SetActive(!bagPanel.activeSelf); if (lp.Hud != null) lp.Hud.gameObject.SetActive(!bagPanel.activeSelf); foreach (Transform ch in safe) ch.gameObject.SetActive(!bagPanel.activeSelf); tagRoot.gameObject.SetActive(!bagPanel.activeSelf); if (bagPanel.activeSelf) RefreshBag(); }
        public bool BagOpen => bagPanel != null && bagPanel.activeSelf;
        public void OnPicked(ItemId k, int n) { Toast("+ " + AgniItems.Name(k) + (n > 1 && !AgniLoot.WholeOnly(k) ? "  ×" + n : ""), AgniItems.ColorOf(k)); if (BagOpen) RefreshBag(); }
        public void OnPickFail(ItemId k)
        {
            var c = AgniItems.Cat(k); string why = c == ItemCat.Vest || c == ItemCat.Helmet || c == ItemCat.Bag ? "Tumhara wala behtar hai" : c == ItemCat.Scope || c == ItemCat.Muzzle || c == ItemCat.Mag || c == ItemCat.Stock ? "Bag me jagah nahi" : "Bag bhar gaya";
            Toast(why + " — " + AgniItems.Name(k), new Color(1f, 0.6f, 0.5f));
        }
        void Toast(string s, Color c)
        {
            var t = K.Txt(feedRoot, "F", s, 30, c, TextAnchor.MiddleCenter, true); t.Outline(new Color(0, 0, 0, 0.75f), 2); t.rectTransform.Place(new Vector2(0.5f, 0), Vector2.zero, new Vector2(900, 40), new Vector2(0.5f, 0));
            feed.Insert(0, (t, Time.time)); while (feed.Count > 4) { Destroy(feed[feed.Count - 1].t.gameObject); feed.RemoveAt(feed.Count - 1); }
        }

        // ------------------------------------------------------------ per frame
        void Update()
        {
            if (lp == null || P == null) return;
            var v = P.Inv.Value; float now = Time.time;
            // nearby list
            var near = Nearby(); int n = Mathf.Min(near.Count, rows.Count);
            listRoot.gameObject.SetActive(near.Count > 0 && P.Alive && !BagOpen);
            listHead.text = "PAAS ME  " + near.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; bool on = i < n; r.rt.gameObject.SetActive(on); if (!on) continue; var e = near[i];
                r.net = e.net; r.idx = e.idx; r.id = e.s.Id; r.icon.sprite = Icon(e.s.Id); r.icon.enabled = r.icon.sprite != null;
                r.name.text = AgniItems.Name(e.s.Id) + (e.crate ? "  <size=19><color=#ffd27a>peti</color></size>" : ""); r.name.color = AgniItems.ColorOf(e.s.Id);
                r.amt.text = AmountText(e.s); r.name.GetComponent<RarBar>().bar.color = AgniItems.ColorOf(e.s.Id);
            }
            autoTxt.text = P.AutoPick.Value ? "AUTO ✓" : "AUTO ✗"; autoBg.color = P.AutoPick.Value ? new Color(0.15f, 0.5f, 0.32f) : new Color(0.35f, 0.18f, 0.16f);
            // guns
            for (int i = 0; i < 2; i++)
            {
                var g = gunSlots[i]; var id = v.Gun(i); bool has = id != ItemId.None; bool active = has && v.act == i && P.Slot.Value == 0;
                g.icon.sprite = has ? Icon(id) : null; g.icon.enabled = has; g.name.text = has ? AgniItems.Name(id) : (i == 0 ? "KHAALI HAATH" : "KHAALI");
                int mag = has ? (v.act == i ? P.Ammo.Value : v.Mag(i)) : 0; int res = has ? v.AmmoOf(AgniItems.Gun(id).ammo) : 0;
                g.ammo.text = has ? ((v.act == i && P.Reloading.Value) ? "<size=24>RELOAD…</size>" : mag + "<size=22> / " + res + "</size>") : "";
                var sc = has ? v.Att(i, ItemCat.Scope) : ItemId.None; g.scope.text = sc != ItemId.None ? AgniItems.ScopeZoom(sc) + "x" : has && AgniItems.Gun(id).builtInZoom > 0 ? AgniItems.Gun(id).builtInZoom + "x" : "";
                g.bg.color = active ? Pal.A(Pal.SaffronDeep, 0.9f) : Pal.A(Pal.Navy, 0.78f);
            }
            // gear chips
            SetGear(0, v.vest, v.vest > 0 ? v.vestDur / (float)AgniItems.VestDur(v.vest) : 0, (ItemId)(29 + Mathf.Max(1, (int)v.vest)));
            SetGear(1, v.helmet, v.helmet > 0 ? v.helmetDur / (float)AgniItems.HelmetDur(v.helmet) : 0, (ItemId)(32 + Mathf.Max(1, (int)v.helmet)));
            SetGear(2, v.bag, Mathf.Clamp01(P.BagWeight(v) / P.BagCap(v)), (ItemId)(35 + Mathf.Max(1, (int)v.bag)));
            // heal button
            var show = healPick != ItemId.None && v.Heals(healPick) > 0 ? healPick : v.dawa > 0 && P.Hp.Value < 120 ? ItemId.DawaKit : v.patti > 0 ? ItemId.Patti : v.dawa > 0 ? ItemId.DawaKit : v.josh > 0 ? ItemId.JoshDrink : ItemId.None;
            if (healPick != ItemId.None && v.Heals(healPick) == 0) healPick = ItemId.None;
            healIcon.sprite = show != ItemId.None ? Icon(show) : null; healIcon.enabled = healIcon.sprite != null;
            healCount.text = show != ItemId.None ? v.Heals(show).ToString() : ""; healTxt.text = show != ItemId.None ? AgniItems.Name(show) + (healPick == ItemId.None ? "" : " •") : "";
            healIcon.transform.parent.gameObject.SetActive(P.Alive && (v.patti + v.dawa + v.josh > 0 || P.Healing.Value));
            if (P.Healing.Value && AgniNet.NM != null) { float left = (float)(P.HealEnd.Value - AgniNet.NM.ServerTime.Time); healRing.fillAmount = 1 - Mathf.Clamp01(left / Mathf.Max(0.1f, P.HealLen.Value)); healTimer.text = Mathf.Max(0, left).ToString("0.0"); healIcon.color = new Color(1, 1, 1, 0.35f); }
            else { healRing.fillAmount = P.Boost.Value > 0 ? P.Boost.Value / 100f : 0; healTimer.text = ""; healIcon.color = Color.white; }
            // scope button + overlay
            float z = lp.Zoom; scopeBtn.SetActive(P.Alive && P.HasGunInHand);
            var sct = v.Att(v.act, ItemCat.Scope); int zz = AgniItems.ScopeZoom(sct); if (zz == 0 && P.HasGunInHand) zz = AgniItems.Gun(P.Held).builtInZoom;
            scopeTxt.text = lp.Aiming ? "BAND" : zz > 0 ? "SCOPE\n<size=30>" + zz + "x</size>" : "NISHANA";
            scopeRoot.SetActive(lp.Aiming && z >= 2f);
            // feed fade
            for (int i = feed.Count - 1; i >= 0; i--) { var f = feed[i]; float age = now - f.at; f.t.rectTransform.anchoredPosition = new Vector2(0, i * 44); var c = f.t.color; c.a = Mathf.Clamp01(3f - age); f.t.color = c; if (age > 3f) { Destroy(f.t.gameObject); feed.RemoveAt(i); } }
            // airdrop banner + marker
            if (now < airUntil)
            {
                float d = Vector3.Distance(lp.ViewPos, airAt); airTxt.text = airLanded ? "" : "RAAHAT PETI gir rahi hai  •  " + Mathf.RoundToInt(d) + " m";
                Mark(airMark, airAt + Vector3.up * 2.5f, "▼ RAAHAT PETI " + Mathf.RoundToInt(d) + "m", 3000f, true);
            }
            else { airTxt.text = ""; airMark.gameObject.SetActive(false); }
            if (BagOpen && Time.frameCount % 15 == 0) RefreshBag();
            Tags();
        }
        void SetGear(int i, int lv, float frac, ItemId icon)
        {
            var g = gear[i]; g.icon.sprite = Icon(icon); g.icon.color = lv > 0 ? Color.white : new Color(1, 1, 1, 0.22f);
            g.lv.text = lv > 0 ? "Lv" + lv : ""; g.bar.fillAmount = lv > 0 || i == 2 ? frac : 0;
            g.bar.color = i == 2 ? (frac > 0.9f ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.85f, 0.45f)) : frac < 0.3f ? new Color(1f, 0.45f, 0.35f) : new Color(0.55f, 0.88f, 1f);
        }
        static string AmountText(LootStack s)
        {
            var c = AgniItems.Cat(s.Id);
            if (c == ItemCat.Gun) return s.amount + " goli"; if (c == ItemCat.Vest || c == ItemCat.Helmet) return s.amount + "%".Replace("%", "") + " ♦";
            if (AgniLoot.WholeOnly(s.Id)) return ""; return "×" + s.amount;
        }

        // ------------------------------------------------------------ what is near me (ground stacks + crate contents)
        struct Near { public ulong net; public byte idx; public LootStack s; public float d; public bool crate; }
        readonly List<Near> near = new List<Near>();
        List<Near> Nearby()
        {
            near.Clear(); if (!P.Alive) return near; var me = lp.ViewPos;
            foreach (var pk in AgniPickup.Live)
            {
                if (pk == null || !pk.IsSpawned) continue; var d = pk.transform.position - me; if (Mathf.Abs(d.y) > 1.8f) continue; d.y = 0; float dd = d.magnitude; if (dd > AgniPlayer.PickRange) continue;
                near.Add(new Near { net = pk.NetworkObjectId, idx = 0, s = new LootStack(pk.Id, pk.Amount.Value), d = dd });
            }
            foreach (var c in AgniCrate.Live)
            {
                if (c == null || !c.IsSpawned || !c.Landed.Value) continue; var d = c.transform.position - me; if (Mathf.Abs(d.y) > 2f) continue; d.y = 0; float dd = d.magnitude; if (dd > AgniPlayer.PickRange + 0.6f) continue;
                for (int i = 0; i < c.Items.Count; i++) near.Add(new Near { net = c.NetworkObjectId, idx = (byte)i, s = c.Items[i], d = dd - 0.01f * (16 - i), crate = true });
            }
            near.Sort((a, b) => Score(b).CompareTo(Score(a)));
            return near;
        }
        float Score(Near n) { var c = AgniItems.Cat(n.s.Id); return (int)AgniItems.Def(n.s.Id).rarity * 10 + (c == ItemCat.Gun ? 5 : c == ItemCat.Vest || c == ItemCat.Helmet ? 4 : 0) - n.d; }

        // ------------------------------------------------------------ name tags over loot in view
        void Tags()
        {
            var cam = lp.Cam; int used = 0;
            if (cam != null && P.Alive)
            {
                var me = lp.ViewPos; var list = new List<(float d, Vector3 p, string s, Color c)>();
                foreach (var pk in AgniPickup.Live) { if (pk == null) continue; float d = Vector3.Distance(pk.transform.position, me); if (d < 9f) list.Add((d, pk.transform.position + Vector3.up * 0.45f, AgniItems.Name(pk.Id) + (pk.Amount.Value > 1 && !AgniLoot.WholeOnly(pk.Id) ? " ×" + pk.Amount.Value : ""), AgniItems.ColorOf(pk.Id))); }
                foreach (var c in AgniCrate.Live) { if (c == null) continue; float d = Vector3.Distance(c.transform.position, me); if (d < 40f && c.Landed.Value) list.Add((d * 0.2f, c.transform.position + Vector3.up * (c.Kind.Value == 1 ? 1.8f : 0.9f), c.Label.Value.ToString() + "  (" + c.Items.Count + ")", c.Kind.Value == 1 ? new Color(1f, 0.6f, 0.45f) : new Color(0.75f, 0.9f, 1f))); }
                list.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var e in list) { if (used >= tags.Count) break; if (Mark(tags[used], e.p, e.s, 40f, false)) { tags[used].color = e.c; used++; } }
            }
            for (int i = used; i < tags.Count; i++) tags[i].gameObject.SetActive(false);
        }
        bool Mark(Text t, Vector3 world, string s, float maxD, bool clampEdge)
        {
            var cam = lp.Cam; if (cam == null) { t.gameObject.SetActive(false); return false; }
            var sp = cam.WorldToViewportPoint(world); bool behind = sp.z < 0;
            if (!clampEdge && (behind || sp.x < 0 || sp.x > 1 || sp.y < 0 || sp.y > 1)) { t.gameObject.SetActive(false); return false; }
            // occluded by a wall? (tags only for what you can actually see)
            if (!clampEdge) { var o = cam.transform.position; var dir = world - o; if (Physics.Raycast(o, dir.normalized, out var h, dir.magnitude - 0.35f, ~0, QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<AgniPlayer>() == null && h.collider.GetComponentInParent<AgniCrate>() == null) { t.gameObject.SetActive(false); return false; } }
            if (behind) { sp.x = 1 - sp.x; sp.y = 0.02f; }
            if (clampEdge) { sp.x = Mathf.Clamp(sp.x, 0.06f, 0.94f); sp.y = Mathf.Clamp(sp.y, 0.08f, 0.9f); }
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(sp.x, sp.y); t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchoredPosition = Vector2.zero; t.rectTransform.sizeDelta = new Vector2(600, 40);
            t.text = s; t.gameObject.SetActive(true); return true;
        }

        // ------------------------------------------------------------ BAG screen
        void BuildBag()
        {
            var r = K.R("Bag", canvas.transform); r.Fill(); bagPanel = r.gameObject;
            var dim = r.gameObject.AddComponent<Image>(); dim.color = new Color(0.03f, 0.05f, 0.09f, 0.95f); dim.raycastTarget = true;
            var title = K.Txt(r, "Title", "JHOLA", 64, Pal.Gold, TextAnchor.UpperLeft, true); title.rectTransform.Place(new Vector2(0, 1), new Vector2(120, -40), new Vector2(600, 90), new Vector2(0, 1)); title.Shadow(new Color(0, 0, 0, 0.6f), 0, -4);
            bagWeight = K.Txt(r, "Weight", "", 32, Pal.Cream, TextAnchor.UpperLeft, true); bagWeight.rectTransform.Place(new Vector2(0, 1), new Vector2(420, -64), new Vector2(900, 60), new Vector2(0, 1));
            var close = K.Btn(r, "Close", null, Pal.SaffronDeep, 10, ToggleBag, "btn_40"); ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(-60, -40), new Vector2(110, 110), new Vector2(1, 1)); K.Icon(close.transform, "ic_close", new Vector2(52, 52), Color.white);
            bagContent = K.R("Content", r); bagContent.Fill(100, 60, 100, 170);
            bagPanel.SetActive(false);
        }
        void RefreshBag()
        {
            for (int i = bagContent.childCount - 1; i >= 0; i--) Destroy(bagContent.GetChild(i).gameObject);
            var v = P.Inv.Value; float w = P.BagWeight(v), cap = P.BagCap(v);
            bagWeight.text = "Wazan  " + Mathf.CeilToInt(w) + " / " + Mathf.RoundToInt(cap) + (v.bag > 0 ? "   (Jhola Lv" + v.bag + ")" : "   (koi jhola nahi)");
            // column 1: worn gear
            var c1 = Column(bagContent, 0f, 0.24f, "PEHNA HUA");
            WornCard(c1, 0, "Kavach", v.vest, v.vest > 0 ? (ItemId)(29 + v.vest) : ItemId.Kavach1, v.vest > 0 ? v.vestDur + " / " + AgniItems.VestDur(v.vest) : "—", v.vest > 0 ? v.vestDur / (float)AgniItems.VestDur(v.vest) : 0, ItemId.Kavach1);
            WornCard(c1, 1, "Helmet", v.helmet, v.helmet > 0 ? (ItemId)(32 + v.helmet) : ItemId.Helmet1, v.helmet > 0 ? v.helmetDur + " / " + AgniItems.HelmetDur(v.helmet) : "—", v.helmet > 0 ? v.helmetDur / (float)AgniItems.HelmetDur(v.helmet) : 0, ItemId.Helmet1);
            WornCard(c1, 2, "Jhola", v.bag, v.bag > 0 ? (ItemId)(35 + v.bag) : ItemId.Jhola1, Mathf.CeilToInt(w) + " / " + Mathf.RoundToInt(cap), Mathf.Clamp01(w / cap), ItemId.None);
            // column 2: guns with attachment slots
            var c2 = Column(bagContent, 0.26f, 0.6f, "BANDOOK");
            for (int i = 0; i < 2; i++) GunCard(c2, i, v);
            // column 3: the bag
            var c3 = Column(bagContent, 0.62f, 1f, "JHOLE ME");
            var items = new List<(ItemId id, int n)>();
            foreach (var a in new[] { ItemId.GoliRifle, ItemId.GoliSmg, ItemId.Chharra, ItemId.GoliSniper }) if (v.AmmoOf(a) > 0) items.Add((a, v.AmmoOf(a)));
            foreach (var h in new[] { ItemId.DawaKit, ItemId.Patti, ItemId.JoshDrink }) if (v.Heals(h) > 0) items.Add((h, v.Heals(h)));
            foreach (var k in ThrowMath.All) if (P.Count(k) > 0) items.Add(((ItemId)(byte)k, P.Count(k)));
            for (int i = 0; i < 6; i++) if (v.Loose(i) > 0) items.Add(((ItemId)(50 + i), v.Loose(i)));
            if (items.Count == 0) { var t = K.Txt(c3, "Empty", "Jhola khaali hai — ghar ke andar loot dhoondo!", 28, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, true); t.rectTransform.Place(new Vector2(0, 1), new Vector2(10, -80), new Vector2(700, 50), new Vector2(0, 1)); }
            for (int i = 0; i < items.Count && i < 11; i++) BagRow(c3, i, items[i].id, items[i].n, v);
        }
        RectTransform Column(RectTransform parent, float x0, float x1, string title)
        {
            var c = K.R("Col_" + title, parent); c.anchorMin = new Vector2(x0, 0); c.anchorMax = new Vector2(x1, 1); c.offsetMin = c.offsetMax = Vector2.zero; c.pivot = new Vector2(0, 1);
            var t = K.Txt(c, "H", title, 30, new Color(1f, 0.85f, 0.55f), TextAnchor.UpperLeft, true); t.rectTransform.Place(new Vector2(0, 1), new Vector2(6, 0), new Vector2(500, 44), new Vector2(0, 1));
            return c;
        }
        void WornCard(RectTransform col, int i, string label, int lv, ItemId icon, string sub, float frac, ItemId dropId)
        {
            var r = K.R("Worn" + i, col).Place(new Vector2(0, 1), new Vector2(0, -56 - i * 196), new Vector2(500, 182), new Vector2(0, 1));
            var bg = K.Img(r, "Bg", "rr_24", lv > 0 ? new Color(0.1f, 0.16f, 0.26f, 0.95f) : new Color(1, 1, 1, 0.06f)); bg.rectTransform.Fill();
            var ic = K.Img(r, "Icon", null, lv > 0 ? Color.white : new Color(1, 1, 1, 0.25f)); ic.sprite = Icon(icon); ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(150, 150), new Vector2(0, 0.5f));
            var t = K.Txt(r, "T", label + (lv > 0 ? "  Lv" + lv : ""), 32, lv > 0 ? AgniItems.RarityColor((Rarity)(lv - 1)) : new Color(1, 1, 1, 0.5f), TextAnchor.UpperLeft, true); t.rectTransform.Fill(180, 0, 0, 22);
            var s = K.Txt(r, "S", sub, 26, Pal.Cream, TextAnchor.MiddleLeft, true); s.rectTransform.Fill(180, 0, 0, 0);
            var bb = K.Img(r, "BarBg", null, new Color(1, 1, 1, 0.15f)); bb.rectTransform.Place(new Vector2(0, 0), new Vector2(180, 30), new Vector2(290, 12), new Vector2(0, 0));
            var bar = K.Img(bb.transform, "Bar", null, frac < 0.3f ? new Color(1f, 0.45f, 0.35f) : new Color(0.55f, 0.88f, 1f)); bar.rectTransform.Fill(); bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillAmount = frac; bar.sprite = K.S("rr_16");
            if (lv > 0 && dropId != ItemId.None) SmallBtn(r, "PHENKO", new Vector2(-12, -12), new Vector2(1, 1), new Color(0.55f, 0.2f, 0.16f), () => { P.DropRpc((byte)dropId, 1); });
        }
        void GunCard(RectTransform col, int i, AgniInv v)
        {
            var id = v.Gun(i); var r = K.R("GunCard" + i, col).Place(new Vector2(0, 1), new Vector2(0, -56 - i * 300), new Vector2(760, 284), new Vector2(0, 1));
            var bg = K.Img(r, "Bg", "rr_24", id != ItemId.None ? (v.act == i ? new Color(0.32f, 0.16f, 0.08f, 0.95f) : new Color(0.1f, 0.16f, 0.26f, 0.95f)) : new Color(1, 1, 1, 0.06f)); bg.rectTransform.Fill();
            if (id == ItemId.None) { var e = K.Txt(r, "E", "Slot " + (i + 1) + " — khaali", 30, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter, true); e.rectTransform.Fill(); return; }
            var gd = AgniItems.Gun(id);
            var ic = K.Img(r, "Icon", null, Color.white); ic.sprite = Icon(id); ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 1), new Vector2(14, -50), new Vector2(380, 130), new Vector2(0, 1));
            var t = K.Txt(r, "T", gd.name + "  <size=22><color=#bcd>" + gd.cls + "</color></size>", 34, AgniItems.ColorOf(id), TextAnchor.UpperLeft, true); t.rectTransform.Fill(18, 0, 0, 10);
            int mag = v.act == i ? P.Ammo.Value : v.Mag(i);
            var a = K.Txt(r, "A", mag + " / " + v.MagSize(i) + "   <size=24>" + AgniItems.Name(gd.ammo) + ": " + v.AmmoOf(gd.ammo) + "</size>", 32, Color.white, TextAnchor.UpperRight, true); a.rectTransform.Fill(0, 0, 18, 12);
            var cats = new[] { ItemCat.Scope, ItemCat.Muzzle, ItemCat.Mag, ItemCat.Stock }; string[] nm = { "SCOPE", "NALI", "MAG", "STOCK" }; bool[] ok = { gd.scopeOk, gd.muzzleOk, gd.magOk, gd.stockOk };
            for (int k = 0; k < 4; k++)
            {
                var att = v.Att(i, cats[k]); var sl = K.R("Att" + k, r).Place(new Vector2(0, 0), new Vector2(410 + (k % 2) * 172, 16 + (k < 2 ? 112 : 0)), new Vector2(162, 100), new Vector2(0, 0));
                K.Img(sl, "Bg", "rr_16", ok[k] ? new Color(0, 0, 0, 0.35f) : new Color(0, 0, 0, 0.12f)).rectTransform.Fill();
                if (att != ItemId.None) { var ai = K.Img(sl, "I", null, Color.white); ai.sprite = Icon(att); ai.preserveAspect = true; ai.rectTransform.Fill(8, 22, 8, 6); }
                var lab = K.Txt(sl, "L", att != ItemId.None ? AgniItems.Name(att).Replace("Doorbeen ", "") : ok[k] ? nm[k] : "—", 19, att != ItemId.None ? AgniItems.ColorOf(att) : new Color(1, 1, 1, 0.4f), TextAnchor.LowerCenter, true); lab.rectTransform.Fill(0, 2, 0, 0);
            }
            SmallBtn(r, "PHENKO", new Vector2(14, 14), new Vector2(0, 0), new Color(0.55f, 0.2f, 0.16f), () => P.DropRpc((byte)id, 1));
            if (v.act != i) SmallBtn(r, "HAATH ME", new Vector2(176, 14), new Vector2(0, 0), new Color(0.15f, 0.45f, 0.3f), () => TapGun(i));
        }
        void BagRow(RectTransform col, int i, ItemId id, int n, AgniInv v)
        {
            var r = K.R("Item" + i, col).Place(new Vector2(0, 1), new Vector2(0, -56 - i * 76), new Vector2(760, 70), new Vector2(0, 1));
            K.Img(r, "Bg", "rr_16", new Color(0.08f, 0.12f, 0.2f, 0.92f)).rectTransform.Fill();
            K.Img(r, "Rar", null, AgniItems.ColorOf(id)).rectTransform.Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 58), new Vector2(0, 0.5f));
            var ic = K.Img(r, "Icon", null, Color.white); ic.sprite = Icon(id); ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(84, 58), new Vector2(0, 0.5f));
            var t = K.Txt(r, "T", AgniItems.Name(id), 28, AgniItems.ColorOf(id), TextAnchor.MiddleLeft, true); t.rectTransform.Fill(112, 0, 0, 0);
            var cnt = K.Txt(r, "N", "×" + n + "   <size=20><color=#9ab>" + Mathf.CeilToInt(n * AgniItems.Def(id).weight) + " kg</color></size>", 28, Color.white, TextAnchor.MiddleLeft, true); cnt.rectTransform.Fill(380, 0, 0, 0);
            var cat = AgniItems.Cat(id);
            if (cat == ItemCat.Heal) SmallBtn(r, "KHAO", new Vector2(-150, 0), new Vector2(1, 0.5f), new Color(0.15f, 0.45f, 0.3f), () => P.UseHealRpc((byte)id));
            if (cat >= ItemCat.Scope)
            {
                int fit = -1; for (int gi = 0; gi < 2; gi++) { var g = v.Gun(gi); if (g != ItemId.None && AgniItems.Accepts(AgniItems.Gun(g), id)) { fit = gi; if (gi == v.act) break; } }
                if (fit >= 0) { int f = fit; SmallBtn(r, "LAGAO", new Vector2(-150, 0), new Vector2(1, 0.5f), new Color(0.15f, 0.45f, 0.3f), () => P.AttachRpc((byte)id, (byte)f)); }
            }
            int dropN = cat == ItemCat.Ammo ? Mathf.Min(n, AgniItems.Def(id).stack) : 1;
            SmallBtn(r, "PHENKO", new Vector2(-10, 0), new Vector2(1, 0.5f), new Color(0.55f, 0.2f, 0.16f), () => P.DropRpc((byte)id, (ushort)dropN));
        }
        void SmallBtn(RectTransform parent, string label, Vector2 pos, Vector2 anchor, Color c, System.Action a)
        {
            var b = K.Btn(parent, label, label, c, 22, () => { a(); Invoke(nameof(RefreshBag), 0.25f); }, "btn_16"); ((RectTransform)b.transform).Place(anchor, pos, new Vector2(134, 52), anchor);
            var sh = b.transform.Find("Shadow"); if (sh) sh.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ icons rendered from our loot models
        static readonly Dictionary<ItemId, Sprite> icons = new Dictionary<ItemId, Sprite>();
        static Camera iconCam; static RenderTexture iconRt;
        public static Sprite Icon(ItemId id)
        {
            if (id == ItemId.None) return null;
            if (icons.TryGetValue(id, out var s) && s != null) return s;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            bool gun = AgniItems.IsGun(id); int W = gun ? 320 : 192, H = gun ? 128 : 192;
            var root = new GameObject("IconStage"); root.transform.position = new Vector3(5000, -2000, 5000);
            var v = AgniLootView.Build(id, root.transform);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var lg in root.GetComponentsInChildren<LODGroup>()) lg.enabled = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.name == "Rim") r.enabled = false;
            var b = new Bounds(); bool any = false; foreach (var r in root.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (iconCam == null)
            {
                var cg = new GameObject("IconCam"); iconCam = cg.AddComponent<Camera>(); iconCam.enabled = false; iconCam.orthographic = true; iconCam.clearFlags = CameraClearFlags.SolidColor; iconCam.backgroundColor = new Color(0, 0, 0, 0);
                iconCam.cullingMask = 1 << 31; iconCam.nearClipPlane = 0.01f; iconCam.farClipPlane = 20f; var ud = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ud.renderPostProcessing = false; ud.renderShadows = false;
                var lg = new GameObject("IconLight"); lg.transform.SetParent(cg.transform, false); var li = lg.AddComponent<Light>(); li.type = LightType.Directional; li.intensity = 1.2f; li.cullingMask = 1 << 31; lg.transform.localRotation = Quaternion.Euler(30, -30, 0);
                DontDestroyOnLoad(cg);
            }
            // view from above-front, a little tilted (items lie on the floor)
            var dir = gun ? Quaternion.Euler(80, 0, 0) * Vector3.forward : Quaternion.Euler(52, -30, 0) * Vector3.forward;
            iconCam.transform.position = b.center - dir * 5f; iconCam.transform.rotation = Quaternion.LookRotation(dir, gun ? Vector3.forward : Vector3.up);
            var ext = b.extents; float size = gun ? Mathf.Max(b.size.x, b.size.z) * 0.5f * H / W * 1.08f : Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z)) * 1.25f;
            if (gun) { var flat = new Vector3(b.size.x, 0, b.size.z); size = Mathf.Max(flat.x, flat.z) * 0.5f * H / (float)W * 1.1f; if (b.size.z > b.size.x) iconCam.transform.rotation = Quaternion.LookRotation(dir, Vector3.right); }
            iconCam.orthographicSize = Mathf.Max(0.05f, size); iconCam.aspect = W / (float)H;
            var rt = RenderTexture.GetTemporary(W, H, 16, RenderTextureFormat.ARGB32); rt.antiAliasing = 1; iconCam.targetTexture = rt;
            var amb = RenderSettings.ambientMode; iconCam.Render(); iconCam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            Destroy(root);
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f); s.name = "icon_" + id; icons[id] = s; return s;
        }
        static Sprite scopeSprite;
        static Sprite ScopeSprite()
        {
            if (scopeSprite != null) return scopeSprite; int n = 512; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.86f) / 0.03f); a = a * a * (3 - 2 * a); // black outside the lens
                    float ring = Mathf.Clamp01(1 - Mathf.Abs(r - 0.865f) / 0.012f) * 0.85f;
                    bool line = (Mathf.Abs(dx) < 0.0035f && Mathf.Abs(dy) > 0.06f && r < 0.86f) || (Mathf.Abs(dy) < 0.0035f && Mathf.Abs(dx) > 0.06f && r < 0.86f);
                    bool thick = (Mathf.Abs(dx) < 0.01f && Mathf.Abs(dy) > 0.55f && r < 0.86f) || (Mathf.Abs(dy) < 0.01f && Mathf.Abs(dx) > 0.55f && r < 0.86f);
                    float dot = r < 0.008f ? 1 : 0;
                    float al = Mathf.Max(a, ring, line ? 0.9f : 0, thick ? 0.95f : 0, dot);
                    px[y * n + x] = new Color(0, 0, 0, al) + (dot > 0 ? new Color(0.9f, 0.1f, 0.05f, 0) : Color.clear);
                }
            t.SetPixels(px); t.Apply(); scopeSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)); return scopeSprite;
        }
        class RarBar : MonoBehaviour { public Image bar; }
    }
}
