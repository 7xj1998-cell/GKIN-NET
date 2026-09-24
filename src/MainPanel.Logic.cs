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
using Autodesk.AutoCAD.Geometry;
using Font = System.Drawing.Font;

namespace GKIN
{
    public partial class MainPanel
    {
        void Build1()
        {
            var card = Card("Khung tên và bản vẽ cần đóng", CLuc);
            var grid = Grid();
            cboKhung.SelectedIndexChanged += (_, __) =>
            {
                if (cboKhung.SelectedItem is FrameInfo f) { _khung = f.Name; _kt = f.Sample; _def = f.Definition; NapTags(_kt.IsNull ? _def : _kt); CapNhat(); }
            };
            var tayK = Mini("Chọn");
            tayK.Click += (_, __) => Tay('K');
            Row(grid, "Khung tên", cboKhung, tayK);
            Span(grid, stKt);
            Span(grid, ScanLine(chkBD, txtTLBD, stBd, 'B'));
            Span(grid, ScanLine(chkTD, txtTLTD, stTd, 'D'));
            Span(grid, ScanLine(chkTN, txtTLTN, stTn, 'N'));

            Row(grid, "Cách cắt", cboCat);
            Row(grid, "Khoảng cách (m)", txtKC);
            cboCat.SelectedIndexChanged += (_, __) => { txtKC.Enabled = cboCat.SelectedIndex == 0; CapNhat(); };
            Row(grid, "Xếp trắc ngang", cboHuong);
            Row(grid, "Dải bình đồ mỗi tờ", txtBdDai);
            Row(grid, "Trắc ngang mỗi tờ", txtTnMoi);
            hint.SetToolTip(txtBdDai, "Layout: mỗi tờ bình đồ có bấy nhiêu dải viewport, xếp trên/dưới. Bản gốc thường là 2.");
            hint.SetToolTip(txtTnMoi, "Số mặt cắt trên một tờ. Ví dụ 4.");
            Row(grid, "Xuất ra", cboXuat);

            var flags = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Color.Transparent, Margin = Padding.Empty };
            chkGop.Margin = new Padding(0, 4, 12, 4);
            chkAn.Margin = new Padding(0, 4, 0, 4);
            flags.Controls.AddRange(new Control[] { chkGop, chkAn });
            Span(grid, flags);
            hint.SetToolTip(chkAn, "Sau khi tạo tờ Model, hình đã sao chép không in. Không xóa hình gốc.");
            hint.SetToolTip(cboCat, "Điểm cắt chưa có dữ liệu thì chương trình báo, không tự chia.");

            Span(grid, NewWrap("Tùy chọn", CPhu));
            var mau = Mini("Chọn file");
            mau.Click += (_, __) =>
            {
                using var d = new OpenFileDialog { Filter = "Bản vẽ AutoCAD (*.dwg)|*.dwg|Tất cả tệp|*.*" };
                if (d.ShowDialog() == DialogResult.OK) txtMau.Text = d.FileName;
            };
            Row(grid, "File khung mẫu", txtMau, mau);
            hint.SetToolTip(txtMau, "Để trống thì dùng khung đang có trong bản vẽ. File mẫu không bị sửa.");

            Span(grid, chkChongMi);
            Row(grid, "Giá trị (mm)", txtChongMi);
            hint.SetToolTip(chkChongMi, "Chỉ cộng khi được chọn. Số nhập là milimét, đổi theo đơn vị bản vẽ.");

            Row(grid, "Layer khung rải", txtLayer);
            Span(grid, chkBaiTo);
            Row(grid, "Số tờ mỗi hàng", txtBaiTo);
            hint.SetToolTip(chkBaiTo, "Chỉ dùng khi xuất MODEL xếp hàng. Tắt thì mỗi loại một hàng.");

            void ToggleOptions()
            {
                txtChongMi.Enabled = chkChongMi.Checked;
                txtBaiTo.Enabled = chkBaiTo.Checked && cboXuat.SelectedIndex == 1;
            }
            chkChongMi.CheckedChanged += (_, __) => ToggleOptions();
            chkBaiTo.CheckedChanged += (_, __) => ToggleOptions();
            cboXuat.SelectedIndexChanged += (_, __) => ToggleOptions();
            Place(card, grid);
            Mount(pg1, card);
            ToggleOptions();
        }

