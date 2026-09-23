using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
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
                _tdN = CadEngine.QuetTracDocKm(); _hasTd = _tdN > 0;
                _tnN = CadEngine.QuetTracNgang(); _hasTn = _tnN > 0;
                CapNhat(); Toast("Da do lai ban ve.");
            }
            catch (Exception ex) { Toast("Loi do: " + ex.Message); }
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
            PaletteHost.AllowPick(() =>
            {
                var r = CadEngine.Pick(loai == 'K' ? "Chon khung ten: " : loai == 'B' ? "Chon tim: " : "Chon DT: ");
                if (r.Status != PromptStatus.OK) return;
                using (CadEngine.Doc.LockDocument())
                using (var tr = CadEngine.Db.TransactionManager.StartTransaction())
                {
                    var e = tr.GetObject(r.ObjectId, OpenMode.ForRead);
                    if (loai == 'K' && e is BlockReference br) { _khung = CadEngine.EffectiveName(br); _kt = r.ObjectId; NapTags(_kt); }
                    else if (loai == 'B' && e is Curve c) { _bd = r.ObjectId; try { _bdLen = c.GetDistanceAtParameter(c.EndParam); } catch { } _hasBd = true; }
                    else if (loai == 'D') { _hasTd = true; _tdN = Math.Max(1, _tdN); }
                    else { _hasTn = true; _tnN = Math.Max(1, _tnN); }
                    tr.Commit();
                }
                CapNhat();
            });
        }

        void ThucHien()
        {
            if (_page == 1) DongKhung();
            else if (_page == 2) DanhSo();
            else if (_page == 3) InPdf();
            else Toast("Trang thong tin.");
        }

        void DongKhung()
        {
            if (string.IsNullOrEmpty(_khung)) { Toast("Chua co khung ten."); return; }
            int ntd = SoToTD(), ntn = SoToTN(), nbd = (_hasBd && !chkGop.Checked) ? 1 : 0;
            CadEngine.Ed?.WriteMessage($"\nGKIN: BD {nbd} TD {ntd} TN {ntn}");
            CapNhat(); Toast($"OK phuong an BD {nbd} TD {ntd} TN {ntn}");
        }

        void DanhSo()
        {
            var frames = CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Khong thay khung rai."); return; }
            int nbd = (_hasBd && !chkGop.Checked) ? 1 : 0, ntd = SoToTD(), ntn = SoToTN();
            if (frames.Count != nbd + ntd + ntn) ntn = Math.Max(0, frames.Count - nbd - ntd);
            int.TryParse(txtSoBD.Text, out int sobd); int.TryParse(txtSoCS.Text, out int cs);
            if (sobd < 1) sobd = 1; if (cs < 1) cs = 2;
            int cntBD = sobd, cntTD = sobd, cntTN = sobd, idxAll = sobd, nghi = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                string loai = i < nbd ? "BD" : i < nbd + ntd ? "TD" : "TN";
                int idxL = loai == "BD" ? cntBD : loai == "TD" ? cntTD : cntTN;
                string tag = tagSTT.SelectedIndex > 0 ? tagSTT.Text : null;
                if (tag != null && CadEngine.GanAttr(frames[i], tag, CadEngine.Pad(kieuSTT.SelectedIndex == 0 ? idxAll : idxL, cs))) nghi++;
                if (loai == "BD") cntBD++; else if (loai == "TD") cntTD++; else cntTN++;
                idxAll++;
            }
            Toast($"OK ghi {nghi}/{frames.Count} to");
        }

        void InPdf()
        {
            var frames = CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Khong thay khung rai."); return; }
            if (string.IsNullOrWhiteSpace(txtPDF.Text)) { Toast("Chua chon PDF."); return; }
            if (cboPC3.Items.Count == 0) NapMayIn();
            string dir = System.IO.Path.GetDirectoryName(txtPDF.Text);
            string bas = System.IO.Path.GetFileNameWithoutExtension(txtPDF.Text);
            int ok = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                string fn = System.IO.Path.Combine(dir ?? ".", $"{bas}-{CadEngine.Pad(i + 1, 2)}.pdf");
                if (CadEngine.PlotWindow(frames[i], cboPC3.Text, cboCTB.Text, fn)) ok++;
            }
            Toast($"Gui in {ok}/{frames.Count}");
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
