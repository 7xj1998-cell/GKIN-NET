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
            Padding = new Padding(7, 23, 7, 7);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Rounded(rect, 8))
            using (var pen = new Pen(Color.FromArgb(170, AccentColor), 1f))
                e.Graphics.DrawPath(pen, path);
            using var font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            using var brush = new SolidBrush(AccentColor);
            e.Graphics.DrawString(Caption, font, brush, 10, 4);
        }

        static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class VerticalTabButton : Control
    {
        public Color AccentColor { get; set; } = Color.DeepSkyBlue;
        public bool Selected { get; set; }

        public VerticalTabButton()
        {
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Selected ? AccentColor : Color.FromArgb(29, 42, 55));
            using (var pen = new Pen(Selected ? AccentColor : Color.FromArgb(65, 83, 100)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            e.Graphics.TranslateTransform(Width / 2f, Height / 2f);
            e.Graphics.RotateTransform(90f);
            var size = e.Graphics.MeasureString(Text, Font);
            using var brush = new SolidBrush(Selected ? Color.White : Color.FromArgb(224, 232, 240));
            e.Graphics.DrawString(Text, Font, brush, -size.Width / 2f, -size.Height / 2f);
            e.Graphics.ResetTransform();
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
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            Height = 20;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = Rounded(rect, 9);
            using var fill = new SolidBrush(Color.FromArgb(42, AccentColor));
            using var pen = new Pen(Color.FromArgb(180, AccentColor));
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, AccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            int d = radius * 2;
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
