using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Font = System.Drawing.Font;

namespace GKIN
{
    public partial class MainPanel : UserControl
    {
        internal static readonly Color CNen = Color.FromArgb(29, 40, 52);
        internal static readonly Color CCard = Color.FromArgb(38, 52, 66);
        internal static readonly Color CChu = Color.FromArgb(231, 237, 243);
        internal static readonly Color CPhu = Color.FromArgb(157, 170, 182);
        internal static readonly Color CInput = Color.FromArgb(247, 248, 250);
        internal static readonly Color CInputText = Color.FromArgb(39, 47, 55);
        internal static readonly Color CXanh = Color.FromArgb(38, 166, 232);
        internal static readonly Color CTim = Color.FromArgb(162, 100, 235);
        internal static readonly Color CCam = Color.FromArgb(255, 157, 40);
        internal static readonly Color CLuc = Color.FromArgb(43, 207, 111);
        internal static readonly Color CHong = Color.FromArgb(234, 93, 151);
        internal static readonly Color CVang = Color.FromArgb(226, 175, 56);

        int _page = 1;
        Database _stateDb;
        List<FrameInfo> _frames = new List<FrameInfo>();
        string _khung;
        ObjectId _kt, _bd, _def;
        Extents3d? _bdExt, _tdExt, _tnExt;
        List<Extents3d> _tnItems = new List<Extents3d>();
        List<LayoutSheetInfo> _layoutSheets = new List<LayoutSheetInfo>();
        double _bdLen;
        int _tdN, _tnN, _modelTdSheets;
        bool _bdEstimated;
        bool _hasBd, _hasTd, _hasTn;

        readonly ComboBox cboKhung = NewCombo();
        readonly Label stKt = NewLbl("Chưa có", CLuc);
        readonly Label stBd = NewLbl("—", CLuc);
        readonly Label stTd = NewLbl("—", CLuc);
        readonly Label stTn = NewLbl("—", CLuc);
        readonly TextBox txtTLBD = NewTxt("1/1000"), txtTLTD = NewTxt("1/1000"), txtTLTN = NewTxt("1/200"), txtKC = NewTxt("350");
        readonly TextBox txtBdDai = NewTxt("2"), txtTnMoi = NewTxt("4");
        readonly ComboBox cboCat = NewCombo(), cboHuong = NewCombo(), cboXuat = NewCombo();
        readonly CheckBox chkBD = NewChk("Bình đồ", true), chkTD = NewChk("Trắc dọc", true), chkTN = NewChk("Trắc ngang", true);
        readonly CheckBox chkGop = NewChk("Gộp bình đồ + trắc dọc", false), chkAn = NewChk("Không in hình sao chép", false);
        readonly TextBox txtMau = NewTxt(""), txtChongMi = NewTxt("0"), txtLayer = NewTxt("GKIN-KHUNG"), txtBaiTo = NewTxt("4");
        readonly CheckBox chkChongMi = NewChk("Cộng chồng mí", false), chkBaiTo = NewChk("Giới hạn số tờ mỗi hàng", false);

        readonly TextBox preBD = NewTxt("BĐ - "), tenBD = NewTxt("BÌNH ĐỒ TUYẾN");
        readonly TextBox preTD = NewTxt("TĐ - "), tenTD = NewTxt("TRẮC DỌC TUYẾN");
        readonly TextBox preTN = NewTxt("TN - "), tenTN = NewTxt("TRẮC NGANG TUYẾN");
        readonly ComboBox tagSTT = NewCombo(), kieuSTT = NewCombo(), tagMS = NewCombo(), kieuMS = NewCombo();
        readonly ComboBox tagBVS = NewCombo(), kieuBVS = NewCombo(), tagTen = NewCombo(), kieuTen = NewCombo();
        readonly ComboBox tagTL = NewCombo(), kieuTL = NewCombo();
        readonly TextBox txtSoBD = NewTxt("1"), txtSoCS = NewTxt("2");

        readonly ComboBox cboPC3 = NewCombo(), cboCTB = NewCombo(), cboCTBBD = NewCombo(), cboCTBTD = NewCombo(), cboCTBTN = NewCombo();
        readonly CheckBox chkRieng = NewChk("Nét in riêng", true), chkBia = NewChk("Bìa", false), chkMuc = NewChk("Mục lục", true), chkTach = NewChk("Tách PDF từng loại", false);
        readonly TextBox txtPDF = NewTxt(""), txtInLai = NewTxt("");

