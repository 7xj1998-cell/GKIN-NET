using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Font = System.Drawing.Font;

namespace GKIN
{
    public partial class MainPanel
    {
        void Build1()
        {
            var t = Title("KHUNG TEN + BAN VE", CLuc);
            Lbl(pg1, "Khung ten", 8, 36);
            cboKhung.SetBounds(8, 54, 250, 22);
            cboKhung.SelectedIndexChanged += (_, __) =>
            {
                if (cboKhung.SelectedItem is FrameInfo f) { _khung = f.Name; _kt = f.Sample; NapTags(f.Sample); CapNhat(); }
            };
            var tayK = Mini("Tay >", 262, 54); tayK.Click += (_, __) => Tay('K');
            stKt.SetBounds(8, 78, 320, 16);
            RowScan(chkBD, txtTLBD, stBd, 100, 'B');
            RowScan(chkTD, txtTLTD, stTd, 126, 'D');
            RowScan(chkTN, txtTLTN, stTn, 152, 'N');
            Lbl(pg1, "Cat TD", 8, 204);
            cboCat.SetBounds(110, 202, 130, 22); txtKC.SetBounds(244, 202, 40, 22); cboBang.SetBounds(110, 228, 130, 22);
            Lbl(pg1, "Xep TN", 8, 256); cboXep.SetBounds(110, 254, 130, 22); cboHuong.SetBounds(244, 254, 80, 22);
            Lbl(pg1, "Xuat ra", 8, 284); cboXuat.SetBounds(110, 282, 214, 22);
            chkGop.SetBounds(8, 312, 150, 20); chkAn.SetBounds(170, 312, 150, 20); chkNhieu.SetBounds(8, 336, 320, 20);
            pg1.Controls.AddRange(new Control[] { t, cboKhung, tayK, stKt, chkBD, txtTLBD, stBd, chkTD, txtTLTD, stTd, chkTN, txtTLTN, stTn, cboCat, txtKC, cboBang, cboXep, cboHuong, cboXuat, chkGop, chkAn, chkNhieu });
        }

        void Build2()
        {
            var t1 = Title("KY HIEU + TEN TO", CTim);
            Pair(pg2, "Binh do", preBD, tenBD, 36); Pair(pg2, "Trac doc", preTD, tenTD, 62); Pair(pg2, "Trac ngang", preTN, tenTN, 88);
            var t2 = Title("GHI THE KHUNG", CTim); t2.Top = 120;
            PairC(pg2, "To so", tagSTT, kieuSTT, 148); PairC(pg2, "Ma to", tagMS, kieuMS, 174);
            PairC(pg2, "To/tong", tagBVS, kieuBVS, 200); PairC(pg2, "Ten to", tagTen, kieuTen, 226); PairC(pg2, "Ty le", tagTL, kieuTL, 252);
            Lbl(pg2, "So bat dau", 8, 284); txtSoBD.SetBounds(90, 282, 40, 22);
            Lbl(pg2, "So chu so", 140, 284); txtSoCS.SetBounds(214, 282, 40, 22);
            pg2.Controls.AddRange(new Control[] { t1, t2, preBD, tenBD, preTD, tenTD, preTN, tenTN, tagSTT, kieuSTT, tagMS, kieuMS, tagBVS, kieuBVS, tagTen, kieuTen, tagTL, kieuTL, txtSoBD, txtSoCS });
        }

