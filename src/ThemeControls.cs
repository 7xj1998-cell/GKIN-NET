using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GKIN
{
    internal sealed class AccentCard : Panel
    {
        public string Caption { get; set; } = "";
        public Color AccentColor { get; set; } = Color.DeepSkyBlue;

        public AccentCard()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(41, 55, 69);
            Padding = new Padding(8, 28, 8, 10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 20 || Height < 20) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Rounded(rect, 8))
            using (var pen = new Pen(Color.FromArgb(170, AccentColor), 1f))
                e.Graphics.DrawPath(pen, path);
            var caption = new Rectangle(10, 4, Math.Max(10, Width - 20), 20);
            TextRenderer.DrawText(e.Graphics, Caption, Font, caption, AccentColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class TabButton : Control
    {
        public Color AccentColor { get; set; } = Color.DeepSkyBlue;
        public bool Selected { get; set; }

        public TabButton()
        {
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Height = 30;
        }

        public override string Text
        {
            get => base.Text;
            set { base.Text = value; Fit(); }
        }

        void Fit()
        {
            var size = TextRenderer.MeasureText(Text ?? "", Font);
            Width = size.Width + 22;
            Height = 30;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Selected ? AccentColor : Color.FromArgb(29, 42, 55));
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Selected ? Color.White : Color.FromArgb(224, 232, 240),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Invalidate(); }
    }

    internal sealed class PillLabel : Control
    {
        public Color AccentColor { get; set; } = Color.DeepSkyBlue;

        public PillLabel()
        {
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            Height = 22;
            Margin = new Padding(0, 0, 4, 4);
        }

        public override string Text
        {
            get => base.Text;
            set { base.Text = value; Fit(); }
        }

        void Fit()
        {
            var size = TextRenderer.MeasureText(Text ?? "", Font);
            Width = size.Width + 16;
            Height = 22;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = Rounded(rect, 9);
            using var fill = new SolidBrush(Color.FromArgb(42, AccentColor));
            using var pen = new Pen(Color.FromArgb(180, AccentColor));
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, AccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
