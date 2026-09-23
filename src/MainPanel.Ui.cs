using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

namespace GKIN
{
    public partial class MainPanel : UserControl
    {
        static readonly Color CNen = Color.FromArgb(11, 15, 23);
        static readonly Color CCard = Color.FromArgb(21, 27, 38);
        static readonly Color CChu = Color.FromArgb(232, 236, 244);
        static readonly Color CPhu = Color.FromArgb(139, 147, 167);
        static readonly Color CXanh = Color.FromArgb(59, 130, 246);
        static readonly Color CTim = Color.FromArgb(139, 92, 246);
        static readonly Color CCam = Color.FromArgb(245, 158, 11);
        static readonly Color CLuc = Color.FromArgb(34, 197, 94);
        static readonly Color CLucDam = Color.FromArgb(21, 128, 61);

        int _page = 1;
        List<FrameInfo> _frames = new List<FrameInfo>();
        string _khung;
        ObjectId _kt, _bd;
        double _bdLen;
        int _tdN, _tnN;
        bool _hasBd, _hasTd, _hasTn;

        readonly ComboBox cboKhung = NewCombo();
        readonly Label stKt = NewLbl("chua co", CLuc);
        readonly Label stBd = NewLbl("-", CLuc);
        readonly Label stTd = NewLbl("-", CLuc);
        readonly Label stTn = NewLbl("-", CLuc);
        readonly TextBox txtTLBD = NewTxt("1000"), txtTLTD = NewTxt("1000"), txtTLTN = NewTxt("200"), txtKC = NewTxt("350");
        readonly ComboBox cboCat = NewCombo(), cboBang = NewCombo(), cboXep = NewCombo(), cboHuong = NewCombo(), cboXuat = NewCombo();
        readonly CheckBox chkBD = NewChk("Binh do", true), chkTD = NewChk("Trac doc", true), chkTN = NewChk("Trac ngang", true);
        readonly CheckBox chkGop = NewChk("Gop BD+TD", false), chkAn = NewChk("An khung rai", false), chkNhieu = NewChk("Nhieu tuyen", false);
        readonly TextBox preBD = NewTxt("BD - "), tenBD = NewTxt("BINH DO TUYEN");
        readonly TextBox preTD = NewTxt("TD - "), tenTD = NewTxt("TRAC DOC TUYEN");
        readonly TextBox preTN = NewTxt("TN - "), tenTN = NewTxt("TRAC NGANG TUYEN");
        readonly ComboBox tagSTT = NewCombo(), kieuSTT = NewCombo(), tagMS = NewCombo(), kieuMS = NewCombo();
        readonly ComboBox tagBVS = NewCombo(), kieuBVS = NewCombo(), tagTen = NewCombo(), kieuTen = NewCombo();
        readonly ComboBox tagTL = NewCombo(), kieuTL = NewCombo();
        readonly TextBox txtSoBD = NewTxt("1"), txtSoCS = NewTxt("2");
        readonly ComboBox cboPC3 = NewCombo(), cboCTB = NewCombo(), cboCTBBD = NewCombo(), cboCTBTD = NewCombo(), cboCTBTN = NewCombo();
        readonly CheckBox chkRieng = NewChk("Net rieng", false), chkMuc = NewChk("Muc luc", true), chkTach = NewChk("Tach PDF", false);
        readonly TextBox txtPDF = NewTxt(""), txtInLai = NewTxt("");
        readonly Label pills = NewLbl(".. Khung | .. BD | .. TD | .. TN", CLuc);
        readonly Label status = NewLbl(" ", CPhu);
        readonly Panel pg1 = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
        readonly Panel pg2 = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
        readonly Panel pg3 = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
        readonly Panel pg4 = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
        readonly Button[] tabs = new Button[4];

        public MainPanel()
        {
            BackColor = CNen; ForeColor = CChu; Font = new Font("Segoe UI", 8.25f); Dock = DockStyle.Fill;
            var rail = new Panel { Dock = DockStyle.Right, Width = 36, BackColor = CNen };
            string[] tn = { "Ban ve", "So to", "In PDF", "TT" };
            Color[] tc = { CXanh, CTim, CCam, CLuc };
            for (int i = 0; i < 4; i++)
            {
                int p = i + 1;
                var b = new Button { Dock = DockStyle.Top, Height = 120, FlatStyle = FlatStyle.Flat, Text = tn[i], ForeColor = CChu, BackColor = Mix(tc[i], i == 0 ? 1 : 0.3), Font = new Font("Segoe UI", 8, FontStyle.Bold) };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (_, __) => ShowPage(p);
                tabs[i] = b;
            }
            for (int i = 3; i >= 0; i--) rail.Controls.Add(tabs[i]);
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 88, BackColor = CNen };
            pills.SetBounds(8, 4, 320, 18); status.SetBounds(8, 24, 320, 16);
            var run = Act("THUC HIEN", CLucDam, 8, 46, 168);
            var inn = Act("In", CCard, 182, 46, 56); inn.ForeColor = CCam;
            var dol = Act("Do lai", CCard, 244, 46, 72); dol.ForeColor = CXanh;
            run.Click += (_, __) => ThucHien();
            inn.Click += (_, __) => ShowPage(3);
            dol.Click += (_, __) => DoLai();
            foot.Controls.AddRange(new Control[] { pills, status, run, inn, dol });
            Build1(); Build2(); Build3(); Build4(); FillCombos();
            var body = new Panel { Dock = DockStyle.Fill, BackColor = CNen };
            body.Controls.AddRange(new Control[] { pg4, pg3, pg2, pg1 });
            Controls.Add(body); Controls.Add(foot); Controls.Add(rail);
            ShowPage(1);
        }
    }
}