        void Build3()
        {
            var t = Title("IN PDF", CCam);
            Lbl(pg3, "May in", 8, 40); cboPC3.SetBounds(110, 38, 214, 22);
            Lbl(pg3, "Net in", 8, 68); cboCTB.SetBounds(110, 66, 140, 22); chkRieng.SetBounds(254, 68, 80, 20);
            Lbl(pg3, "Net BD", 8, 96); cboCTBBD.SetBounds(110, 94, 214, 22);
            Lbl(pg3, "Net TD", 8, 122); cboCTBTD.SetBounds(110, 120, 214, 22);
            Lbl(pg3, "Net TN", 8, 148); cboCTBTN.SetBounds(110, 146, 214, 22);
            Lbl(pg3, "File PDF", 8, 176); txtPDF.SetBounds(110, 174, 180, 22);
            var br = Mini("...", 294, 174);
            br.Click += (_, __) => { using var d = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "HoSo.pdf" }; if (d.ShowDialog() == DialogResult.OK) txtPDF.Text = d.FileName; };
            chkMuc.SetBounds(8, 206, 90, 20); chkTach.SetBounds(110, 206, 100, 20);
            var il = Mini("In lai", 236, 234); il.Click += (_, __) => InPdf();
            txtInLai.SetBounds(110, 234, 120, 22);
            pg3.Controls.AddRange(new Control[] { t, cboPC3, cboCTB, chkRieng, cboCTBBD, cboCTBTD, cboCTBTN, txtPDF, br, chkMuc, chkTach, txtInLai, il });
        }

        void Build4()
        {
            var t = Title("THONG TIN", CLuc);
            var about = NewLbl("GKIN palette .NET\r\nLenh GKIN / GKINDO", CPhu);
            about.SetBounds(8, 40, 320, 120);
            pg4.Controls.AddRange(new Control[] { t, about });
        }

        void FillCombos()
        {
            Fill(cboCat, "Khoang cach deu", "Theo be rong", "Diem cat");
            Fill(cboBang, "Bang", "Khong bang");
            Fill(cboXep, "Theo luoi goc", "Theo ly trinh", "Tu chon");
            Fill(cboHuong, "Ngang 1-2-3-4", "Doc 1-2-3-4");
            Fill(cboXuat, "MODEL TD+TN", "MODEL hang", "LAYOUT");
            Fill(kieuSTT, "noi tiep ca bo", "tung loai", "khong ghi");
            Fill(kieuMS, "tung loai", "noi tiep ca bo", "khong ghi");
            Fill(kieuBVS, "tung loai", "ca bo", "khong ghi");
            Fill(kieuTen, "ten + ly trinh", "chi ten", "khong ghi");
            Fill(kieuTL, "ngang; dung", "1 ty le", "khong ghi");
        }

        public void DoLai()
        {
            try
            {
                var db = CadEngine.Db;
                if (db == null) { ResetState(null); Toast("Khong co ban ve dang mo."); return; }
                if (!ReferenceEquals(_stateDb, db)) ResetState(db);
                _frames = CadEngine.QuetKhung();
                cboKhung.Items.Clear();
                foreach (var f in _frames) cboKhung.Items.Add(f);
                int best = 0;
                for (int i = 0; i < _frames.Count; i++)
                    if (_frames[i].Name.ToUpperInvariant().Contains("KHUNG")) best = i;
                if (_frames.Count > 0)
                {
                    cboKhung.SelectedIndex = best;
                    _khung = _frames[best].Name; _kt = _frames[best].Sample; NapTags(_kt);
                }
                _hasBd = CadEngine.QuetBinhDo(out _bd, out _bdLen);
                _bdExt = _hasBd ? CadEngine.BBox(_bd) : null;
                _tdN = CadEngine.QuetTracDocKm(out _tdExt); _hasTd = _tdN > 0;
                _tnN = CadEngine.QuetTracNgang(out _tnExt); _hasTn = _tnN > 0;
                CapNhat(); Toast("Da do lai ban ve.");
            }
            catch (Exception ex) { Toast("Loi do: " + ex.Message); }
        }