        readonly PillLabel pillKhung = NewPill("Chưa có khung", CXanh);
        readonly PillLabel pillBd = NewPill("BĐ 0", CXanh);
        readonly PillLabel pillTd = NewPill("TĐ 0 tờ", CVang);
        readonly PillLabel pillTn = NewPill("TN 0 lưới", CTim);
        readonly PillLabel pillDau = NewPill("Đầu bảng 0/0", CVang);
        readonly PillLabel pillKem = NewPill("Kèm 0", CHong);
        readonly Label status = NewLbl("Sẵn sàng", CPhu);
        readonly Panel pg1 = NewPage(), pg2 = NewPage(), pg3 = NewPage(), pg4 = NewPage();
        readonly TabButton[] tabs = new TabButton[4];
        readonly Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 102, BackColor = CNen };
        readonly ToolTip hint = new ToolTip { AutoPopDelay = 10000, InitialDelay = 400, ReshowDelay = 200 };

        public MainPanel()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackColor = CNen;
            ForeColor = CChu;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point);
            Dock = DockStyle.Fill;
            MinimumSize = new Size(380, 420);

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.FromArgb(24, 35, 46),
                Padding = new Padding(4, 2, 4, 0)
            };
            string[] names = { "Bản vẽ", "Đánh số tờ", "In PDF", "Thông tin" };
            Color[] colors = { CXanh, CTim, CCam, CLuc };
            for (int i = 0; i < tabs.Length; i++)
            {
                int page = i + 1;
                var tab = new TabButton { Text = names[i], AccentColor = colors[i], Margin = new Padding(0, 0, 4, 0) };
                tab.Click += (_, __) => ShowPage(page);
                tabs[i] = tab;
                bar.Controls.Add(tab);
            }
            hint.SetToolTip(tabs[1], "Ghi số, mã, tên và tỷ lệ vào khung tên.");

            var pills = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                WrapContents = true,
                AutoScroll = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            pills.Controls.AddRange(new Control[] { pillKhung, pillBd, pillTd, pillTn, pillDau, pillKem });
            status.Dock = DockStyle.Top;
            status.Height = 18;
            status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Font = new Font("Segoe UI", 8f);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = CNen,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var run = Act("THỰC HIỆN", Color.FromArgb(24, 177, 91));
            var print = Act("In", CCard);
            print.ForeColor = CCam;
            print.FlatAppearance.BorderColor = CCam;
            print.FlatAppearance.BorderSize = 1;
            var rescan = Act("Dò lại", CCard);
            rescan.ForeColor = CXanh;
            rescan.FlatAppearance.BorderColor = CXanh;
            rescan.FlatAppearance.BorderSize = 1;
            run.Dock = DockStyle.Fill;
            run.Margin = new Padding(0, 0, 6, 0);
            run.MinimumSize = new Size(120, 28);
            print.Margin = new Padding(0, 0, 6, 0);
            rescan.Margin = Padding.Empty;
            run.Click += (_, __) => ThucHien();
            print.Click += (_, __) => ShowPage(3);
            rescan.Click += (_, __) => DoLai();
            hint.SetToolTip(print, "Mở tab In PDF. Chưa in ngay.");
            actions.Controls.Add(run, 0, 0);
            actions.Controls.Add(print, 1, 0);
            actions.Controls.Add(rescan, 2, 0);
            footer.Controls.Add(actions);
            footer.Controls.Add(status);
            footer.Controls.Add(pills);

            Build1(); Build2(); Build3(); Build4(); FillCombos();
            var body = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
            body.Controls.AddRange(new Control[] { pg4, pg3, pg2, pg1 });
            var workspace = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
            workspace.Controls.Add(body);
            workspace.Controls.Add(footer);
            Controls.Add(workspace);
            Controls.Add(bar);
            ShowPage(1);
            CapNhat();
            ResumeLayout(true);
        }

        static Panel NewPage() => new Panel { Dock = DockStyle.Fill, BackColor = CNen, AutoScroll = true };
        static PillLabel NewPill(string text, Color color) => new PillLabel { Text = text, AccentColor = color };
    }
}
