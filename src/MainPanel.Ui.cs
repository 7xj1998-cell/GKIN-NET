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
        ObjectId _kt, _bd;
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
        readonly ComboBox cboCat = NewCombo(), cboHuong = NewCombo(), cboXuat = NewCombo();
        readonly CheckBox chkBD = NewChk("Bình đồ", true), chkTD = NewChk("Trắc dọc", true), chkTN = NewChk("Trắc ngang", true);
        readonly CheckBox chkGop = NewChk("Gộp bình đồ + trắc dọc", false), chkAn = NewChk("Không in hình học sao chép", false);
        readonly Panel morePanel = new Panel { BackColor = Color.Transparent, Visible = false };
        readonly TextBox txtMau = NewTxt(""), txtChongMi = NewTxt("0"), txtLayer = NewTxt("GKIN-KHUNG"), txtBaiTo = NewTxt("4");
        readonly CheckBox chkChongMi = NewChk("Chồng mí (mm)", false), chkBaiTo = NewChk("Bãi tờ (tờ/bản vẽ)", false);

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

        readonly PillLabel pillKhung = NewPill("✓ Khung", CXanh, 64);
        readonly PillLabel pillBd = NewPill("BĐ 0", CXanh, 68);
        readonly PillLabel pillTd = NewPill("TĐ 0 dải", CVang, 68);
        readonly PillLabel pillTn = NewPill("TN 0 lưới", CTim, 72);
        readonly PillLabel pillDau = NewPill("Đầu bảng 0/0", CVang, 90);
        readonly PillLabel pillKem = NewPill("Kèm 0", CHong, 58);
        readonly Label status = NewLbl("Sẵn sàng", CPhu);
        readonly Panel pg1 = NewPage(), pg2 = NewPage(), pg3 = NewPage(), pg4 = NewPage();
        readonly VerticalTabButton[] tabs = new VerticalTabButton[4];
        readonly Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = CNen };

        public MainPanel()
        {
            SuspendLayout();
            BackColor = CNen;
            ForeColor = CChu;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point);
            Dock = DockStyle.Fill;
            MinimumSize = new Size(500, 420);
            footer.Size = new Size(440, 76);

            var rail = new Panel { Dock = DockStyle.Right, Width = 38, BackColor = Color.FromArgb(24, 35, 46), Padding = new Padding(2, 5, 2, 5) };
            string[] names = { "Bản vẽ", "Đánh số tờ", "In PDF", "Thông tin" };
            Color[] colors = { CXanh, CTim, CCam, CLuc };
            for (int i = 0; i < tabs.Length; i++)
            {
                int page = i + 1;
                var tab = new VerticalTabButton
                {
                    Text = names[i], AccentColor = colors[i],
                    Dock = DockStyle.Top, Height = 96, Margin = new Padding(0)
                };
                tab.Click += (_, __) => ShowPage(page);
                tabs[i] = tab;
            }
            for (int i = tabs.Length - 1; i >= 0; i--) rail.Controls.Add(tabs[i]);

            var pills = new FlowLayoutPanel
            {
                Left = 6, Top = 1, Height = 22, Width = 430,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty
            };
            pills.Controls.AddRange(new Control[] { pillKhung, pillBd, pillTd, pillTn, pillDau, pillKem });
            status.SetBounds(8, 23, 422, 16);
            status.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            status.Font = new Font("Segoe UI", 7.25f);
            var run = Act("▶  THỰC HIỆN", Color.FromArgb(24, 177, 91));
            var print = Act("In ▸", CCard); print.ForeColor = CCam; print.FlatAppearance.BorderColor = CCam; print.FlatAppearance.BorderSize = 1;
            var rescan = Act("↻  Dò lại", CCard); rescan.ForeColor = CXanh; rescan.FlatAppearance.BorderColor = CXanh; rescan.FlatAppearance.BorderSize = 1;
            run.SetBounds(7, 41, 238, 32);
            print.SetBounds(252, 41, 79, 32);
            rescan.SetBounds(338, 41, 96, 32);
            run.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            print.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            rescan.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            run.Click += (_, __) => ThucHien();
            print.Click += (_, __) => ShowPage(3);
            rescan.Click += (_, __) => DoLai();
            footer.Controls.AddRange(new Control[] { pills, status, run, print, rescan });

            Build1(); Build2(); Build3(); Build4(); FillCombos();
            var body = new Panel { Dock = DockStyle.Fill, BackColor = CNen, Padding = new Padding(5, 5, 5, 1) };
            body.Controls.AddRange(new Control[] { pg4, pg3, pg2, pg1 });
            var workspace = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
            workspace.Controls.Add(body);
            workspace.Controls.Add(footer);
            Controls.Add(workspace);
            Controls.Add(rail);
            ShowPage(1);
            CapNhat();
            ResumeLayout(true);
        }

        static Panel NewPage() => new Panel { Dock = DockStyle.Fill, BackColor = CNen };
        static PillLabel NewPill(string text, Color color, int width) => new PillLabel { Text = text, AccentColor = color, Width = width, Margin = new Padding(0, 0, 3, 0) };
    }
}
