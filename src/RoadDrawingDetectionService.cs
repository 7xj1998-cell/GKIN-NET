using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    public static class RoadDrawingDetectionService
    {
        static double Width(Extents3d e) => e.MaxPoint.X - e.MinPoint.X;
        static double Height(Extents3d e) => e.MaxPoint.Y - e.MinPoint.Y;
        static Point3d Center(Extents3d e) => e.MinPoint + (e.MaxPoint - e.MinPoint) * 0.5;
        static bool Contains(Extents3d e, Point3d p) => p.X >= e.MinPoint.X && p.X <= e.MaxPoint.X && p.Y >= e.MinPoint.Y && p.Y <= e.MaxPoint.Y;

        // Do not grow by intersecting arbitrary bounding boxes: an alignment,
        // frame or site survey can span the entire drawing.
        public static Extents3d CollectCell(Extents3d seed, Extents3d cell, IList<Extents3d> geometry)
        {
            var result = seed;
            foreach (var e in geometry)
            {
                if (!Contains(cell, Center(e)) || Width(e) > Width(cell) || Height(e) > Height(cell)) continue;
                var clipped = new Extents3d(new Point3d(Math.Max(cell.MinPoint.X, e.MinPoint.X), Math.Max(cell.MinPoint.Y, e.MinPoint.Y), 0),
                    new Point3d(Math.Min(cell.MaxPoint.X, e.MaxPoint.X), Math.Min(cell.MaxPoint.Y, e.MaxPoint.Y), 0));
                result.AddExtents(clipped);
            }
            return result;
        }

        public static List<Extents3d> Profiles(IList<Extents3d> seeds, IList<Extents3d> geometry)
        {
            var result = new List<Extents3d>();
            foreach (var seed in seeds.OrderByDescending(Width))
            {
                if (result.Any(x => Contains(x, Center(seed)))) continue;
                double w = Math.Max(1, Width(seed)), h = Math.Max(1, Height(seed));
                var cell = new Extents3d(new Point3d(seed.MinPoint.X - w * 0.08, seed.MinPoint.Y - Math.Max(h * 4, w * 0.12), 0),
                    new Point3d(seed.MaxPoint.X + w * 0.02, seed.MaxPoint.Y + Math.Max(h * 2, w * 0.06), 0));
                result.Add(CollectCell(seed, cell, geometry));
            }
            return result.OrderByDescending(x => x.MaxPoint.Y).ThenBy(x => x.MinPoint.X).ToList();
        }

        public static List<Extents3d> Sections(IList<Extents3d> seeds, IList<Extents3d> geometry)
        {
            var result = new List<Extents3d>();
            foreach (var seed in seeds)
            {
                double w = Math.Max(1, Width(seed)), h = Math.Max(1, Height(seed));
                var c = Center(seed);
                // Neighbour seeds define the cell; one surface curve can be
                // almost flat, so its height is not a table-height estimate.
                var row = seeds.Where(x => Math.Abs(Center(x).Y - c.Y) < w * 0.35).ToList();
                var column = seeds.Where(x => Math.Abs(Center(x).X - c.X) < w * 0.60).ToList();
                var left = row.Where(x => Center(x).X < c.X - w * 0.1).OrderByDescending(x => Center(x).X).ToList();
                var right = row.Where(x => Center(x).X > c.X + w * 0.1).OrderBy(x => Center(x).X).ToList();
                var below = column.Where(x => Center(x).Y < c.Y - h * 0.5).OrderByDescending(x => Center(x).Y).ToList();
                var above = column.Where(x => Center(x).Y > c.Y + h * 0.5).OrderBy(x => Center(x).Y).ToList();
                double dx = right.Count > 0 ? Center(right[0]).X - c.X : left.Count > 0 ? c.X - Center(left[0]).X : w * 1.5;
                double dy = below.Count > 0 ? c.Y - Center(below[0]).Y : above.Count > 0 ? Center(above[0]).Y - c.Y : Math.Max(h * 4, w * 0.6);
                // VNroad table lies below the terrain; its stake caption is
                // above. Adjacent rows must not contribute their captions.
                var cell = new Extents3d(new Point3d(seed.MinPoint.X - Math.Max(0.1, dx - w) * 0.48, seed.MinPoint.Y - dy * 0.55, 0),
                    new Point3d(seed.MaxPoint.X + Math.Max(0.1, dx - w) * 0.48, seed.MaxPoint.Y + dy * 0.40, 0));
                result.Add(CollectCell(seed, cell, geometry));
            }
            return result;
        }
    }
}
