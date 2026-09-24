using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    /// <summary>
    /// Cắt dải trắc dọc theo bước lý trình, nhân bản cụm đầu bảng
    /// và bóc text Km... ở hai mép đoạn cắt.
    /// </summary>
    public static class ProfileCutterService
    {
        public sealed class Band
        {
            public List<Extents3d> Windows = new List<Extents3d>();
            public List<Point3d> Look = new List<Point3d>();
            public List<double> Span = new List<double>();
            public List<double> Twist = new List<double>();
            public string From;
            public string To;
        }

        public static List<Band> Cut(Extents3d profile, double totalLength, double step, int fallbackCount)
        {
            var result = new List<Band>();
            double width = Math.Max(1, profile.MaxPoint.X - profile.MinPoint.X);
            var stations = CollectStations(profile);
            Extents3d? header = FindHeader(profile);
            double headerMaxX = header == null
                ? profile.MinPoint.X + Math.Min(width * 0.12, 80)
                : header.Value.MaxPoint.X;
            double dataMinX = Math.Max(profile.MinPoint.X, headerMaxX);
            double dataWidth = Math.Max(1, profile.MaxPoint.X - dataMinX);
            double length = totalLength > 1 ? totalLength : dataWidth;
            if (step <= 1) step = length / Math.Max(1, fallbackCount);
            int count = Math.Max(1, (int)Math.Ceiling(length / step));
            for (int i = 0; i < count; i++)
            {
                double t0 = Math.Min(1.0, i * step / length);
                double t1 = Math.Min(1.0, (i + 1) * step / length);
                double x0 = dataMinX + dataWidth * t0;
                double x1 = dataMinX + dataWidth * t1;
                if (x1 - x0 < 1) continue;
                var slice = new Extents3d(
                    new Point3d(x0, profile.MinPoint.Y, 0),
                    new Point3d(x1, profile.MaxPoint.Y, 0));
                var band = new Band
                {
                    From = StationAt(stations, x0, true) ?? CadEngine.LyTrinh(i * step),
                    To = StationAt(stations, x1, false) ?? CadEngine.LyTrinh(Math.Min(length, (i + 1) * step))
                };
                if (header != null)
                    AddWindow(band, header.Value, 1.04);
                AddWindow(band, slice, 1.02);
                result.Add(band);
            }
            if (result.Count > 0) return result;
            var whole = new Band
            {
                From = CadEngine.LyTrinh(0),
                To = CadEngine.LyTrinh(length)
            };
            AddWindow(whole, profile, 1.0);
            result.Add(whole);
            return result;
        }

        static void AddWindow(Band band, Extents3d window, double spanFactor)
        {
            band.Windows.Add(window);
            band.Look.Add(new Point3d(
                (window.MinPoint.X + window.MaxPoint.X) / 2.0,
                (window.MinPoint.Y + window.MaxPoint.Y) / 2.0, 0));
            band.Span.Add(Math.Max(1, window.MaxPoint.X - window.MinPoint.X) * spanFactor);
            band.Twist.Add(0);
        }

        public static Extents3d? FindHeader(Extents3d profile)
        {
            var db = CadEngine.Db;
            if (db == null) return FallbackHeader(profile);
            string[] keys =
            {
                "CAO DO", "CAO ĐỘ", "LY TRINH", "LÝ TRÌNH", "TEN COC", "TÊN CỌC",
                "COT CAO", "CỘT", "DAU BANG", "ĐẦU BẢNG", "LYTRINH", "TENCOC"
            };
            Extents3d? acc = null;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    double left = profile.MinPoint.X;
                    double right = profile.MinPoint.X + Math.Max(20, (profile.MaxPoint.X - profile.MinPoint.X) * 0.18);
                    foreach (ObjectId id in ms)
                    {
                        var obj = tr.GetObject(id, OpenMode.ForRead, false);
                        string s = obj switch { DBText t => t.TextString, MText m => m.Contents, _ => null };
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        string u = s.ToUpperInvariant();
                        bool hit = false;
                        for (int i = 0; i < keys.Length && !hit; i++)
                            if (u.Contains(keys[i])) hit = true;
                        if (!hit) continue;
                        if (obj is not Entity ent || !CadEngine.TryEntityExtents(ent, out Extents3d ext)) continue;
                        double cx = (ext.MinPoint.X + ext.MaxPoint.X) / 2.0;
                        if (cx < left - 5 || cx > right) continue;
                        acc = CadEngine.UnionExtents(acc, ext);
                    }
                }
            }
            catch { }
            if (acc == null) return FallbackHeader(profile);
            return new Extents3d(
                new Point3d(Math.Min(acc.Value.MinPoint.X, profile.MinPoint.X), profile.MinPoint.Y, 0),
                new Point3d(Math.Min(profile.MaxPoint.X, acc.Value.MaxPoint.X + 2), profile.MaxPoint.Y, 0));
        }

        static Extents3d FallbackHeader(Extents3d profile)
        {
            double w = Math.Min(80, Math.Max(15, (profile.MaxPoint.X - profile.MinPoint.X) * 0.10));
            return new Extents3d(
                new Point3d(profile.MinPoint.X, profile.MinPoint.Y, 0),
                new Point3d(profile.MinPoint.X + w, profile.MaxPoint.Y, 0));
        }

        public static List<(double X, string Text)> CollectStations(Extents3d profile)
        {
            var list = new List<(double, string)>();
            var db = CadEngine.Db;
            if (db == null) return list;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        var obj = tr.GetObject(id, OpenMode.ForRead, false);
                        string s = obj switch { DBText t => t.TextString, MText m => m.Contents, _ => null };
                        if (s == null) continue;
                        var match = CadEngine.StationPattern.Match(s);
                        if (!match.Success) continue;
                        if (obj is not Entity ent || !CadEngine.TryEntityExtents(ent, out Extents3d ext)) continue;
                        double cx = (ext.MinPoint.X + ext.MaxPoint.X) / 2.0;
                        double cy = (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0;
                        if (cx < profile.MinPoint.X - 5 || cx > profile.MaxPoint.X + 5) continue;
                        if (cy < profile.MinPoint.Y - 5 || cy > profile.MaxPoint.Y + 5) continue;
                        list.Add((cx, match.Value.Replace(" ", "")));
                    }
                }
            }
            catch { }
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return list;
        }

        public static string StationAt(List<(double X, string Text)> stations, double x, bool left)
        {
            if (stations == null || stations.Count == 0) return null;
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i < stations.Count; i++)
            {
                double d = left
                    ? Math.Abs(stations[i].X - x) + (stations[i].X < x - 1 ? 0 : 2)
                    : Math.Abs(stations[i].X - x) + (stations[i].X > x + 1 ? 0 : 2);
                if (d < bestD) { bestD = d; best = i; }
            }
            return stations[best].Text;
        }
    }
}