        void Build2()
        {
            var names = Card("Mã và tên từng loại tờ", CTim);
            var nameGrid = Grid();
            nameGrid.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, 88);
            nameGrid.ColumnStyles[2] = new ColumnStyle(SizeType.Percent, 100f);
            Row(nameGrid, "Bình đồ", preBD, tenBD, CXanh);
            Row(nameGrid, "Trắc dọc", preTD, tenTD, CVang);
            Row(nameGrid, "Trắc ngang", preTN, tenTN, CTim);
            Place(names, nameGrid);

            var fields = Card("Ghi vào thẻ khung tên", CHong);
            var fieldGrid = Grid();
            fieldGrid.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, 160);
            fieldGrid.ColumnStyles[2] = new ColumnStyle(SizeType.Percent, 100f);
            Row(fieldGrid, "Tờ số", tagSTT, kieuSTT);
            Row(fieldGrid, "Mã tờ", tagMS, kieuMS);
            Row(fieldGrid, "Tờ / tổng", tagBVS, kieuBVS);
            Row(fieldGrid, "Tên tờ", tagTen, kieuTen);
            Row(fieldGrid, "Tỷ lệ", tagTL, kieuTL);
            Row(fieldGrid, "Số bắt đầu", txtSoBD);
            Row(fieldGrid, "Số chữ số", txtSoCS);
            var note = NewWrap("Tên thẻ trong block khung, đúng chữ:\r\nSTT = số tờ.\r\nSBV hoặc MSBV = mã tờ, ví dụ BĐ - 01.\r\nTENBV hoặc TENBVE = tên bản vẽ.\r\nBVS = tờ/tổng, ví dụ 01/08. Không có thẻ thì chọn không ghi.\r\nTYLE = tỷ lệ, ví dụ 1/1000.\r\nChỉ ghi các tờ vừa tạo. Tờ cũ không bị đè. Muốn nối số thì sửa Số bắt đầu.", CPhu);
            Span(fieldGrid, note);
            Place(fields, fieldGrid);
            Mount(pg2, names, fields);
        }

        void Build3()
        {
            var card = Card("In PDF", CCam);
            var grid = Grid();
            Row(grid, "Máy in PDF", cboPC3);
            Row(grid, "Nét in chung", cboCTB);
            Span(grid, chkRieng);
            Row(grid, "Nét in BĐ", cboCTBBD, null, CXanh);
            Row(grid, "Nét in TĐ", cboCTBTD, null, CVang);
            Row(grid, "Nét in TN", cboCTBTN, null, CTim);
            var browse = Mini("Chọn file");
            browse.Click += (_, __) =>
            {
                using var d = new SaveFileDialog { Filter = "Tệp PDF (*.pdf)|*.pdf", FileName = "Ho-so.pdf" };
                if (d.ShowDialog() == DialogResult.OK) txtPDF.Text = d.FileName;
            };
            Row(grid, "File PDF", txtPDF, browse);
            var parts = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Color.Transparent, Margin = Padding.Empty };
            chkBia.Margin = new Padding(0, 4, 12, 4);
            chkMuc.Margin = new Padding(0, 4, 12, 4);
            chkTach.Margin = new Padding(0, 4, 0, 4);
            parts.Controls.AddRange(new Control[] { chkBia, chkMuc, chkTach });
            Span(grid, parts);
            var again = Mini("In lại");
            again.Click += (_, __) => InPdf(true);
            Row(grid, "In lại tờ", txtInLai, again);
            hint.SetToolTip(txtInLai, "Ví dụ: 2 là tờ 2. 2,5-7 là tờ 2 và từ tờ 5 đến tờ 7. Bấm In lại, không bấm THỰC HIỆN.");
            Span(grid, NewWrap("Khổ giấy lấy theo từng tờ. Khớp khổ thì in 1:1.", CPhu));
            chkRieng.CheckedChanged += (_, __) => ToggleSeparateCtb();
            Place(card, grid);
            Mount(pg3, card);
            ToggleSeparateCtb();
        }

        void Build4()
        {
            var card = Card("Cách dùng", CLuc);
            var grid = Grid();
            var title = NewWrap("GKIN — Ghép khung, in nhanh", CChu);
            title.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            Span(grid, title);
            Span(grid, NewWrap("Gõ GKIN để mở bảng này.\r\n\r\n1. Bản vẽ — Dò lại. Xuất LAYOUT để cắt tuyến thành từng tờ như bản gốc.\r\n2. THỰC HIỆN — mỗi tờ một layout, viewport cắt theo khoảng cách mét.\r\n3. Đánh số — chỉ ghi thẻ STT, SBV, TENBV, TYLE của các tờ vừa tạo.\r\n4. In PDF.\r\n\r\nAutoCAD 2021–2024. Nên thử trên bản sao của bản vẽ.", CPhu));
            var open = Mini("Mở thư mục PDF");
            open.Click += (_, __) => OpenOutputFolder();
            var close = Mini("Đóng bảng");
            close.Click += (_, __) => PaletteHost.Hide();
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Color.Transparent, Margin = new Padding(0, 8, 0, 0) };
            open.Margin = new Padding(0, 0, 8, 0);
            buttons.Controls.AddRange(new Control[] { open, close });
            Span(grid, buttons);
            Place(card, grid);
            Mount(pg4, card);
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
                _khung = null; _kt = ObjectId.Null; _def = ObjectId.Null; _bd = ObjectId.Null;
                _bdExt = _tdExt = _tnExt = null; _bdLen = 0; _bdEstimated = false;
                _frames = CadEngine.QuetKhung();
                cboKhung.Items.Clear();
                foreach (var f in _frames) cboKhung.Items.Add(f);
                if (_frames.Count > 0)
                {
                    cboKhung.SelectedIndex = 0;
                    _khung = _frames[0].Name; _kt = _frames[0].Sample; _def = _frames[0].Definition;
                    NapTags(_kt.IsNull ? _def : _kt);
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
            _stateDb = db; _frames.Clear(); _khung = null; _kt = ObjectId.Null; _bd = ObjectId.Null; _def = ObjectId.Null;
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
            void Set(ComboBox combo, params string[] guesses)
            {
                combo.Items.Clear(); foreach (var value in all) combo.Items.Add(value);
                int index = 0;
                for (int i = 0; i < tags.Count && index == 0; i++)
                    foreach (var guess in guesses)
                        if (string.Equals(tags[i], guess, StringComparison.OrdinalIgnoreCase)) { index = i + 1; break; }
                if (combo.Items.Count > 0) combo.SelectedIndex = index;
            }
            Set(tagSTT, "STT", "SOTT");
            Set(tagMS, "MSBV", "SBV", "MABV", "MASO");
            Set(tagBVS, "BVS", "SOBV");
            Set(tagTen, "TENBVE", "TENBV", "TENBANVE", "TENTO");
            Set(tagTL, "TYLE", "TILE", "TL");
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
            return (int)Math.Ceiling(_tnItems.Count / Math.Max(1, Number(txtTnMoi.Text, 4)));
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
                    if (loai == 'K' && e is BlockReference br) { _khung = CadEngine.EffectiveName(br); _kt = r.ObjectId; _def = br.BlockTableRecord; NapTags(_kt); }
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
            if (string.IsNullOrEmpty(_khung) && _def.IsNull && string.IsNullOrWhiteSpace(txtMau.Text)) { Toast("Chưa có khung tên."); return; }
            if (chkTD.Checked && _hasTd && cboCat.SelectedIndex == 2)
            {
                Toast("Chưa có điểm cắt trắc dọc; hãy chọn Khoảng cách đều hoặc Theo bề rộng.");
                return;
            }

            Point3d? origin = null;
            if (cboXuat.SelectedIndex != 2)
            {
                bool cancel = false;
                PaletteHost.AllowPick(() =>
                {
                    var picked = CadEngine.PickPoint("Chọn góc dưới-trái tờ đầu tiên", "TuDong");
                    if (picked == null || picked.Status == PromptStatus.Cancel) { cancel = true; return; }
                    if (picked.Status == PromptStatus.Keyword || picked.Status == PromptStatus.None) return;
                    if (picked.Status != PromptStatus.OK) { cancel = true; return; }
                    origin = picked.Value;
                });
                if (cancel) { Toast("Đã hủy đặt bản vẽ."); return; }
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
            else if (sampleFrame.IsNull && !_def.IsNull)
            {
                sampleFrame = CadEngine.InsertFrameInstance(_def);
                if (sampleFrame.IsNull) { Toast("Không chèn được block khung."); return; }
                importedSample = true;
            }

            try
            {
                if (cboXuat.SelectedIndex == 2)
                {
                    _layoutSheets = CadEngine.CreateLayouts(sampleFrame, chkBD.Checked ? _bdExt : null, chkTD.Checked ? _tdExt : null,
                        chkTN.Checked ? _tnItems : null, ntd, tdLength, tdStep, chkGop.Checked, cboHuong.SelectedIndex == 1,
                        chkBD.Checked ? _bd : ObjectId.Null, Math.Max(1, (int)Number(txtBdDai.Text, 2)), Math.Max(1, (int)Number(txtTnMoi.Text, 4)),
                        out string error);
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
                    cboXuat.SelectedIndex == 1, txtLayer.Text, overlap, sheetsPerRow, chkAn.Checked, origin,
                    chkBD.Checked ? _bd : ObjectId.Null, Math.Max(1, (int)Number(txtBdDai.Text, 2)), Math.Max(1, (int)Number(txtTnMoi.Text, 4)),
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
            var frames = CadEngine.LastFrames;
            var types = CadEngine.LastTypes;
            if (frames == null || types == null || frames.Count == 0 || types.Count != frames.Count)
            {
                Toast("Chỉ đánh số các tờ vừa tạo. Bấm THỰC HIỆN ở tab Bản vẽ trước.");
                return;
            }
            int nbd = types.Count(x => x == "BD");
            int ntd = types.Count(x => x == "TD");
            int ntn = types.Count(x => x == "TN");
            int sobd = Math.Max(1, (int)Number(txtSoBD.Text, 1)), cs = Math.Max(1, (int)Number(txtSoCS.Text, 2));
            double kc = Number(txtKC.Text, 350);
            int cntBD = sobd, cntTD = sobd, cntTN = sobd, idxAll = sobd;
            int totalAll = sobd - 1 + frames.Count;
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["BD"] = sobd - 1 + nbd, ["TD"] = sobd - 1 + ntd, ["TN"] = sobd - 1 + ntn };
            var updates = new List<KeyValuePair<ObjectId, Dictionary<string, string>>>();
            for (int i = 0; i < frames.Count; i++)
            {
                string loai = types[i];
                if (loai != "BD" && loai != "TD" && loai != "TN") continue;
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

        static AccentCard Card(string caption, Color accent) => new AccentCard
        {
            Caption = caption,
            AccentColor = accent,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        static Label NewLbl(string text, Color color) => new Label
        {
            Text = text,
            ForeColor = color,
            AutoSize = false,
            BackColor = Color.Transparent,
            UseCompatibleTextRendering = false
        };

        static TextBox NewTxt(string text) => new TextBox
        {
            Text = text,
            BackColor = CInput,
            ForeColor = CInputText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };

        static ComboBox NewCombo() => new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = CInput,
            ForeColor = CInputText,
            FlatStyle = FlatStyle.Flat,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 9f)
        };

        static CheckBox NewChk(string text, bool on) => new CheckBox
        {
            Text = text,
            Checked = on,
            ForeColor = CChu,
            BackColor = Color.FromArgb(41, 55, 69),
            AutoSize = true,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = false,
            Margin = new Padding(0, 4, 8, 4)
        };

        static void Fill(ComboBox combo, params string[] items)
        {
            combo.Items.Clear();
            combo.Items.AddRange(items);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        static Button Mini(string text)
        {
            var button = new Button
            {
                Text = text,
                BackColor = CCard,
                ForeColor = CChu,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                UseVisualStyleBackColor = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(72, 30),
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(8, 4, 0, 4)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(72, 91, 108);
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        static Button Act(string text, Color background)
        {
            var button = Mini(text);
            button.BackColor = background;
            button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            button.FlatAppearance.BorderSize = 0;
            button.MinimumSize = new Size(72, 32);
            return button;
        }

        static TableLayoutPanel Grid()
        {
            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return table;
        }

        static Label NewWrap(string text, Color color)
        {
            var label = NewLbl(text, color);
            label.AutoSize = true;
            label.MaximumSize = new Size(420, 0);
            label.Margin = new Padding(0, 4, 0, 4);
            return label;
        }

        void Row(TableLayoutPanel table, string label, Control editor, Control tail = null, Color? color = null)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = NewLbl(label, color ?? CChu);
            caption.AutoSize = true;
            caption.Anchor = AnchorStyles.Left;
            caption.Margin = new Padding(0, 8, 10, 4);
            editor.Margin = new Padding(0, 4, 0, 4);
            editor.MinimumSize = new Size(40, 28);
            if (tail == null)
            {
                editor.Dock = DockStyle.Fill;
                table.SetColumnSpan(editor, 2);
                table.Controls.Add(caption, 0, row);
                table.Controls.Add(editor, 1, row);
            }
            else
            {
                editor.Dock = DockStyle.Fill;
                tail.Dock = DockStyle.Fill;
                tail.Margin = new Padding(8, 4, 0, 4);
                table.Controls.Add(caption, 0, row);
                table.Controls.Add(editor, 1, row);
                table.Controls.Add(tail, 2, row);
            }
        }

        static void Span(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (control is Label label)
            {
                label.AutoSize = true;
                label.MaximumSize = new Size(420, 0);
            }
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 2, 0, 2);
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 3);
        }

        FlowLayoutPanel ScanLine(CheckBox check, TextBox scale, Label state, char kind)
        {
            check.Margin = new Padding(0, 6, 8, 4);
            scale.Width = 84;
            scale.MinimumSize = new Size(84, 28);
            scale.MaximumSize = new Size(84, 32);
            scale.Margin = new Padding(0, 4, 8, 4);
            state.AutoSize = true;
            state.MaximumSize = new Size(240, 0);
            state.Margin = new Padding(0, 8, 8, 4);
            var button = Mini("Chọn");
            button.Margin = new Padding(0, 4, 0, 4);
            button.Click += (_, __) => Tay(kind);
            var line = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            line.Controls.AddRange(new Control[] { check, scale, state, button });
            return line;
        }

        static void Place(AccentCard card, TableLayoutPanel table)
        {
            card.Controls.Add(table);
            bool busy = false;
            void Fit()
            {
                if (busy) return;
                busy = true;
                try
                {
                    int inner = Math.Max(220, card.ClientSize.Width - card.Padding.Horizontal);
                    table.MaximumSize = new Size(inner, 0);
                    if (Math.Abs(table.Width - inner) > 1) table.Width = inner;
                    foreach (Control child in table.Controls)
                    {
                        if (child is FlowLayoutPanel flow)
                            flow.MaximumSize = new Size(Math.Max(80, inner), 0);
                        if (child is Label label && label.AutoSize)
                            label.MaximumSize = new Size(Math.Max(80, inner), 0);
                    }
                    int height = table.GetPreferredSize(new Size(inner, 0)).Height + card.Padding.Vertical + 4;
                    if (Math.Abs(card.Height - height) > 1) card.Height = Math.Max(64, height);
                }
                finally { busy = false; }
            }
            card.Resize += (_, __) => Fit();
            Fit();
        }

        static void Mount(Panel page, params AccentCard[] cards)
        {
            page.Controls.Clear();
            page.AutoScroll = true;
            var stack = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = CNen,
                Padding = new Padding(6, 6, 6, 8)
            };
            for (int i = cards.Length - 1; i >= 0; i--)
            {
                cards[i].Dock = DockStyle.Top;
                stack.Controls.Add(cards[i]);
                if (i > 0)
                    stack.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = CNen });
            }
            page.Controls.Add(stack);
        }
    }
}