        public void OnDocumentChanged(Document doc)
        {
            ResetState(doc?.Database);
            if (doc == null || IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && ReferenceEquals(CadEngine.Doc, doc)) DoLai();
            }));
        }

        void ResetState(Database db)
        {
            _stateDb = db;
            _frames.Clear();
            _khung = null;
            _kt = ObjectId.Null;
            _bd = ObjectId.Null;
            _bdExt = _tdExt = _tnExt = null;
            _layoutSheets.Clear();
            _bdLen = 0;
            _tdN = _tnN = 0;
            _hasBd = _hasTd = _hasTn = false;
            cboKhung.Items.Clear();
            CapNhat();
        }

        bool EnsureCurrentDocument()
        {
            var db = CadEngine.Db;
            if (db == null) { Toast("Khong co ban ve dang mo."); return false; }
            if (!ReferenceEquals(_stateDb, db)) DoLai();
            return ReferenceEquals(_stateDb, db);
        }

        void NapTags(ObjectId id)
        {
            var tags = CadEngine.Tags(id);
            var baseL = new List<string> { "- (khong ghi)" }; baseL.AddRange(tags);
            void one(ComboBox c, string guess)
            {
                c.Items.Clear(); foreach (var t in baseL) c.Items.Add(t);
                int ix = 0;
                for (int i = 0; i < tags.Count; i++)
                    if (string.Equals(tags[i], guess, StringComparison.OrdinalIgnoreCase)) ix = i + 1;
                if (c.Items.Count > 0) c.SelectedIndex = ix;
            }
            one(tagSTT, "STT"); one(tagMS, "MSBV"); one(tagBVS, "BVS"); one(tagTen, "TENBVE"); one(tagTL, "TYLE");
        }

        void CapNhat()
        {
            stKt.Text = string.IsNullOrEmpty(_khung) ? "chua co" : "OK " + _khung;
            stBd.Text = _hasBd ? "OK 1 tim" : "khong thay";
            stTd.Text = _hasTd ? "OK ~" + _tdN : "khong thay";
            stTn.Text = _hasTn ? "OK ~" + _tnN : "khong thay";
            pills.Text = (string.IsNullOrEmpty(_khung) ? ".. Khung" : "OK Khung") + " | " +
                (_hasBd ? "BD" : ".. BD") + " | " + (_hasTd ? "TD " + SoToTD() : ".. TD") + " | " + (_hasTn ? "TN " + SoToTN() : ".. TN");
        }

        int SoToTD()
        {
            if (!_hasBd) return 0;
            double.TryParse(txtKC.Text, out double kc); if (kc <= 0) kc = 350;
            return (int)Math.Ceiling(_bdLen / kc);
        }
        int SoToTN()
        {
            if (!_hasTn) return 0;
            int moi = cboHuong.SelectedIndex == 0 ? 4 : 6;
            return (int)Math.Ceiling(_tnN / (double)moi);
        }

        void Tay(char loai)
        {
            if (!EnsureCurrentDocument()) return;
            PaletteHost.AllowPick(() =>
            {
                var r = CadEngine.Pick(loai == 'K' ? "Chon khung ten: " : loai == 'B' ? "Chon tim: " : "Chon DT: ");
                if (r.Status != PromptStatus.OK) return;
                using (CadEngine.Doc.LockDocument())
                using (var tr = CadEngine.Db.TransactionManager.StartTransaction())
                {
                    var e = tr.GetObject(r.ObjectId, OpenMode.ForRead);
                    if (loai == 'K' && e is BlockReference br) { _khung = CadEngine.EffectiveName(br); _kt = r.ObjectId; NapTags(_kt); }
                    else if (loai == 'B' && e is Curve c) { _bd = r.ObjectId; try { _bdExt = c.GeometricExtents; _bdLen = c.GetDistanceAtParameter(c.EndParam); } catch { } _hasBd = true; }
                    else if (loai == 'D' && e is Entity td) { _hasTd = true; _tdN = Math.Max(1, _tdN); try { _tdExt = CadEngine.Expand(td.GeometricExtents, 0.08, 2.50); } catch { } }
                    else if (e is Entity tn) { _hasTn = true; _tnN = Math.Max(1, _tnN); try { _tnExt = CadEngine.Expand(tn.GeometricExtents, 0.12, 0.20); } catch { } }
                    tr.Commit();
                }
                CapNhat();
            });
        }

        void ThucHien()
        {
            if (!EnsureCurrentDocument()) return;
            if (_page == 1) DongKhung();
            else if (_page == 2) DanhSo();
            else if (_page == 3) InPdf();
            else Toast("Trang thong tin.");
        }

        void DongKhung()
        {
            if (string.IsNullOrEmpty(_khung)) { Toast("Chua co khung ten."); return; }
            int ntd = SoToTD(), ntn = SoToTN(), nbd = (_hasBd && !chkGop.Checked) ? 1 : 0;
            if (cboXuat.SelectedIndex != 2)
            {
                Toast("Che do MODEL chua an toan; hay chon LAYOUT de tao khung + viewport.");
                return;
            }
            var made = CadEngine.CreateLayouts(
                _kt,
                chkBD.Checked ? _bdExt : null,
                chkTD.Checked ? _tdExt : null,
                chkTN.Checked ? _tnExt : null,
                ntd, ntn, chkGop.Checked, cboHuong.SelectedIndex == 1,
                out string error);
            _layoutSheets = made;
            CapNhat();
            if (!string.IsNullOrEmpty(error))
                Toast($"Da tao {made.Count} Layout, dung tai loi: {error}");
            else
                Toast($"OK da tao {made.Count} Layout (BD {nbd}, TD {ntd}, TN {ntn})");
        }

        void DanhSo()
        {
            bool useLayouts = cboXuat.SelectedIndex == 2;
            if (useLayouts && _layoutSheets.Count == 0)
            {
                Toast("Chua co Layout GKIN trong phien nay; hay THUC HIEN o tab Ban ve truoc.");
                return;
            }
            var frames = useLayouts
                ? _layoutSheets.ConvertAll(x => x.FrameId)
                : CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Khong thay khung rai."); return; }
            int nbd = useLayouts ? _layoutSheets.FindAll(x => x.Type == "BD").Count : (_hasBd && !chkGop.Checked) ? 1 : 0;
            int ntd = useLayouts ? _layoutSheets.FindAll(x => x.Type == "TD").Count : SoToTD();
            int ntn = useLayouts ? _layoutSheets.FindAll(x => x.Type == "TN").Count : SoToTN();
            if (frames.Count != nbd + ntd + ntn) ntn = Math.Max(0, frames.Count - nbd - ntd);
            int.TryParse(txtSoBD.Text, out int sobd); int.TryParse(txtSoCS.Text, out int cs);
            if (sobd < 1) sobd = 1; if (cs < 1) cs = 2;
            double.TryParse(txtKC.Text, out double kc); if (kc <= 0) kc = 350;
            int cntBD = sobd, cntTD = sobd, cntTN = sobd, idxAll = sobd;
            int totalAll = sobd - 1 + frames.Count;
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["BD"] = sobd - 1 + nbd,
                ["TD"] = sobd - 1 + ntd,
                ["TN"] = sobd - 1 + ntn
            };
            var updates = new List<KeyValuePair<ObjectId, Dictionary<string, string>>>();
            for (int i = 0; i < frames.Count; i++)
            {
                string loai = useLayouts ? _layoutSheets[i].Type : i < nbd ? "BD" : i < nbd + ntd ? "TD" : "TN";
                int idxL = loai == "BD" ? cntBD : loai == "TD" ? cntTD : cntTN;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AddField(values, tagSTT, kieuSTT,
                    kieuSTT.SelectedIndex == 0 ? CadEngine.Pad(idxAll, cs) : CadEngine.Pad(idxL, cs));
                AddField(values, tagMS, kieuMS,
                    Prefix(loai) + CadEngine.Pad(kieuMS.SelectedIndex == 0 ? idxL : idxAll, cs));
                AddField(values, tagBVS, kieuBVS,
                    CadEngine.Pad(kieuBVS.SelectedIndex == 0 ? idxL : idxAll, cs) + "/" +
                    CadEngine.Pad(kieuBVS.SelectedIndex == 0 ? totals[loai] : totalAll, cs));
                AddField(values, tagTen, kieuTen,
                    kieuTen.SelectedIndex == 0 && loai == "TD"
                        ? $"{SheetName(loai)} ({CadEngine.LyTrinh((idxL - 1) * kc)} - {CadEngine.LyTrinh(idxL * kc)})"
                        : SheetName(loai));
                AddField(values, tagTL, kieuTL, ScaleText(loai, kieuTL.SelectedIndex));
                if (values.Count > 0)
                    updates.Add(new KeyValuePair<ObjectId, Dictionary<string, string>>(frames[i], values));
                if (loai == "BD") cntBD++; else if (loai == "TD") cntTD++; else cntTN++;
                idxAll++;
            }
            if (updates.Count == 0) { Toast("Khong co truong attribute nao duoc chon de ghi."); return; }
            int changed = CadEngine.GanAttrs(updates);
            Toast($"OK da ghi {changed} attribute tren {updates.Count}/{frames.Count} to");
        }

        static void AddField(Dictionary<string, string> values, ComboBox tag, ComboBox mode, string value)
        {
            if (tag.SelectedIndex <= 0 || mode.SelectedIndex == 2 || string.IsNullOrWhiteSpace(value)) return;
            values[tag.Text] = value;
        }

        string Prefix(string loai) => loai == "BD" ? preBD.Text : loai == "TD" ? preTD.Text : preTN.Text;
        string SheetName(string loai) => loai == "BD" ? tenBD.Text : loai == "TD" ? tenTD.Text : tenTN.Text;

        string ScaleText(string loai, int mode)
        {
            string scale = loai == "BD" ? txtTLBD.Text : loai == "TD" ? txtTLTD.Text : txtTLTN.Text;
            return loai == "TD" && mode == 0 ? $"1/{scale} ; 1/{scale}" : $"1/{scale}";
        }

        void InPdf()
        {
            bool useLayouts = cboXuat.SelectedIndex == 2;
            if (useLayouts && _layoutSheets.Count == 0)
            {
                Toast("Chua co Layout GKIN trong phien nay; hay THUC HIEN o tab Ban ve truoc.");
                return;
            }
            var frames = useLayouts ? _layoutSheets.ConvertAll(x => x.FrameId) : CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Khong thay to de in."); return; }
            if (string.IsNullOrWhiteSpace(txtPDF.Text)) { Toast("Chua chon PDF."); return; }
            if (cboPC3.Items.Count == 0) NapMayIn();
            string dir = System.IO.Path.GetDirectoryName(txtPDF.Text);
            string bas = System.IO.Path.GetFileNameWithoutExtension(txtPDF.Text);
            int ok = 0;
            string lastError = null;
            for (int i = 0; i < frames.Count; i++)
            {
                string type = useLayouts ? _layoutSheets[i].Type : "TO";
                string fn = System.IO.Path.Combine(dir ?? ".", $"{bas}-{CadEngine.Pad(i + 1, 2)}-{type}.pdf");
                string ctb = chkRieng.Checked
                    ? type == "BD" ? cboCTBBD.Text : type == "TD" ? cboCTBTD.Text : cboCTBTN.Text
                    : cboCTB.Text;
                bool plotted = useLayouts
                    ? CadEngine.PlotLayout(_layoutSheets[i].LayoutId, cboPC3.Text, ctb, fn)
                    : CadEngine.PlotWindow(frames[i], cboPC3.Text, ctb, fn);
                if (plotted) ok++;
                else lastError = CadEngine.LastError;
            }
            Toast(lastError == null
                ? $"Da tao PDF {ok}/{frames.Count}"
                : $"Da tao PDF {ok}/{frames.Count}; loi cuoi: {lastError}");
        }

        void NapMayIn()
        {
            foreach (var s in CadEngine.ListPc3()) cboPC3.Items.Add(s);
            foreach (var s in CadEngine.ListCtb()) { cboCTB.Items.Add(s); cboCTBBD.Items.Add(s); cboCTBTD.Items.Add(s); cboCTBTN.Items.Add(s); }
            if (cboPC3.Items.Count > 0) cboPC3.SelectedIndex = 0;
            if (cboCTB.Items.Count > 0) { cboCTB.SelectedIndex = 0; cboCTBBD.SelectedIndex = 0; cboCTBTD.SelectedIndex = 0; cboCTBTN.SelectedIndex = 0; }
        }

        void ShowPage(int n)
        {
            _page = n;
            pg1.Visible = n == 1; pg2.Visible = n == 2; pg3.Visible = n == 3; pg4.Visible = n == 4;
            Color[] tc = { CXanh, CTim, CCam, CLuc };
            for (int i = 0; i < 4; i++) tabs[i].BackColor = Mix(tc[i], i + 1 == n ? 1 : 0.30);
            if (n == 3 && cboPC3.Items.Count == 0) NapMayIn();
        }

        public void Toast(string s) { status.Text = s; CadEngine.Ed?.WriteMessage("\n[GKIN] " + s); }

        static Color Mix(Color c, double f) => Color.FromArgb((int)(11 + (c.R - 11) * f), (int)(15 + (c.G - 15) * f), (int)(23 + (c.B - 23) * f));
        static Label NewLbl(string t, Color c) => new Label { Text = t, ForeColor = c, AutoSize = false, BackColor = Color.Transparent };
        static TextBox NewTxt(string t) => new TextBox { Text = t, BackColor = CCard, ForeColor = CChu, BorderStyle = BorderStyle.FixedSingle };
        static ComboBox NewCombo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CCard, ForeColor = CChu, FlatStyle = FlatStyle.Flat };
        static CheckBox NewChk(string t, bool on) => new CheckBox { Text = t, Checked = on, ForeColor = CChu, BackColor = CNen, AutoSize = true };
        static void Fill(ComboBox c, params string[] items) { c.Items.Clear(); c.Items.AddRange(items); if (c.Items.Count > 0) c.SelectedIndex = 0; }
        static Label Title(string t, Color c) { var l = NewLbl(t, c); l.SetBounds(8, 8, 320, 22); l.Font = new Font("Segoe UI", 8, FontStyle.Bold); return l; }
        static void Lbl(Panel p, string t, int x, int y) { var l = NewLbl(t, CChu); l.SetBounds(x, y, 200, 16); p.Controls.Add(l); }
        static Button Mini(string t, int x, int y) { var b = new Button { Text = t, BackColor = CCard, ForeColor = CChu, FlatStyle = FlatStyle.Flat }; b.FlatAppearance.BorderColor = Color.FromArgb(36, 48, 68); b.SetBounds(x, y, 58, 22); return b; }
        static Button Act(string t, Color bg, int x, int y, int w) { var b = new Button { Text = t, BackColor = bg, ForeColor = CChu, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold) }; b.FlatAppearance.BorderSize = 0; b.SetBounds(x, y, w, 32); return b; }
        void RowScan(CheckBox chk, TextBox tl, Label st, int y, char loai) { chk.SetBounds(8, y, 100, 20); tl.SetBounds(110, y, 48, 22); st.SetBounds(162, y + 2, 100, 16); var b = Mini("Tay >", 268, y); b.Click += (_, __) => Tay(loai); pg1.Controls.Add(b); }
        void Pair(Panel p, string lab, TextBox a, TextBox b, int y) { Lbl(p, lab, 8, y + 2); a.SetBounds(90, y, 70, 22); b.SetBounds(164, y, 160, 22); }
        void PairC(Panel p, string lab, ComboBox a, ComboBox b, int y) { Lbl(p, lab, 8, y + 2); a.SetBounds(90, y, 110, 22); b.SetBounds(204, y, 124, 22); }
    }
}
