using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
            var card = Card("Khung tên + bản vẽ cần đóng khung — tự dò", CLuc);
            pg1.Controls.Add(card);

            Lbl(card, "Khung tên", 8, 29, 94);
            cboKhung.SetBounds(104, 25, 247, 23); cboKhung.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            cboKhung.SelectedIndexChanged += (_, __) =>
            {
                if (cboKhung.SelectedItem is FrameInfo f) { _khung = f.Name; _kt = f.Sample; NapTags(f.Sample); CapNhat(); }
            };
            var tayK = Mini("Chọn ▾"); tayK.SetBounds(356, 25, 62, 23); tayK.Anchor = AnchorStyles.Top | AnchorStyles.Right; tayK.Click += (_, __) => Tay('K');
            stKt.SetBounds(104, 48, 314, 18); stKt.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            RowScan(card, chkBD, txtTLBD, stBd, 69, 'B');
            RowScan(card, chkTD, txtTLTD, stTd, 93, 'D');
            RowScan(card, chkTN, txtTLTN, stTn, 117, 'N');

            Lbl(card, "Cắt trắc dọc", 8, 146, 94);
            cboCat.SetBounds(104, 142, 198, 23); txtKC.SetBounds(307, 142, 70, 23); Lbl(card, "m", 382, 146, 24);
            cboCat.SelectedIndexChanged += (_, __) => { txtKC.Enabled = cboCat.SelectedIndex == 0; CapNhat(); };
            Lbl(card, "Xếp trắc ngang", 8, 172, 94);
            cboHuong.SetBounds(104, 168, 314, 23); cboHuong.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "Xuất ra", 8, 198, 94);
            cboXuat.SetBounds(104, 194, 314, 23); cboXuat.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            chkGop.SetBounds(8, 221, 182, 21); chkAn.SetBounds(205, 221, 190, 21);

            var more = Mini("▸  Tùy chọn thêm — file khung mẫu · chồng mí · layer · bãi tờ");
            more.TextAlign = ContentAlignment.MiddleLeft; more.SetBounds(8, 245, 410, 22); more.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            more.Click += (_, __) => { morePanel.Visible = !morePanel.Visible; more.Text = (morePanel.Visible ? "▾" : "▸") + "  Tùy chọn thêm — file khung mẫu · chồng mí · layer · bãi tờ"; };

            morePanel.SetBounds(8, 269, 410, 89); morePanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(morePanel, "File khung mẫu", 8, 4, 95); txtMau.SetBounds(104, 1, 252, 22); txtMau.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var mau = Mini("..."); mau.SetBounds(360, 1, 42, 22); mau.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            mau.Click += (_, __) => { using var d = new OpenFileDialog { Filter = "Bản vẽ AutoCAD (*.dwg)|*.dwg|Tất cả tệp|*.*" }; if (d.ShowDialog() == DialogResult.OK) txtMau.Text = d.FileName; };
            chkChongMi.SetBounds(8, 26, 120, 20); txtChongMi.SetBounds(132, 25, 48, 22);
            Lbl(morePanel, "Layer khung rải", 193, 29, 102); txtLayer.SetBounds(294, 25, 108, 22); txtLayer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkBaiTo.SetBounds(8, 52, 146, 20); txtBaiTo.SetBounds(157, 51, 48, 22);
            void ToggleOptions()
            {
                txtChongMi.Enabled = chkChongMi.Checked;
                txtBaiTo.Enabled = chkBaiTo.Checked && cboXuat.SelectedIndex == 1;
            }
            chkChongMi.CheckedChanged += (_, __) => ToggleOptions();
            chkBaiTo.CheckedChanged += (_, __) => ToggleOptions();
            cboXuat.SelectedIndexChanged += (_, __) => ToggleOptions();
            morePanel.Controls.AddRange(new Control[] { txtMau, mau, chkChongMi, txtChongMi, txtLayer, chkBaiTo, txtBaiTo });

            card.Controls.AddRange(new Control[] { cboKhung, tayK, stKt, chkBD, txtTLBD, stBd, chkTD, txtTLTD, stTd, chkTN, txtTLTN, stTn,
                cboCat, txtKC, cboHuong, cboXuat, chkGop, chkAn, more, morePanel });
            card.Resize += (_, __) =>
            {
                int w = card.ClientSize.Width;
                cboKhung.Width = Math.Max(80, w - 176); tayK.Left = w - 70; stKt.Width = w - 112;
                stBd.Width = stTd.Width = stTn.Width = Math.Max(50, w - 254);
                foreach (var button in card.Controls.OfType<Button>().Where(x => x.Text == "Tay ▾")) button.Left = w - 66;
                cboCat.Width = Math.Max(100, w - 224); txtKC.Left = w - 111;
                cboHuong.Width = cboXuat.Width = w - 112;
                more.Width = w - 16; morePanel.Width = w - 16;
                txtMau.Width = Math.Max(60, w - 176); mau.Left = w - 66; txtLayer.Width = Math.Max(60, w - 310);
            };
            ToggleOptions();
        }

        void Build2()
        {
            var names = Card("Ký hiệu mã + tên tờ từng loại", CTim);
            names.SetBounds(0, 0, 430, 105); names.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var fields = Card("Ghi vào thẻ của khung tên — chọn THẺ + KIỂU ĐÁNH", CHong);
            fields.SetBounds(0, 111, 430, 290); fields.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            pg2.Controls.AddRange(new Control[] { fields, names });

            Pair(names, "Bình đồ", preBD, tenBD, 25, CXanh);
            Pair(names, "Trắc dọc", preTD, tenTD, 50, CVang);
            Pair(names, "Trắc ngang", preTN, tenTN, 75, CTim);
            PairC(fields, "Tờ số", tagSTT, kieuSTT, 27);
            PairC(fields, "Mã tờ", tagMS, kieuMS, 53);
            PairC(fields, "Tờ / tổng", tagBVS, kieuBVS, 79);
            PairC(fields, "Tên tờ", tagTen, kieuTen, 105);
            PairC(fields, "Tỷ lệ", tagTL, kieuTL, 131);
            Lbl(fields, "Số bắt đầu", 8, 162, 80); txtSoBD.SetBounds(91, 158, 68, 23);
            Lbl(fields, "Số chữ số", 178, 162, 78); txtSoCS.SetBounds(257, 158, 161, 23); txtSoCS.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var note = NewLbl("Theo số tờ lần trước — TĐ 8 tờ · STT 08–15 · MSBV TĐ - 01...TĐ - 08 · BVS 01/08...08/08 · TENBVE = TRẮC DỌC TUYẾN · TYLE = tỷ lệ rải từng loại", CPhu);
            note.SetBounds(8, 188, 410, 58); note.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; note.Font = new Font("Segoe UI", 7.25f); note.AutoEllipsis = true;
            fields.Controls.AddRange(new Control[] { txtSoBD, txtSoCS, note });
            void ResizeCards()
            {
                int w = pg2.ClientSize.Width;
                names.Width = fields.Width = w;
                tenBD.Width = tenTD.Width = tenTN.Width = Math.Max(80, w - 177);
                kieuSTT.Width = kieuMS.Width = kieuBVS.Width = kieuTen.Width = kieuTL.Width = Math.Max(90, w - 210);
                txtSoCS.Width = Math.Max(50, w - 265); note.Width = Math.Max(120, w - 16);
                fields.Height = Math.Max(180, pg2.ClientSize.Height - 111);
            }
            pg2.Resize += (_, __) => ResizeCards();
            ResizeCards();
        }

        void Build3()
        {
            var card = Card("In PDF — cả bộ thành một file", CCam);
            pg3.Controls.Add(card);
            Lbl(card, "Máy in PDF", 8, 29, 96); cboPC3.SetBounds(104, 25, 314, 23); cboPC3.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "Nét in", 8, 55, 96); cboCTB.SetBounds(104, 51, 205, 23); chkRieng.SetBounds(314, 52, 104, 21); chkRieng.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "Nét in BĐ", 24, 81, 80, CXanh); cboCTBBD.SetBounds(104, 77, 314, 23); cboCTBBD.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "Nét in TĐ", 24, 107, 80, CVang); cboCTBTD.SetBounds(104, 103, 314, 23); cboCTBTD.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "Nét in TN", 24, 133, 80, CTim); cboCTBTN.SetBounds(104, 129, 314, 23); cboCTBTN.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            Lbl(card, "File PDF", 8, 159, 96); txtPDF.SetBounds(104, 155, 267, 23); txtPDF.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var browse = Mini("..."); browse.SetBounds(376, 155, 42, 23); browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Click += (_, __) => { using var d = new SaveFileDialog { Filter = "Tệp PDF (*.pdf)|*.pdf", FileName = "Ho-so.pdf" }; if (d.ShowDialog() == DialogResult.OK) txtPDF.Text = d.FileName; };
            chkBia.SetBounds(8, 183, 68, 21); chkMuc.SetBounds(82, 183, 96, 21); chkTach.SetBounds(185, 183, 155, 21);
            Lbl(card, "In lại tờ", 8, 214, 96); txtInLai.SetBounds(104, 210, 267, 23); txtInLai.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var again = Mini("In lại"); again.SetBounds(376, 210, 42, 23); again.Anchor = AnchorStyles.Top | AnchorStyles.Right; again.Click += (_, __) => InPdf(true);
            var note = NewLbl("Khổ giấy: tự chọn theo cỡ từng tờ (khớp khổ → in 1:1). In lại: nhập 2,5-7.", CPhu);
            note.SetBounds(8, 239, 410, 35); note.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; note.Font = new Font("Segoe UI", 7.25f);
            chkRieng.CheckedChanged += (_, __) => ToggleSeparateCtb();
            card.Controls.AddRange(new Control[] { cboPC3, cboCTB, chkRieng, cboCTBBD, cboCTBTD, cboCTBTN, txtPDF, browse, chkBia, chkMuc, chkTach, txtInLai, again, note });
            card.Resize += (_, __) =>
            {
                int w = card.ClientSize.Width;
                cboPC3.Width = w - 112;
                cboCTB.Width = Math.Max(90, w - 219); chkRieng.Left = w - 112;
                cboCTBBD.Width = cboCTBTD.Width = cboCTBTN.Width = w - 112;
                txtPDF.Width = txtInLai.Width = Math.Max(80, w - 167); browse.Left = again.Left = w - 50;
                note.Width = w - 16;
            };
            ToggleSeparateCtb();
        }

        void Build4()
        {
            var card = Card("Thông tin", CLuc);
            pg4.Controls.Add(card);
            var title = NewLbl("GKIN — Ghép khung, in nhanh", CChu); title.Font = new Font("Segoe UI", 13f, FontStyle.Bold); title.SetBounds(14, 35, 390, 28);
            var about = NewLbl("Lệnh: GKIN\r\n\r\nB1  Bản vẽ — dò hoặc chọn khung, bình đồ, trắc dọc, trắc ngang.\r\nB2  THỰC HIỆN — tạo bộ tờ trong Model hoặc Layout.\r\nB3  Đánh số tờ — ghi mã, số, tên và tỷ lệ vào khung tên.\r\nB4  In PDF — xuất cả bộ hoặc in lại các tờ đã chọn.\r\n\r\nHỗ trợ AutoCAD 2021–2024 · Unicode tiếng Việt.", CPhu);
            about.SetBounds(14, 71, 390, 175); about.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var open = Mini("Mở thư mục xuất PDF"); open.SetBounds(14, 265, 188, 28);
            open.Click += (_, __) => OpenOutputFolder();
            var close = Mini("Đóng panel"); close.SetBounds(210, 265, 194, 28); close.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            close.Click += (_, __) => PaletteHost.Hide();
            card.Controls.AddRange(new Control[] { title, about, open, close });
            card.Resize += (_, __) => { title.Width = about.Width = card.ClientSize.Width - 28; close.Width = Math.Max(100, card.ClientSize.Width - 224); };
        }

        void FillCombos()
        {
            Fill(cboCat, "Khoảng cách đều", "Theo bề rộng", "Điểm cắt");
            Fill(cboHuong, "Ngang 1-2-3-4", "Dọc 1-2-3-4");
            Fill(cboXuat, "MODEL — trắc dọc + trắc ngang", "MODEL — xếp hàng", "LAYOUT — mỗi tờ một layout");
            Fill(kieuSTT, "nối tiếp cả bộ → 11", "từng loại → 01", "không ghi");
            Fill(kieuMS, "từng loại → BĐ - 01", "nối tiếp cả bộ", "không ghi");
            Fill(kieuBVS, "từng loại → 01/09", "cả bộ", "không ghi");
            Fill(kieuTen, "tên + lý trình từng tờ", "chỉ tên", "không ghi");
            Fill(kieuTL, "ngang; đứng (TĐ tự dò)", "một tỷ lệ", "không ghi");
            cboXuat.SelectedIndex = 2;
        }

        public void DoLai()
        {
            try
            {
                var db = CadEngine.Db;
                if (db == null) { ResetState(null); Toast("Không có bản vẽ đang mở."); return; }
                if (!CadEngine.SameDatabase(_stateDb, db)) ResetState(db);
                _khung = null; _kt = ObjectId.Null; _bd = ObjectId.Null;
                _bdExt = _tdExt = _tnExt = null; _bdLen = 0; _bdEstimated = false;
                _frames = CadEngine.QuetKhung();
                cboKhung.Items.Clear();
                foreach (var f in _frames) cboKhung.Items.Add(f);
                if (_frames.Count > 0)
                {
                    cboKhung.SelectedIndex = 0;
                    _khung = _frames[0].Name; _kt = _frames[0].Sample; NapTags(_kt);
                }
                _hasBd = CadEngine.QuetBinhDo(out _bd, out _bdLen, out _bdEstimated);
                _bdExt = _hasBd ? CadEngine.BBox(_bd) : null;
                _tdN = CadEngine.QuetTracDocKm(out _tdExt); _hasTd = _tdN > 0;
                _tnN = CadEngine.QuetTracNgang(out _tnExt, out _tnItems); _hasTn = _tnN > 0;
                _layoutSheets = CadEngine.ScanLayoutSheets();
                if (_kt.IsNull && _layoutSheets.Count > 0)
                {
                    _kt = _layoutSheets[0].FrameId;
                    _khung = CadEngine.BlockName(_kt);
                    NapTags(_kt);
                }
                CapNhat(); Toast("Đã dò lại bản vẽ.");
            }
            catch (Exception ex) { Toast("Lỗi dò: " + ex.Message); }
        }

        public void OnDocumentChanged(Document doc)
        {
            ResetState(doc?.Database);
            if (doc == null || IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => { if (!IsDisposed && ReferenceEquals(CadEngine.Doc, doc)) DoLai(); }));
        }

        void ResetState(Database db)
        {
            _stateDb = db; _frames.Clear(); _khung = null; _kt = ObjectId.Null; _bd = ObjectId.Null;
            _bdExt = _tdExt = _tnExt = null; _tnItems.Clear(); _layoutSheets.Clear(); _bdLen = 0; _tdN = _tnN = _modelTdSheets = 0;
            _bdEstimated = false; _hasBd = _hasTd = _hasTn = false; cboKhung.Items.Clear(); CapNhat();
        }

        bool EnsureCurrentDocument()
        {
            var db = CadEngine.Db;
            if (db == null) { Toast("Không có bản vẽ đang mở."); return false; }
            if (!CadEngine.SameDatabase(_stateDb, db)) DoLai();
            return CadEngine.SameDatabase(_stateDb, db);
        }

        void NapTags(ObjectId id)
        {
            var tags = CadEngine.Tags(id);
            var all = new List<string> { "— (không ghi)" }; all.AddRange(tags);
            void Set(ComboBox combo, string guess)
            {
                combo.Items.Clear(); foreach (var value in all) combo.Items.Add(value);
                int index = 0;
                for (int i = 0; i < tags.Count; i++) if (string.Equals(tags[i], guess, StringComparison.OrdinalIgnoreCase)) index = i + 1;
                if (combo.Items.Count > 0) combo.SelectedIndex = index;
            }
            Set(tagSTT, "STT"); Set(tagMS, "MSBV"); Set(tagBVS, "BVS"); Set(tagTen, "TENBVE"); Set(tagTL, "TYLE");
        }

        void CapNhat()
        {
            stKt.Text = string.IsNullOrEmpty(_khung) ? "Chưa có" : "✓ 1 khung · " + _khung;
            stBd.Text = _hasBd ? "✓ 1 tim · " + CadEngine.FmtM(_bdLen) + (_bdEstimated ? " · ước lượng" : "") : "Không thấy";
            stTd.Text = _hasTd ? $"✓ {_tdN} dải · đầu bảng ✓" : "Không thấy";
            stTn.Text = _hasTn ? $"✓ {_tnN} lưới mặt cắt" : "Không thấy";
            pillKhung.Text = string.IsNullOrEmpty(_khung) ? "Chưa có khung" : "✓ Khung";
            pillBd.Text = _hasBd ? "BĐ 1 tim" : "BĐ 0";
            int tdSheets = _layoutSheets.Count > 0 ? _layoutSheets.Count(x => x.Type == "TD") : _modelTdSheets;
            pillTd.Text = $"TĐ {tdSheets} tờ";
            pillTn.Text = _hasTn ? $"TN {_tnN} lưới" : "TN 0";
            pillDau.Text = _hasTd ? $"Đầu bảng {_tdN}/{_tdN}" : "Đầu bảng 0/0";
            pillKem.Text = "Kèm 0";
        }

        int SoToTD()
        {
            if (!_hasTd) return 0;
            if (cboCat.SelectedIndex == 1) return 1;
            if (cboCat.SelectedIndex == 2) return 0;
            double kc = Number(txtKC.Text, 350);
            if (kc <= 0) return 1;
            double length = _bdLen > 0 ? _bdLen : _tdExt == null ? 1 : Math.Abs(_tdExt.Value.MaxPoint.X - _tdExt.Value.MinPoint.X);
            return Math.Max(1, (int)Math.Ceiling(Math.Max(length, 1) / kc));
        }

        int SoToTN()
        {
            if (!_hasTn) return 0;
            return (int)Math.Ceiling(_tnItems.Count / 4.0);
        }

        void Tay(char loai)
        {
            if (!EnsureCurrentDocument()) return;
            PaletteHost.AllowPick(() =>
            {
                var r = CadEngine.Pick(loai == 'K' ? "Chọn khung tên: " : loai == 'B' ? "Chọn tim tuyến: " : "Chọn đối tượng đại diện: ");
                if (r.Status != PromptStatus.OK) return;
                using (CadEngine.Doc.LockDocument())
                using (var tr = CadEngine.Db.TransactionManager.StartTransaction())
                {
                    var e = tr.GetObject(r.ObjectId, OpenMode.ForRead);
                    if (loai == 'K' && e is BlockReference br) { _khung = CadEngine.EffectiveName(br); _kt = r.ObjectId; NapTags(_kt); }
                    else if (loai == 'B' && e is Curve c) { _bd = r.ObjectId; try { _bdExt = c.GeometricExtents; _bdLen = c.GetDistanceAtParameter(c.EndParam); } catch { } _bdEstimated = false; _hasBd = true; }
                    else if (loai == 'D' && e is Entity td) { _hasTd = true; _tdN = Math.Max(1, _tdN); try { _tdExt = CadEngine.Expand(td.GeometricExtents, 0.08, 2.50); } catch { } }
                    else if (e is Entity tn) { _hasTn = true; _tnN = 1; try { var ext = tn.GeometricExtents; _tnItems = new List<Extents3d> { ext }; _tnExt = CadEngine.Expand(ext, 0.12, 0.20); } catch { } }
                    tr.Commit();
                }
                CapNhat();
            });
        }

        void ThucHien()
        {
            if (!EnsureCurrentDocument()) return;
            if (_page == 1) DongKhung(); else if (_page == 2) DanhSo(); else if (_page == 3) InPdf(false); else Toast("Trang thông tin.");
        }

        void DongKhung()
        {
            if (string.IsNullOrEmpty(_khung) && string.IsNullOrWhiteSpace(txtMau.Text)) { Toast("Chưa có khung tên."); return; }
            if (chkTD.Checked && _hasTd && cboCat.SelectedIndex == 2)
            {
                Toast("Chưa có điểm cắt trắc dọc; hãy chọn Khoảng cách đều hoặc Theo bề rộng.");
                return;
            }

            int ntd = SoToTD();
            double tdLength = _bdLen > 0 ? _bdLen : _tdExt == null ? 0 : Math.Abs(_tdExt.Value.MaxPoint.X - _tdExt.Value.MinPoint.X);
            double tdStep = cboCat.SelectedIndex == 0 ? Number(txtKC.Text, 350) : 0;
            ObjectId sampleFrame = _kt;
            bool importedSample = false;
            if (!string.IsNullOrWhiteSpace(txtMau.Text))
            {
                sampleFrame = CadEngine.ImportTemplateFrame(txtMau.Text, out _, out string importError);
                if (sampleFrame.IsNull) { Toast("Không nạp được file khung mẫu: " + importError); return; }
                importedSample = true;
            }

            try
            {
                if (cboXuat.SelectedIndex == 2)
                {
                    _layoutSheets = CadEngine.CreateLayouts(sampleFrame, chkBD.Checked ? _bdExt : null, chkTD.Checked ? _tdExt : null,
                        chkTN.Checked ? _tnItems : null, ntd, tdLength, tdStep, chkGop.Checked, cboHuong.SelectedIndex == 1, out string error);
                    _modelTdSheets = 0;
                    if (importedSample && _layoutSheets.Count > 0)
                    {
                        _kt = _layoutSheets[0].FrameId;
                        _khung = CadEngine.BlockName(_kt);
                    }
                    CapNhat();
                    Toast(string.IsNullOrEmpty(error) ? $"Đã tạo {_layoutSheets.Count} layout." : $"Đã tạo {_layoutSheets.Count} layout; dừng tại lỗi: {error}");
                    return;
                }

                double overlap = chkChongMi.Checked
                    ? CadEngine.MillimetersToDrawingUnits(Number(txtChongMi.Text, 0))
                    : 0;
                int sheetsPerRow = chkBaiTo.Checked && cboXuat.SelectedIndex == 1
                    ? Math.Max(1, (int)Number(txtBaiTo.Text, 4))
                    : 4;
                var made = CadEngine.CreateModelSheets(sampleFrame, chkBD.Checked ? _bdExt : null, chkTD.Checked ? _tdExt : null,
                    chkTN.Checked ? _tnItems : null, ntd, tdLength, tdStep, chkGop.Checked, cboHuong.SelectedIndex == 1,
                    cboXuat.SelectedIndex == 1, txtLayer.Text, overlap, sheetsPerRow, chkAn.Checked,
                    out _modelTdSheets, out string modelError);
                _layoutSheets.Clear();
                if (importedSample && made.Count > 0)
                {
                    _kt = made[0];
                    _khung = CadEngine.BlockName(_kt);
                }
                CapNhat();
                Toast(string.IsNullOrEmpty(modelError) ? $"Đã tạo {made.Count} khung trong Model." : $"Đã tạo {made.Count} khung; dừng tại lỗi: {modelError}");
            }
            finally
            {
                if (importedSample) CadEngine.DeleteEntity(sampleFrame);
            }
        }

        void DanhSo()
        {
            bool useLayouts = cboXuat.SelectedIndex == 2;
            if (useLayouts) _layoutSheets = CadEngine.ScanLayoutSheets();
            if (useLayouts && _layoutSheets.Count == 0) { Toast("Không thấy layout GKIN-BD, GKIN-TD hoặc GKIN-TN trong bản vẽ."); return; }
            var frames = useLayouts ? _layoutSheets.ConvertAll(x => x.FrameId) : CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Không thấy khung rải."); return; }
            int nbd = useLayouts ? _layoutSheets.Count(x => x.Type == "BD") : (_hasBd && !chkGop.Checked ? 1 : 0);
            int ntd = useLayouts ? _layoutSheets.Count(x => x.Type == "TD") : (_modelTdSheets > 0 ? _modelTdSheets : SoToTD());
            int ntn = useLayouts ? _layoutSheets.Count(x => x.Type == "TN") : SoToTN();
            if (frames.Count != nbd + ntd + ntn) ntn = Math.Max(0, frames.Count - nbd - ntd);
            int sobd = Math.Max(1, (int)Number(txtSoBD.Text, 1)), cs = Math.Max(1, (int)Number(txtSoCS.Text, 2));
            double kc = Number(txtKC.Text, 350);
            int cntBD = sobd, cntTD = sobd, cntTN = sobd, idxAll = sobd;
            int totalAll = sobd - 1 + frames.Count;
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["BD"] = sobd - 1 + nbd, ["TD"] = sobd - 1 + ntd, ["TN"] = sobd - 1 + ntn };
            var updates = new List<KeyValuePair<ObjectId, Dictionary<string, string>>>();
            for (int i = 0; i < frames.Count; i++)
            {
                string loai = useLayouts ? _layoutSheets[i].Type : i < nbd ? "BD" : i < nbd + ntd ? "TD" : "TN";
                int idxL = loai == "BD" ? cntBD : loai == "TD" ? cntTD : cntTN;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AddField(values, tagSTT, kieuSTT, kieuSTT.SelectedIndex == 0 ? CadEngine.Pad(idxAll, cs) : CadEngine.Pad(idxL, cs));
                AddField(values, tagMS, kieuMS, Prefix(loai) + CadEngine.Pad(kieuMS.SelectedIndex == 0 ? idxL : idxAll, cs));
                AddField(values, tagBVS, kieuBVS, CadEngine.Pad(kieuBVS.SelectedIndex == 0 ? idxL : idxAll, cs) + "/" + CadEngine.Pad(kieuBVS.SelectedIndex == 0 ? totals[loai] : totalAll, cs));
                AddField(values, tagTen, kieuTen, kieuTen.SelectedIndex == 0 && loai == "TD" ? $"{SheetName(loai)} ({CadEngine.LyTrinh((idxL - 1) * kc)} - {CadEngine.LyTrinh(idxL * kc)})" : SheetName(loai));
                AddField(values, tagTL, kieuTL, ScaleText(loai, kieuTL.SelectedIndex));
                if (values.Count > 0) updates.Add(new KeyValuePair<ObjectId, Dictionary<string, string>>(frames[i], values));
                if (loai == "BD") cntBD++; else if (loai == "TD") cntTD++; else cntTN++;
                idxAll++;
            }
            if (updates.Count == 0) { Toast("Không có thẻ attribute nào được chọn để ghi."); return; }
            int changed = CadEngine.GanAttrs(updates);
            Toast($"Đã ghi {changed} attribute trên {updates.Count}/{frames.Count} tờ.");
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
            string scale = NormalizeScale(loai == "BD" ? txtTLBD.Text : loai == "TD" ? txtTLTD.Text : txtTLTN.Text);
            return loai == "TD" && mode == 0 ? scale + " ; " + scale : scale;
        }

        void InPdf(bool reprint)
        {
            bool useLayouts = cboXuat.SelectedIndex == 2;
            if (useLayouts) _layoutSheets = CadEngine.ScanLayoutSheets();
            if (useLayouts && _layoutSheets.Count == 0) { Toast("Không thấy layout GKIN-BD, GKIN-TD hoặc GKIN-TN trong bản vẽ."); return; }
            var frames = useLayouts ? _layoutSheets.ConvertAll(x => x.FrameId) : CadEngine.KhungRai(_khung);
            if (frames.Count == 0) { Toast("Không thấy tờ để in."); return; }
            if (string.IsNullOrWhiteSpace(txtPDF.Text)) { Toast("Chưa chọn đường dẫn PDF."); return; }
            if (cboPC3.Items.Count == 0) NapMayIn();
            var selected = reprint ? ParsePages(txtInLai.Text, frames.Count) : Enumerable.Range(0, frames.Count).ToList();
            if (selected.Count == 0) { Toast("Danh sách tờ in lại không hợp lệ. Ví dụ: 2,5-7."); return; }
            string dir = Path.GetDirectoryName(txtPDF.Text) ?? ".";
            string basename = Path.GetFileNameWithoutExtension(txtPDF.Text);
            ObjectId modelLayoutId = useLayouts ? ObjectId.Null : CadEngine.ModelLayoutId();
            var requests = new List<PlotSheetRequest>();
            foreach (int i in selected)
            {
                string type = useLayouts ? _layoutSheets[i].Type : "TO";
                string ctb = chkRieng.Checked ? type == "BD" ? cboCTBBD.Text : type == "TD" ? cboCTBTD.Text : cboCTBTN.Text : cboCTB.Text;
                Extents2d? window = null;
                if (!useLayouts)
                {
                    var ext = CadEngine.BBox(frames[i]);
                    if (ext == null) continue;
                    window = new Extents2d(ext.Value.MinPoint.X, ext.Value.MinPoint.Y, ext.Value.MaxPoint.X, ext.Value.MaxPoint.Y);
                }
                requests.Add(new PlotSheetRequest { LayoutId = useLayouts ? _layoutSheets[i].LayoutId : modelLayoutId, Window = window, Ctb = ctb, Type = type });
            }
            if (requests.Count == 0) { Toast("Không dựng được danh sách tờ in."); return; }

            var entries = selected.Select((sheetIndex, order) =>
            {
                string type = useLayouts ? _layoutSheets[sheetIndex].Type : "TỜ";
                string name = useLayouts ? _layoutSheets[sheetIndex].LayoutName : "Model";
                return $"{order + 1:00}  |  {type}  |  {name}";
            }).ToList();
            var front = CadEngine.CreateFrontMatter(_kt, chkBia.Checked, chkMuc.Checked, entries, out string frontError);
            var frontRequests = front.Select(x => new PlotSheetRequest { LayoutId = x.LayoutId, Ctb = cboCTB.Text, Type = "PHU" }).ToList();
            var batches = new List<(string suffix, List<PlotSheetRequest> sheets)>();
            if (chkTach.Checked)
            {
                foreach (var group in requests.GroupBy(x => x.Type))
                    batches.Add(("-" + group.Key, frontRequests.Concat(group).ToList()));
            }
            else
                batches.Add((reprint ? "-in-lai" : "", frontRequests.Concat(requests).ToList()));

            int files = 0; string lastError = frontError;
            try
            {
                foreach (var batch in batches)
                {
                    string file = Path.Combine(dir, basename + batch.suffix + ".pdf");
                    if (CadEngine.PlotSheets(batch.sheets, cboPC3.Text, file)) files++; else lastError = CadEngine.LastError;
                }
            }
            finally
            {
                CadEngine.DeleteLayouts(front.Select(x => x.LayoutName));
            }
            int pageCount = batches.Sum(x => x.sheets.Count);
            Toast(lastError == null ? $"Đã tạo {files} PDF, gồm {pageCount} trang." : $"Đã tạo {files}/{batches.Count} PDF; lỗi cuối: {lastError}");
        }

        void NapMayIn()
        {
            cboPC3.Items.Clear(); cboCTB.Items.Clear(); cboCTBBD.Items.Clear(); cboCTBTD.Items.Clear(); cboCTBTN.Items.Clear();
            foreach (var value in CadEngine.ListPc3()) cboPC3.Items.Add(value);
            foreach (var value in CadEngine.ListCtb()) { cboCTB.Items.Add(value); cboCTBBD.Items.Add(value); cboCTBTD.Items.Add(value); cboCTBTN.Items.Add(value); }
            SelectPreferred(cboPC3, "DWG To PDF.pc3"); SelectPreferred(cboCTB, "monochrome.ctb");
            SelectPreferred(cboCTBBD, cboCTB.Text); SelectPreferred(cboCTBTD, cboCTB.Text); SelectPreferred(cboCTBTN, cboCTB.Text);
        }

        void ShowPage(int n)
        {
            _page = n; pg1.Visible = n == 1; pg2.Visible = n == 2; pg3.Visible = n == 3; pg4.Visible = n == 4;
            for (int i = 0; i < tabs.Length; i++) { tabs[i].Selected = i + 1 == n; tabs[i].Invalidate(); }
            if (n == 3 && cboPC3.Items.Count == 0 && CadEngine.Db != null) NapMayIn();
        }

        public void Toast(string message) { status.Text = message; CadEngine.Ed?.WriteMessage("\n[GKIN] " + message); }

        void ToggleSeparateCtb()
        {
            cboCTB.Enabled = !chkRieng.Checked;
            cboCTBBD.Enabled = cboCTBTD.Enabled = cboCTBTN.Enabled = chkRieng.Checked;
        }

        void OpenOutputFolder()
        {
            try
            {
                string folder = Path.GetDirectoryName(txtPDF.Text);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { Toast("Chưa có thư mục xuất PDF."); return; }
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex) { Toast("Không mở được thư mục: " + ex.Message); }
        }

        static List<int> ParsePages(string text, int count)
        {
            var pages = new SortedSet<int>();
            foreach (string raw in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] range = raw.Split('-');
                if (!int.TryParse(range[0], out int start)) continue;
                int end = start;
                if (range.Length == 2 && !int.TryParse(range[1], out end)) continue;
                if (end < start) { int swap = start; start = end; end = swap; }
                for (int page = Math.Max(1, start); page <= Math.Min(count, end); page++) pages.Add(page - 1);
            }
            return pages.ToList();
        }

        static double Number(string text, double fallback)
        {
            string clean = (text ?? "").Trim();
            if (clean.StartsWith("1/", StringComparison.Ordinal)) clean = clean.Substring(2);
            clean = clean.Replace(',', '.');
            return double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) && value >= 0 ? value : fallback;
        }

        static string NormalizeScale(string text)
        {
            string value = (text ?? "").Trim();
            if (string.IsNullOrEmpty(value)) return "1/1";
            return value.StartsWith("1/", StringComparison.Ordinal) ? value : "1/" + value;
        }

        static void SelectPreferred(ComboBox combo, string value)
        {
            if (combo.Items.Count == 0) return;
            int index = combo.FindStringExact(value ?? ""); combo.SelectedIndex = index >= 0 ? index : 0;
        }

        static AccentCard Card(string caption, Color accent) => new AccentCard { Caption = caption, AccentColor = accent, Dock = DockStyle.Fill, Size = new Size(430, 380) };
        static Label NewLbl(string text, Color color) => new Label { Text = text, ForeColor = color, AutoSize = false, BackColor = Color.Transparent };
        static TextBox NewTxt(string text) => new TextBox { Text = text, BackColor = CInput, ForeColor = CInputText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.25f) };
        static ComboBox NewCombo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CInput, ForeColor = CInputText, FlatStyle = FlatStyle.Flat, IntegralHeight = false, Font = new Font("Segoe UI", 8.25f) };
        static CheckBox NewChk(string text, bool on) => new CheckBox { Text = text, Checked = on, ForeColor = CChu, BackColor = Color.Transparent, AutoSize = false, UseVisualStyleBackColor = false };
        static void Fill(ComboBox combo, params string[] items) { combo.Items.Clear(); combo.Items.AddRange(items); if (combo.Items.Count > 0) combo.SelectedIndex = 0; }
        static void Lbl(Control parent, string text, int x, int y, int width, Color? color = null) { var label = NewLbl(text, color ?? CChu); label.SetBounds(x, y, width, 18); parent.Controls.Add(label); }
        static Button Mini(string text) { var button = new Button { Text = text, BackColor = CCard, ForeColor = CChu, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f), UseVisualStyleBackColor = false }; button.FlatAppearance.BorderColor = Color.FromArgb(72, 91, 108); button.FlatAppearance.BorderSize = 1; return button; }
        static Button Act(string text, Color background) { var button = Mini(text); button.BackColor = background; button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold); button.FlatAppearance.BorderSize = 0; return button; }

        void RowScan(Control parent, CheckBox check, TextBox scale, Label state, int y, char type)
        {
            check.SetBounds(8, y, 96, 21); scale.SetBounds(104, y, 70, 22); state.SetBounds(180, y + 2, 174, 18); state.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var button = Mini("Tay ▾"); button.SetBounds(360, y, 58, 22); button.Anchor = AnchorStyles.Top | AnchorStyles.Right; button.Click += (_, __) => Tay(type);
            parent.Controls.Add(button);
        }

        static void Pair(Control parent, string label, TextBox prefix, TextBox name, int y, Color color)
        {
            Lbl(parent, label, 8, y + 3, 82, color); prefix.SetBounds(92, y, 72, 22); name.SetBounds(169, y, 249, 22); name.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            parent.Controls.AddRange(new Control[] { prefix, name });
        }

        static void PairC(Control parent, string label, ComboBox tag, ComboBox mode, int y)
        {
            Lbl(parent, label, 8, y + 3, 82); tag.SetBounds(92, y, 105, 23); mode.SetBounds(202, y, 216, 23); mode.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            parent.Controls.AddRange(new Control[] { tag, mode });
        }
    }
}
