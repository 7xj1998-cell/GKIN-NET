using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    /// <summary>
    /// Gom từng mặt cắt ngang và xếp vào lưới Ngang 1-2-3-4 hoặc Dọc 1-2-3-4.
    /// Khi đầy một tờ thì ngắt sang tờ tiếp theo.
    /// </summary>
    public static class CrossSectionPackerService
    {
        public sealed class Sheet
        {
            public int Index;
            public bool StackVertical;
            public List<Extents3d> Windows = new List<Extents3d>();
        }

        public static List<Sheet> Pack(IList<Extents3d> items, int perSheet, bool vertical)
        {
            var sheets = new List<Sheet>();
            if (items == null || items.Count == 0) return sheets;
            var ordered = vertical
                ? items.OrderByDescending(x => x.MaxPoint.Y).ThenBy(x => x.MinPoint.X).ToList()
                : items.OrderBy(x => x.MinPoint.X).ThenByDescending(x => x.MaxPoint.Y).ToList();
            int per = Math.Max(1, perSheet);
            for (int i = 0; i < ordered.Count; i += per)
            {
                sheets.Add(new Sheet
                {
                    Index = i / per + 1,
                    StackVertical = vertical,
                    Windows = ordered.Skip(i).Take(per).ToList()
                });
            }
            return sheets;
        }

        public static (double Width, double Height) TypicalSize(IList<Extents3d> items)
        {
            if (items == null || items.Count == 0) return (1, 1);
            var widths = items.Select(x => Math.Abs(x.MaxPoint.X - x.MinPoint.X)).Where(v => v > 0.01).OrderBy(v => v).ToList();
            var heights = items.Select(x => Math.Abs(x.MaxPoint.Y - x.MinPoint.Y)).Where(v => v > 0.01).OrderBy(v => v).ToList();
            double w = widths.Count == 0 ? 1 : widths[widths.Count / 2];
            double h = heights.Count == 0 ? 1 : heights[heights.Count / 2];
            return (w, h);
        }
    }
}
