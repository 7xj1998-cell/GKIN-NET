using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    public static class ProfileCutterService
    {
        static readonly Regex StationRx = new Regex(@"(?<![A-Z0-9])KM\s*\d+\s*\+\s*\d{1,3}(?:[\.,]\d+)?(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        static bool TryExt(Entity entity, out Extents3d extents)
        {
            try { extents = entity.GeometricExtents; return true; }
            catch { extents = default; return false; }
        }

        static Extents3d? Union(Extents3d? a, Extents3d b)
        {
            if (a == null) return b;
            var x = a.Value;
            x.AddExtents(b);
            return x;
        }

        public sealed class Band
        {
            public List<Extents3d> Windows = new List<Extents3d>();
            public List<Point3d> Look = new List<Point3d>();
            public List<double> Span = new List<double>();
            public List<double> Twist = new List<double>();
            public string From;
            public string To;
            public double StartStation;
            public double EndStation;
            public bool HasRealStations;
        }

        public static List<Band> Cut(Extents3d profile, double totalLength, double step, int fallbackCount, IList<RoadInteropService.Label> labels = null)
        {
            var result = new List<Band>();
            double width = Math.Max(1, profile.MaxPoint.X - profile.MinPoint.X);
            List<(double X, string Text)> stations;
            Extents3d? header;
            using (var documentLock = CadEngine.Doc?.LockDocument())
            {
                labels = labels ?? LoadLabels();
                stations = CollectStations(profile, labels);
                header = FindHeader(profile, labels);
            }
            double headerMaxX = header == null
                ? profile.MinPoint.X
                : header.Value.MaxPoint.X;
            double dataMinX = Math.Max(profile.MinPoint.X, headerMaxX);
            double dataWidth = Math.Max(1, profile.MaxPoint.X - dataMinX);
            double length = totalLength > 1 ? totalLength : dataWidth;
            var anchors = stations.Where(x => RoadInteropService.TryStation(x.Text, out _))
                .Select(x => { RoadInteropService.TryStation(x.Text, out double station); return new RoadInteropService.Anchor { Station = station, Coordinate = x.X }; })
                .GroupBy(x => Math.Round(x.Station, 3)).Select(x => x.First()).OrderBy(x => x.Station).ToList();
            bool real = RoadInteropService.Monotonic(anchors);
            double firstStation = real ? anchors[0].Station : 0;
            double lastStation = real ? anchors[anchors.Count - 1].Station : CadEngine.DrawingUnitsToMeters(length);
            if (real) length = CadEngine.MetersToDrawingUnits(lastStation - firstStation);
            else length = dataWidth;
            if (step <= 1e-6) step = length / Math.Max(1, fallbackCount);
            int count = Math.Max(1, (int)Math.Ceiling(length / step));
            if (count > 2000) throw new InvalidOperationException("Khoảng cách quá nhỏ, tạo hơn 2.000 tờ. Tăng khoảng cách cắt rồi thử lại.");
            for (int i = 0; i < count; i++)
            {
                double t0 = Math.Min(1.0, i * step / length);
                double t1 = Math.Min(1.0, (i + 1) * step / length);
                double x0 = dataMinX + dataWidth * t0;
                double x1 = dataMinX + dataWidth * t1;
                double from = firstStation + (lastStation - firstStation) * t0;
                double to = firstStation + (lastStation - firstStation) * t1;
                if (real)
                {
                    x0 = RoadInteropService.Interpolate(anchors, from);
                    x1 = RoadInteropService.Interpolate(anchors, to);
                }
                if (Math.Abs(x1 - x0) < 1e-6) continue;
                double minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1);
                foreach (var label in labels.Where(x => x.Position.Y >= profile.MinPoint.Y && x.Position.Y <= profile.MaxPoint.Y
                    && RoadInteropService.TryStation(x.Text, out _) && (Math.Abs(x.Position.X - minX) < 0.01 || Math.Abs(x.Position.X - maxX) < 0.01)))
                {
                    minX = Math.Min(minX, label.Bounds.MinPoint.X); maxX = Math.Max(maxX, label.Bounds.MaxPoint.X);
                }
                var slice = new Extents3d(
                    new Point3d(minX, profile.MinPoint.Y, 0),
                    new Point3d(maxX, profile.MaxPoint.Y, 0));
                var band = new Band
                {
                    From = CadEngine.LyTrinh(from), To = CadEngine.LyTrinh(to),
                    StartStation = from, EndStation = to, HasRealStations = real
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
                To = CadEngine.LyTrinh(lastStation), StartStation = firstStation, EndStation = lastStation, HasRealStations = real
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

        static List<RoadInteropService.Label> LoadLabels()
        {
            if (CadEngine.Db == null) return new List<RoadInteropService.Label>();
            using (var tr = CadEngine.Db.TransactionManager.StartOpenCloseTransaction())
                return RoadInteropService.ReadLabels(CadEngine.Db, tr);
        }

        public static Extents3d? FindHeader(Extents3d profile, IList<RoadInteropService.Label> labels = null)
        {
            var db = CadEngine.Db;
            if (db == null) return null;
            string[] keys =
            {
                "CAO DO", "CAO ĐỘ", "LY TRINH", "LÝ TRÌNH", "TEN COC", "TÊN CỌC",
                "COT CAO", "CỘT", "DAU BANG", "ĐẦU BẢNG", "LYTRINH", "TENCOC",
                "KHOẢNG CÁCH", "KHOANG CACH", "CAO ÑOÄ", "KHOAÛNG CAÙCH", "TEÂN COÏC", "LYÙ TRÌNH",
                "CAO ®", "KHO¶NG C¸CH", "LÝ TR×NH", "TªN CÄC"
            };
            Extents3d? acc = null;
            double textHeight = 0;
            try
            {
                {
                    double left = profile.MinPoint.X;
                    double right = profile.MinPoint.X + Math.Max(20, (profile.MaxPoint.X - profile.MinPoint.X) * 0.18);
                    foreach (var label in labels ?? LoadLabels())
                    {
                        string s = label.Text;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        string u = s.ToUpperInvariant();
                        bool hit = string.Equals(label.Layer, "TEXTHEADERTD", StringComparison.OrdinalIgnoreCase);
                        for (int i = 0; i < keys.Length && !hit; i++)
                            if (u.Contains(keys[i])) hit = true;
                        if (!hit) continue;
                        Extents3d ext = label.Bounds;
                        if (ext.MinPoint.Y > profile.MaxPoint.Y || ext.MaxPoint.Y < profile.MinPoint.Y) continue;
                        double cx = (ext.MinPoint.X + ext.MaxPoint.X) / 2.0;
                        if (cx < left - Math.Max(5, (profile.MaxPoint.X - left) * 0.70) || cx > right) continue;
                        acc = Union(acc, ext);
                        textHeight = Math.Max(textHeight, ext.MaxPoint.Y - ext.MinPoint.Y);
                    }
                }
            }
            catch { }
            if (acc == null) return null;
            double headerRight = acc.Value.MaxPoint.X + 2;
            var sourceLabels = labels ?? LoadLabels();
            if (sourceLabels.Any(x => x.Layer.Equals("TEXTHEADERTD", StringComparison.OrdinalIgnoreCase)
                && x.Position.Y >= profile.MinPoint.Y && x.Position.Y <= profile.MaxPoint.Y
                && x.Position.X >= profile.MinPoint.X && x.Position.X < headerRight))
            {
                var firstColumn = sourceLabels.Where(x => x.Position.X >= profile.MinPoint.X && x.Position.X <= profile.MaxPoint.X
                    && x.Position.Y >= profile.MinPoint.Y && x.Position.Y <= profile.MaxPoint.Y && RoadInteropService.TryStation(x.Text, out _))
                    .Select(x => x.Position.X).DefaultIfEmpty(headerRight).Min();
                if (firstColumn > headerRight && firstColumn < profile.MinPoint.X + (profile.MaxPoint.X - profile.MinPoint.X) * 0.20) headerRight = firstColumn;
            }
            return new Extents3d(
                new Point3d(Math.Min(acc.Value.MinPoint.X, profile.MinPoint.X), profile.MinPoint.Y, 0),
                new Point3d(Math.Min(profile.MaxPoint.X, headerRight), Math.Min(profile.MaxPoint.Y, acc.Value.MaxPoint.Y + textHeight), 0));
        }


        public static List<(double X, string Text)> CollectStations(Extents3d profile, IList<RoadInteropService.Label> labels = null)
        {
            var list = new List<(double, string)>();
            var db = CadEngine.Db;
            if (db == null) return list;
            try
            {
                {
                    foreach (var label in labels ?? LoadLabels())
                    {
                        string s = label.Text;
                        if (s == null) continue;
                        var match = StationRx.Match(s);
                        if (!match.Success) continue;
                        double cx = label.Position.X;
                        double cy = label.Position.Y;
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

    }
}
