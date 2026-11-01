// Agni Royale — DOST / SQUAD panel (Mahal lobby). Friends list with live presence, requests, add-by-ID,
// room code (copy / share / deep link agniroyale://join?code=XXXXXX) and join-by-code. All data comes from
// AgniSocial.Friends (IFriendsService, mock until Sujon's backend exists) and AgniSocial.Party (IPartyService, LAN now).
using System.Collections.Generic;
using Agni.Social;
using UnityEngine;
using UnityEngine.UI;

namespace Agni.UI
{
    public class AgniFriendsPanel : MonoBehaviour
    {
        public enum Tab { Dost = 0, Requests = 1, AddId = 2, Room = 3 }
        public static AgniFriendsPanel Open(Transform canvas, Tab tab = Tab.Dost)
        {
            var card = K.Modal(canvas, "FriendsPanel", new Vector2(1940, 900), out var root);
            var p = root.AddComponent<AgniFriendsPanel>(); p.card = card; p.Build(); p.Show(tab); AgniSfx.Play("sfx_button_tap"); return p;
        }

        RectTransform card, body; Tab tab; readonly List<(Tab t, Image bg, Text label, Image badge, Text badgeT)> tabs = new List<(Tab, Image, Text, Image, Text)>();
        Text toast, note, idv; float toastUntil, refreshAt; InputField idInput, codeInput; Text addStatus, joinStatus; string lastSig = "", lastErr;
        static IFriendsService F => AgniSocial.Friends; static IPartyService Party => AgniSocial.Party;
        static readonly Color Ink = Pal.Hex("#3A1E10"), Sub = Pal.Hex("#8A6A50"), RowBg = Pal.Hex("#FFFFFF"), Green = Pal.Hex("#2FBF71"), Amber = Pal.Hex("#F5A524"), Grey = Pal.Hex("#A89A8C");

        void Build()
        {
            var title = K.Txt(card, "Title", "DOST & SQUAD", 62, Pal.SaffronDeep, TextAnchor.MiddleLeft, true); title.rectTransform.Place(new Vector2(0, 1), new Vector2(56, -18), new Vector2(700, 110), new Vector2(0, 1));
            // my ID chip + copy
            var chip = K.Img(card, "MyId", "rr_24", Pal.Hex("#FFF6E6")).rectTransform; chip.Place(new Vector2(1, 1), new Vector2(-150, -30), new Vector2(640, 90), new Vector2(1, 1));
            K.Img(chip, "Ol", "ol_24", Pal.A(Pal.Saffron, 0.8f)).rectTransform.Fill();
            var idl = K.Txt(chip, "L", "TUMHARA ID", 22, Sub, TextAnchor.UpperLeft, true); idl.rectTransform.Fill(24, 0, 0, 10);
            idv = K.Txt(chip, "V", AgniSocial.Pretty(AgniSocial.MyPlayerId), 40, Ink, TextAnchor.LowerLeft, true); idv.rectTransform.Fill(24, 8, 160, 0); idv.resizeTextForBestFit = true; idv.resizeTextMinSize = 22; idv.resizeTextMaxSize = 40;
            var cp = K.Btn(chip, "CopyId", "COPY", Pal.Saffron, 30, () => Copy(AgniSocial.MyPlayerId, "ID copy ho gaya"), "btn_24");
            ((RectTransform)cp.transform).Place(new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(150, 66), new Vector2(1, 0.5f));
            K.CloseX(card, Close);

            // tab rail
            string[] names = { "DOST", "REQUESTS", "ADD ID", "ROOM CODE" };
            for (int i = 0; i < 4; i++)
            {
                var t = (Tab)i;
                var b = K.Btn(card, "Tab_" + names[i], null, Color.white, 10, () => Show(t), "btn_24");
                var rt = (RectTransform)b.transform; rt.Place(new Vector2(0, 1), new Vector2(40, -190 - i * 124), new Vector2(330, 108), new Vector2(0, 1));
                var lab = K.Txt(rt, "Label", names[i], 34, Ink, TextAnchor.MiddleLeft, true); lab.rectTransform.Fill(34, 0, 0, 0);
                var badge = K.Img(rt, "Badge", "circle", Pal.Red); badge.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(50, 50), new Vector2(1, 0.5f));
                var bt = K.Txt(badge.transform, "N", "", 28, Color.white, TextAnchor.MiddleCenter, true); bt.rectTransform.Fill();
                tabs.Add((t, b.GetComponent<Image>(), lab, badge, bt));
            }
            note = K.Txt(card, "DemoNote", "", 22, Sub, TextAnchor.LowerLeft, false);
            note.rectTransform.Place(new Vector2(0, 0), new Vector2(44, 26), new Vector2(340, 90), new Vector2(0, 0)); note.horizontalOverflow = HorizontalWrapMode.Wrap;

            body = K.R("Body", card); body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one; body.offsetMin = new Vector2(410, 34); body.offsetMax = new Vector2(-40, -176);

            var tb = K.Img(card, "Toast", "rr_24", Pal.A(Pal.Ink, 0.92f)); tb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(200, -70), new Vector2(900, 84), new Vector2(0.5f, 0));
            toast = K.Txt(tb.transform, "T", "", 32, Color.white, TextAnchor.MiddleCenter, true); toast.rectTransform.Fill(20, 0, 20, 0); tb.gameObject.SetActive(false);

            Bind(); AgniSocial.ServicesChanged += Rebind;
        }
        IFriendsService boundF; IPartyService boundP;
        void Bind() { boundF = F; boundP = Party; boundF.Changed += Dirty; boundP.Changed += Dirty; }
        void Unbind() { if (boundF != null) boundF.Changed -= Dirty; if (boundP != null) boundP.Changed -= Dirty; }
        void Rebind() { Unbind(); Bind(); Show(tab); }
        void OnDestroy() { Unbind(); AgniSocial.ServicesChanged -= Rebind; }
        void Dirty() { refreshAt = 0; }
        public void Close() { AgniSfx.Play("sfx_button_tap"); Destroy(gameObject); }

        public void Show(Tab t) { tab = t; lastSig = ""; refreshAt = 0; foreach (Transform c in body) Destroy(c.gameObject); idInput = codeInput = null; BuildTab(); }

        void Update()
        {
            if (toast != null && toast.transform.parent.gameObject.activeSelf && Time.unscaledTime > toastUntil) toast.transform.parent.gameObject.SetActive(false);
            if (Time.unscaledTime < refreshAt) return; refreshAt = Time.unscaledTime + 1f;
            bool isOn = AgniSocial.IsOnline;
            note.text = isOn ? "ONLINE · asli dost (Unity Gaming Services)" : "Demo dost list — " + AgniOnline.StatusText;
            note.color = isOn ? Green : Sub;
            idv.text = string.IsNullOrEmpty(AgniSocial.MyPlayerId) ? "…" : AgniSocial.Pretty(AgniSocial.MyPlayerId);
            if (F.LastError != lastErr) { lastErr = F.LastError; if (!string.IsNullOrEmpty(lastErr)) Toast(lastErr); }
            // badges
            int online = 0, inc = 0; foreach (var f in F.Friends) if (f.presence != Presence.Offline) online++; foreach (var r in F.Requests) if (r.incoming) inc++;
            foreach (var x in tabs)
            {
                bool on = x.t == tab; x.bg.color = on ? Pal.Saffron : Color.white; x.label.color = on ? Color.white : Ink;
                int n = x.t == Tab.Dost ? online : x.t == Tab.Requests ? inc : 0;
                x.badge.gameObject.SetActive(n > 0); x.badge.color = x.t == Tab.Dost ? Green : Pal.Red; x.badgeT.text = n.ToString();
            }
            // rebuild lists only when their data changed (keeps input fields stable)
            string sig = Signature(); if (sig == lastSig) return; lastSig = sig;
            if (tab == Tab.Dost || tab == Tab.Requests || tab == Tab.Room) { foreach (Transform c in body) Destroy(c.gameObject); BuildTab(); }
        }
        string Signature()
        {
            var s = tab.ToString() + "|";
            if (tab == Tab.Dost) foreach (var f in F.Friends) s += f.playerId + (int)f.presence + AgniFriendsPanelTime.Bucket(f.lastSeenUnix) + ",";
            if (tab == Tab.Dost || tab == Tab.Room) { s += Party.InParty + Party.Code + Party.Status; foreach (var m in Party.Members) s += m.name + m.ready; }
            if (tab == Tab.Requests) foreach (var r in F.Requests) s += r.playerId + r.incoming + ",";
            if (tab == Tab.Room) s += AgniLanDiscovery.Found.Count;
            return s;
        }

        void BuildTab()
        {
            switch (tab)
            {
                case Tab.Dost: BuildDost(); break;
                case Tab.Requests: BuildRequests(); break;
                case Tab.AddId: BuildAdd(); break;
                case Tab.Room: BuildRoom(); break;
            }
        }

        // ------------------------------------------------------------- DOST
        void BuildDost()
        {
            var head = K.Txt(body, "Head", SquadLine(), 30, Sub, TextAnchor.UpperLeft, true); head.rectTransform.Place(new Vector2(0, 1), new Vector2(6, 0), new Vector2(1400, 44), new Vector2(0, 1));
            var list = Scroll(body, 56);
            var fr = new List<FriendInfo>(F.Friends);
            fr.Sort((a, b) => Order(a.presence).CompareTo(Order(b.presence)));
            if (fr.Count == 0) Empty(list, "Abhi koi dost nahi — ADD ID se jodo");
            foreach (var f in fr)
            {
                var row = Row(list, f.name, "Lv " + f.level + "  ·  " + f.rank, f.presence, AgniPresenceText(f));
                bool inSquad = false; foreach (var m in Party.Members) if (m.name == f.name) inSquad = true;
                if (inSquad || f.presence == Presence.InSquad && Party.InParty) Pill(row, "SQUAD ME", Green);
                else if (f.presence == Presence.Offline) Pill(row, "OFFLINE", Grey);
                else if (f.presence == Presence.InMatch) Pill(row, "MATCH ME", Amber);
                else
                {
                    var id = f.playerId; var nm = f.name;
                    var b = K.Btn(row, "Invite", "INVITE", Pal.Saffron, 32, () => { var err = F.InviteToSquad(id); Toast(err ?? ("Invite bheja: " + nm)); if (err == null && !Party.InParty) Party.Create(); }, "btn_24");
                    ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(220, 80), new Vector2(1, 0.5f));
                }
            }
        }
        static int Order(Presence p) => p == Presence.InSquad ? 0 : p == Presence.InLobby ? 1 : p == Presence.Online ? 2 : p == Presence.InMatch ? 3 : 4;
        static string AgniPresenceText(FriendInfo f) => MockFriendsService.PresenceText(f);
        string SquadLine()
        {
            if (!Party.InParty) return "SQUAD: sirf tum  ·  INVITE dabao, squad khud ban jayegi";
            return "SQUAD " + Party.Members.Count + "/4  ·  ROOM " + Party.Code;
        }

        // ------------------------------------------------------------- REQUESTS
        void BuildRequests()
        {
            var list = Scroll(body, 0); int n = 0;
            foreach (var r in F.Requests)
            {
                n++; var id = r.playerId; var nm = r.name;
                var row = Row(list, r.name, "Lv " + r.level + "  ·  ID " + AgniSocial.Pretty(r.playerId), r.incoming ? Presence.Online : Presence.Offline, r.incoming ? "Tumhe dost banana chahta hai" : "Request bheji — jawab ka intezaar");
                if (r.incoming)
                {
                    var a = K.Btn(row, "Accept", "ACCEPT", Pal.Saffron, 30, () => { F.Accept(id); Toast(nm + " ab tumhara dost hai"); }, "btn_24");
                    ((RectTransform)a.transform).Place(new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(210, 80), new Vector2(1, 0.5f));
                    var d = K.Btn(row, "Decline", "NAHI", Pal.Hex("#C9B8A4"), 30, () => { F.Decline(id); Toast("Request hata di"); }, "btn_24");
                    ((RectTransform)d.transform).Place(new Vector2(1, 0.5f), new Vector2(-250, 0), new Vector2(160, 80), new Vector2(1, 0.5f));
                }
                else Pill(row, "BHEJI", Grey);
            }
            if (n == 0) Empty(list, "Koi request nahi");
        }

        // ------------------------------------------------------------- ADD ID
        void BuildAdd()
        {
            bool online = AgniSocial.IsOnline;
            var h = K.Txt(body, "H", online ? "Dost ka ID daalo (jaise Rahul#1234)" : "Dost ka 10-digit Player ID daalo", 40, Ink, TextAnchor.UpperLeft, true); h.rectTransform.Place(new Vector2(0, 1), new Vector2(10, -20), new Vector2(1300, 60), new Vector2(0, 1));
            idInput = online ? Field(body, "IdField", "Rahul#1234", InputField.ContentType.Standard, 60) : Field(body, "IdField", "123 456 7890", InputField.ContentType.IntegerNumber, 10); ((RectTransform)idInput.transform).Place(new Vector2(0, 1), new Vector2(10, -110), new Vector2(900, 120), new Vector2(0, 1));
            var send = K.Btn(body, "Send", "REQUEST BHEJO", Pal.Saffron, 38, SendReq, "btn_24"); ((RectTransform)send.transform).Place(new Vector2(0, 1), new Vector2(940, -110), new Vector2(480, 120), new Vector2(0, 1));
            addStatus = K.Txt(body, "Status", "", 32, Pal.SaffronDeep, TextAnchor.UpperLeft, true); addStatus.rectTransform.Place(new Vector2(0, 1), new Vector2(14, -250), new Vector2(1400, 50), new Vector2(0, 1));
            var tip = K.Txt(body, "Tip", "Apna ID upar COPY karke dost ko bhejo — woh tumhe yahan se add kar sakta hai.\n" + (online ? "Request jaate hi dost ke phone par REQUESTS me dikhegi." : "Demo IDs (online aane tak): 912 440 8852 · 135 792 4680 · 246 801 3579"), 28, Sub, TextAnchor.UpperLeft, false);
            tip.rectTransform.Place(new Vector2(0, 1), new Vector2(14, -330), new Vector2(1400, 120), new Vector2(0, 1));
        }
        /// test/proof hook: type an ID into ADD ID and press send
        public void DemoAdd(string id) { if (tab != Tab.AddId) Show(Tab.AddId); idInput.text = id; SendReq(); }
        void SendReq() { var err = F.SendRequest(idInput != null ? idInput.text : ""); addStatus.color = err == null ? Green : Pal.SaffronDeep; addStatus.text = err ?? "Request bhej di! Jawab REQUESTS me dikhega"; if (err == null) idInput.text = ""; }

        // ------------------------------------------------------------- ROOM CODE
        void BuildRoom()
        {
            // left: my room
            var l = K.R("Mine", body); l.anchorMin = new Vector2(0, 0); l.anchorMax = new Vector2(0.56f, 1); l.offsetMin = l.offsetMax = Vector2.zero;
            K.Img(l, "Bg", "rr_24", RowBg).rectTransform.Fill(0, 0, 14, 0);
            var lt = K.Txt(l, "H", "APNI SQUAD ROOM", 34, Sub, TextAnchor.UpperLeft, true); lt.rectTransform.Fill(30, 0, 0, 24);
            if (Party.InParty && !string.IsNullOrEmpty(Party.Code))
            {
                var code = Party.Code;
                for (int i = 0; i < code.Length; i++)
                {
                    var tile = K.Img(l, "C" + i, "rr_16", Pal.Hex("#FFE2B8")); tile.rectTransform.Place(new Vector2(0, 1), new Vector2(30 + i * 112, -82), new Vector2(98, 124), new Vector2(0, 1));
                    K.Img(tile.transform, "Ol", "ol_24", Pal.A(Pal.Saffron, 0.9f)).rectTransform.Fill();
                    var ct = K.Txt(tile.transform, "T", code[i].ToString(), 78, Pal.SaffronDeep, TextAnchor.MiddleCenter, true); ct.rectTransform.Fill();
                }
                var c1 = K.Btn(l, "CopyCode", "COPY CODE", Pal.Saffron, 32, () => Copy(code, "Code copy: " + code), "btn_24"); ((RectTransform)c1.transform).Place(new Vector2(0, 1), new Vector2(30, -232), new Vector2(300, 92), new Vector2(0, 1));
                var c2 = K.Btn(l, "Share", "SHARE LINK", Pal.Hex("#25A36A"), 32, () => Share(code), "btn_24"); ((RectTransform)c2.transform).Place(new Vector2(0, 1), new Vector2(350, -232), new Vector2(300, 92), new Vector2(0, 1));
                var lk = K.Txt(l, "Link", AgniSocial.DeepLink(code), 26, Sub, TextAnchor.UpperLeft, false); lk.rectTransform.Place(new Vector2(0, 1), new Vector2(32, -344), new Vector2(900, 40), new Vector2(0, 1));
                // members
                int i2 = 0;
                foreach (var m in Party.Members)
                {
                    var mr = K.R("M" + i2, l).Place(new Vector2(0, 1), new Vector2(24 + i2 * 200, -396), new Vector2(190, 150), new Vector2(0, 1));
                    var av = K.Img(mr, "Av", "circle", m.leader ? Pal.Gold : Pal.Hex("#FFD08A")); av.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(84, 84), new Vector2(0.5f, 1));
                    var ini = K.Txt(av.transform, "I", Initial(m.name), 44, Ink, TextAnchor.MiddleCenter, true); ini.rectTransform.Fill();
                    if (m.ready) { var tk = K.Img(av.transform, "Tick", "circle", Green); tk.rectTransform.Place(new Vector2(1, 0), new Vector2(6, -6), new Vector2(34, 34), new Vector2(1, 0)); K.Txt(tk.transform, "v", "✓", 26, Color.white, TextAnchor.MiddleCenter, true).rectTransform.Fill(); }
                    var nm = K.Txt(mr, "N", (m.leader ? "★ " : "") + m.name, 26, Ink, TextAnchor.LowerCenter, true); nm.rectTransform.Fill(0, 0, 0, 92);
                    i2++;
                }
                for (; i2 < 4; i2++) { var mr = K.R("E" + i2, l).Place(new Vector2(0, 1), new Vector2(24 + i2 * 200, -396), new Vector2(190, 150), new Vector2(0, 1)); var av = K.Img(mr, "Av", "ring", Pal.A(Sub, 0.5f)); av.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(84, 84), new Vector2(0.5f, 1)); K.Txt(mr, "N", "khaali", 24, Grey, TextAnchor.LowerCenter, false).rectTransform.Fill(0, 0, 0, 92); }
                var leave = K.Btn(l, "Leave", "SQUAD CHHODO", Pal.Hex("#C9B8A4"), 28, () => { Party.Leave(); Toast("Squad chhod di"); }, "btn_24"); ((RectTransform)leave.transform).Place(new Vector2(1, 1), new Vector2(-44, -24), new Vector2(260, 70), new Vector2(1, 1));
            }
            else
            {
                var info = K.Txt(l, "Info", "Room banao, code dosto ko bhejo.\nWoh code daal ke seedha tumhari squad me aa jayenge.", 30, Ink, TextAnchor.UpperLeft, false); info.rectTransform.Place(new Vector2(0, 1), new Vector2(30, -90), new Vector2(940, 120), new Vector2(0, 1));
                var mk = K.Btn(l, "Create", "ROOM BANAO", Pal.Saffron, 40, () => { Party.Create(); Toast(Party.Status); }, "btn_24"); ((RectTransform)mk.transform).Place(new Vector2(0, 1), new Vector2(30, -240), new Vector2(420, 120), new Vector2(0, 1));
            }
            var st = K.Txt(l, "Status", Party.Status, 26, Pal.SaffronDeep, TextAnchor.LowerLeft, true); st.rectTransform.Fill(32, 22, 20, 0);

            // right: join by code
            var r = K.R("Join", body); r.anchorMin = new Vector2(0.56f, 0); r.anchorMax = new Vector2(1, 1); r.offsetMin = r.offsetMax = Vector2.zero;
            K.Img(r, "Bg", "rr_24", RowBg).rectTransform.Fill(14, 0, 0, 0);
            var rt = K.Txt(r, "H", "CODE SE JUDO", 34, Sub, TextAnchor.UpperLeft, true); rt.rectTransform.Fill(44, 0, 0, 24);
            codeInput = Field(r, "CodeField", "ABC123", InputField.ContentType.Alphanumeric, 21); ((RectTransform)codeInput.transform).Place(new Vector2(0, 1), new Vector2(44, -90), new Vector2(620, 120), new Vector2(0, 1));
            codeInput.characterValidation = InputField.CharacterValidation.None; codeInput.onValidateInput += (t, i, ch) => char.ToUpperInvariant(ch);
            var jb = K.Btn(r, "JoinBtn", "JUDO", Pal.Saffron, 40, () => { Party.JoinByCode(codeInput.text); Toast(Party.Status); }, "btn_24"); ((RectTransform)jb.transform).Place(new Vector2(0, 1), new Vector2(44, -232), new Vector2(620, 110), new Vector2(0, 1));
            int k = 0;
            foreach (var kv in AgniLanDiscovery.Found)
            {
                if (k >= 2) break; var code = kv.Key; var h = kv.Value;
                var b = K.Btn(r, "Lan" + k, null, Pal.Hex("#FFE2B8"), 10, () => { Party.JoinByCode(code); Toast(Party.Status); }, "btn_24");
                ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(44, -372 - k * 92), new Vector2(620, 80), new Vector2(0, 1));
                K.Txt(b.transform, "T", "PAAS ME: " + h.host + "  ·  " + code + "  (" + h.members + "/4)", 26, Ink, TextAnchor.MiddleLeft, true).rectTransform.Fill(24, 0, 0, 0); k++;
            }
            if (k == 0) { var hint = K.Txt(r, "Hint", "Same Wi-Fi par dost ka room yahan khud dikhega.", 26, Sub, TextAnchor.UpperLeft, false); hint.rectTransform.Place(new Vector2(0, 1), new Vector2(46, -372), new Vector2(640, 80), new Vector2(0, 1)); }
        }

        // ------------------------------------------------------------- bits
        RectTransform Scroll(RectTransform parent, float top)
        {
            var vp = K.R("Viewport", parent); vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = Vector2.zero; vp.offsetMax = new Vector2(0, -top);
            vp.gameObject.AddComponent<RectMask2D>(); var hit = vp.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
            var content = K.R("Content", vp); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 14; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.padding = new RectOffset(0, 8, 0, 10);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = vp.gameObject.AddComponent<ScrollRect>(); sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30;
            return content;
        }
        RectTransform Row(RectTransform list, string name, string sub, Presence pr, string presence)
        {
            var row = K.R("Row_" + name, list); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 128;
            K.Img(row, "Bg", "rr_24", RowBg).rectTransform.Fill();
            var av = K.Img(row, "Av", "circle", Pal.Hex("#FFD08A")); av.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(94, 94), new Vector2(0, 0.5f));
            K.Txt(av.transform, "I", Initial(name), 48, Ink, TextAnchor.MiddleCenter, true).rectTransform.Fill();
            var dot = K.Img(av.transform, "Dot", "circle", PresColor(pr)); dot.rectTransform.Place(new Vector2(1, 0), new Vector2(4, -2), new Vector2(30, 30), new Vector2(1, 0));
            K.Img(dot.transform, "R", "ring", Color.white).rectTransform.Fill(-3, -3, -3, -3);
            var n = K.Txt(row, "Name", name, 40, Ink, TextAnchor.UpperLeft, true); n.rectTransform.Fill(140, 0, 300, 14);
            var s = K.Txt(row, "Sub", sub, 24, Sub, TextAnchor.LowerLeft, false); s.rectTransform.Fill(142, 16, 300, 0);
            var p = K.Txt(row, "Presence", presence, 28, PresColor(pr) == Grey ? Grey : Pal.Hex("#1E8C55"), TextAnchor.MiddleLeft, true); p.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(560, 0), new Vector2(560, 60), new Vector2(0, 0.5f));
            if (pr == Presence.InMatch) p.color = Pal.Hex("#C77C0A");
            return row;
        }
        static Color PresColor(Presence p) => p == Presence.Offline ? Grey : p == Presence.InMatch ? Amber : Green;
        void Pill(RectTransform row, string label, Color c)
        {
            var im = K.Img(row, "Pill", "rr_24", Pal.A(c, 0.18f)); im.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(220, 70), new Vector2(1, 0.5f));
            K.Txt(im.transform, "T", label, 28, c == Grey ? Pal.Hex("#7A6C60") : c, TextAnchor.MiddleCenter, true).rectTransform.Fill();
        }
        void Empty(RectTransform list, string msg)
        {
            var e = K.R("Empty", list); e.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
            K.Txt(e, "T", msg, 34, Sub, TextAnchor.MiddleCenter, true).rectTransform.Fill();
        }
        static string Initial(string n) => string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1).ToUpperInvariant();
        InputField Field(Transform parent, string name, string placeholder, InputField.ContentType type, int max)
        {
            var rt = K.R(name, parent); var bg = rt.gameObject.AddComponent<Image>(); bg.sprite = K.S("rr_24"); bg.type = Image.Type.Sliced; bg.color = Pal.Hex("#FFF6E6");
            K.Img(rt, "Ol", "ol_24", Pal.A(Pal.Saffron, 0.85f)).rectTransform.Fill();
            var ph = K.Txt(rt, "Placeholder", placeholder, 52, Pal.A(Sub, 0.55f), TextAnchor.MiddleLeft, true); ph.rectTransform.Fill(36, 0, 20, 0);
            var tx = K.Txt(rt, "Text", "", 52, Ink, TextAnchor.MiddleLeft, true); tx.rectTransform.Fill(36, 0, 20, 0); tx.supportRichText = false;
            var f = rt.gameObject.AddComponent<InputField>(); f.textComponent = tx; f.placeholder = ph; f.targetGraphic = bg; f.contentType = type; f.characterLimit = max; f.caretColor = Pal.SaffronDeep; f.caretWidth = 4; f.selectionColor = Pal.A(Pal.Saffron, 0.35f);
            return f;
        }
        public void Toast(string msg) { if (string.IsNullOrEmpty(msg)) return; toast.text = msg; toast.transform.parent.gameObject.SetActive(true); toastUntil = Time.unscaledTime + 2.4f; }
        void Copy(string s, string msg) { GUIUtility.systemCopyBuffer = s; Toast(msg); }
        void Share(string code)
        {
            var text = AgniSocial.ShareText(code); GUIUtility.systemCopyBuffer = text; Toast("Invite link copy ho gaya");
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var ic = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    intent.Call<AndroidJavaObject>("setAction", ic.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", ic.GetStatic<string>("EXTRA_TEXT"), text);
                    var chooser = ic.CallStatic<AndroidJavaObject>("createChooser", intent, "Squad invite bhejo");
                    up.GetStatic<AndroidJavaObject>("currentActivity").Call("startActivity", chooser);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[AgniFriends] share failed: " + e.Message); }
#endif
        }
    }

    static class AgniFriendsPanelTime { public static int Bucket(long unix) => (int)((System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix) / 60); }
}
