using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using PdfSharp.Pdf.IO;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GKIN
{
    public class FrameInfo
    {
        public string Name;
        public int Count;
        public ObjectId Sample;
        public int W, H;
        public ObjectId Definition;
        internal int Priority;
        public override string ToString() => $"{Name} · {Count} khung · {W}x{H}";
    }

    public class LayoutSheetInfo
    {
        public string LayoutName;
        public string Type;
        public int Index;
        public ObjectId LayoutId;
        public ObjectId FrameId;
    }

    public class PlotSheetRequest
    {
        public ObjectId LayoutId;
        public Extents2d? Window;
        public string Ctb;
        public string Type;
    }

    public static partial class CadEngine
    {
        sealed class SheetPlan
        {
            public string Type;
            public int Index;
            public bool StackVertical = true;
            public int Slots = 1;
            public List<Extents3d> Windows = new List<Extents3d>();
            public List<double> Twist = new List<double>();
            public List<Point3d> Look = new List<Point3d>();
            public List<double> Span = new List<double>();
        }

        sealed class Strip
        {
            public Extents3d Ext;
            public Point3d Look;
            public double Twist;
            public double Span;
        }

        static readonly Regex StationRegex = new Regex(@"(?<![A-Z0-9])KM\s*\d+\s*\+\s*\d{1,3}(?:[\.,]\d+)?(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex SheetLayoutRegex = new Regex(@"^GKIN-(BD|TD|TN)-(\d+)(?:-\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static Document Doc => AcadApp.DocumentManager.MdiActiveDocument;
        public static Editor Ed => Doc?.Editor;
        public static Database Db => Doc?.Database;
        public static string LastError { get; private set; }
        public static List<ObjectId> LastFrames { get; private set; } = new List<ObjectId>();
        public static List<string> LastTypes { get; private set; } = new List<string>();

        public static void ClearTransientSheets()
        {
            LastFrames = new List<ObjectId>();
            LastTypes = new List<string>();
        }

        public static bool SameDatabase(Database first, Database second)
        {
            if (first == null || second == null) return false;
            try { return first.UnmanagedObject == second.UnmanagedObject; }
            catch { return first.FingerprintGuid == second.FingerprintGuid; }
        }

        static bool IsCurrentDatabase(ObjectId id) => !id.IsNull && id.Database != null && SameDatabase(id.Database, Db);

        public static string Pad(int n, int cs) => Math.Max(0, n).ToString().PadLeft(Math.Max(1, cs), '0');

        public static string LyTrinh(double m)
        {
            int km = (int)Math.Floor(m / 1000.0);
            double le = m - km * 1000.0;
            return $"Km{km}+{le:0.00}";
        }

        public static string FmtM(double m) =>
            m >= 1000 ? $"{m / 1000.0:0.000} km" : $"{m:0} m";

        static readonly string[] TitleTags =
        {
            "STT", "SOTT", "TOSO",
            "TENBVE", "TENBV", "TENBANVE", "TENTO", "TENTOBVE",
            "MSBV", "SBV", "MABV", "MASO", "MATO",
            "BVS", "SOBV", "TONGTO",
            "TYLE", "TILE", "TL"
        };

        static bool IsTitleTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < TitleTags.Length; i++)
                if (tag.Equals(TitleTags[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string ShortBlockName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int bar = name.LastIndexOf('|');
            return bar >= 0 ? name.Substring(bar + 1) : name;
        }

        public static List<FrameInfo> QuetKhung()
        {
            var map = new Dictionary<string, FrameInfo>(StringComparer.OrdinalIgnoreCase);
            if (Db == null) return new List<FrameInfo>();
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                RememberDefinitions(Db, tr, map);
                RememberInserts(Db, tr, map);
                tr.Commit();
            }
            return map.Values.Where(x => x.Priority > 0)
                .OrderByDescending(x => x.Priority).ThenByDescending(x => x.Count).ThenBy(x => x.Name).ToList();
        }

        static void RememberDefinitions(Database db, Transaction tr, Dictionary<string, FrameInfo> map)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId bid in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(bid, OpenMode.ForRead);
                int score = DefinitionScore(btr, tr);
                if (score <= 0) continue;
                string shortName = ShortBlockName(btr.Name);
                if (!map.TryGetValue(shortName, out var rec) || score > rec.Priority)
                {
                    rec ??= new FrameInfo();
                    rec.Name = shortName;
                    rec.Priority = score;
                    rec.Definition = bid;
                    Extents3d? acc = null;
                    foreach (ObjectId id in btr)
                        if (tr.GetObject(id, OpenMode.ForRead, false) is Entity e && e is not AttributeDefinition && TryExtents(e, out Extents3d ext))
                            acc = Union(acc, ext);
                    if (acc != null)
                    {
                        rec.W = (int)Math.Round(acc.Value.MaxPoint.X - acc.Value.MinPoint.X);
                        rec.H = (int)Math.Round(acc.Value.MaxPoint.Y - acc.Value.MinPoint.Y);
                    }
                    map[shortName] = rec;
                }
            }
        }

        static void RememberInserts(Database db, Transaction tr, Dictionary<string, FrameInfo> map)
        {
            var layouts = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in layouts)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                var space = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                foreach (ObjectId id in space)
                {
                    if (tr.GetObject(id, OpenMode.ForRead, false) is not BlockReference br) continue;
                    int priority = FramePriority(br, tr);
                    if (priority <= 0) continue;
                    string nm = ShortBlockName(EffectiveName(br));
                    if (!map.TryGetValue(nm, out var rec))
                    {
                        rec = new FrameInfo { Name = nm, Priority = priority };
                        map[nm] = rec;
                    }
                    rec.Count++;
                    rec.Priority = Math.Max(rec.Priority, priority);
                    if (rec.Sample.IsNull)
                    {
                        rec.Sample = id;
                        if (TryExtents(br, out Extents3d ext))
                        {
                            rec.W = (int)Math.Round(ext.MaxPoint.X - ext.MinPoint.X);
                            rec.H = (int)Math.Round(ext.MaxPoint.Y - ext.MinPoint.Y);
                        }
                    }
                    if (rec.Definition.IsNull)
                        rec.Definition = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                }
            }
        }

        static int DefinitionScore(BlockTableRecord btr, Transaction tr)
        {
            if (btr.IsLayout || btr.IsAnonymous) return -1;
            string name = btr.Name ?? "";
            if (name.Length == 0 || name[0] == '*') return -1;
            string shortName = ShortBlockName(name);
            if (shortName.Length == 0 || shortName[0] == '*') return -1;
            bool xrefRoot = false;
            try { xrefRoot = btr.IsFromExternalReference && name.IndexOf('|') < 0; }
            catch { }
            int att = 0;
            try
            {
                foreach (ObjectId id in btr)
                    if (tr.GetObject(id, OpenMode.ForRead, false) is AttributeDefinition ad && IsTitleTag(ad.Tag))
                        att++;
            }
            catch { }
            if (xrefRoot && att == 0) return shortName.IndexOf("KHUNG", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
            int score = 0;
            if (att > 0) score += 5 + Math.Min(att, 4);
            if (shortName.IndexOf("KHUNG", StringComparison.OrdinalIgnoreCase) >= 0) score += 3;
            return score;
        }

        static int FramePriority(BlockReference br, Transaction tr)
        {
            string name = EffectiveName(br) ?? "";
            string shortName = ShortBlockName(name);
            if (shortName.StartsWith("*U", StringComparison.OrdinalIgnoreCase)
                || shortName.StartsWith("*D", StringComparison.OrdinalIgnoreCase)) return -1;
            int att = 0;
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (tr.GetObject(id, OpenMode.ForRead, false) is not AttributeReference attribute) continue;
                if (IsTitleTag(attribute.Tag)) att++;
            }
            bool xrefRoot = false;
            try
            {
                var btr = (BlockTableRecord)tr.GetObject(br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord, OpenMode.ForRead);
                xrefRoot = btr.IsFromExternalReference && (btr.Name ?? "").IndexOf('|') < 0;
            }
            catch { }
            if (att > 0) return 5 + att;
            if (xrefRoot) return 0;
            return shortName.IndexOf("KHUNG", StringComparison.OrdinalIgnoreCase) >= 0 ? 3 : 0;
        }

        public static string EffectiveName(BlockReference br)
        {
            try
            {
                if (br.IsDynamicBlock)
                    return ((BlockTableRecord)br.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name;
            }
            catch { }
            return br.Name;
        }

        sealed class PathSample
        {
            public List<Point3d> Points;
            public double Length;
        }

        static string RxName(DBObject obj)
        {
            try
            {
                if (obj is ProxyEntity proxy && !string.IsNullOrWhiteSpace(proxy.OriginalClassName))
                    return proxy.OriginalClassName;
            }
            catch { }
            try { return obj.GetRXClass()?.Name ?? obj.GetType().Name; }
            catch { return obj.GetType().Name; }
        }

        static bool TryLen(Curve curve, out double length)
        {
            try
            {
                length = Math.Abs(curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam));
                return length > 1;
            }
            catch { length = 0; return false; }
        }

        static bool IsAlignment(Entity ent)
        {
            string layer = (ent.Layer ?? "").ToUpperInvariant();
            string rx = RxName(ent).ToUpperInvariant();
            if (rx.Contains("ALIGNMENT") || rx.Contains("TDTDBALIGN")) return true;

            // VNroad 7.1 without its object enabler exposes TDTDBALIGNMENT as
            // AcDbZombieEntity/ACAD_PROXY_ENTITY.  The stable signal left in the
            // drawing is the TUYEN layer (and normally an extension dictionary).
            return layer == "TUYEN"
                && (ent is ProxyEntity || rx.Contains("ZOMBIE") || rx.Contains("PROXY"));
        }

        static bool IsProfile(Entity ent)
        {
            string layer = (ent.Layer ?? "").ToUpperInvariant();
            if (layer.Contains("PLINETNTN") || layer.Contains("TRACNGANG")) return false;
            return layer.Contains("PLINETDTN") || layer.Contains("PLINETD") || layer.Contains("TRACDOC")
                || layer.Contains("PROFILE");
        }

        static bool IsSection(Entity ent)
        {
            string layer = (ent.Layer ?? "").ToUpperInvariant();
            if (layer.Contains("PLINETDTN") || layer.Contains("PLINETD") || layer.Contains("TRACDOC") || layer == "TUYEN") return false;
            return HasXDataApp(ent, "KS_TN")
                || layer.Contains("PLINETNTN") || layer.Contains("PLINETN") || layer.Contains("TRACNGANG")
                || layer.Contains("MATCAT") || layer.Contains("TCTN");
        }

        static bool HasXDataApp(DBObject value, string application)
        {
            if (value == null || string.IsNullOrWhiteSpace(application)) return false;
            ResultBuffer data = null;
            try
            {
                data = value.XData;
                if (data == null) return false;
                foreach (TypedValue item in data)
                    if (item.TypeCode == (int)DxfCode.ExtendedDataRegAppName
                        && string.Equals(Convert.ToString(item.Value), application, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            finally { data?.Dispose(); }
            return false;
        }

        public static double MeasureLength(Entity ent)
        {
            var sample = SampleEntity(ent);
            return sample == null ? 0 : sample.Length;
        }

        public static bool ExtentsOf(Entity entity, out Extents3d extents) => TryExtents(entity, out extents);

        static PathSample SampleEntity(Entity ent)
        {
            if (ent is Curve curve && !(ent is Circle) && TryLen(curve, out double len))
                return SampleCurve(curve, len);
            DBObjectCollection bag = null;
            try
            {
                bag = new DBObjectCollection();
                ent.Explode(bag);
            }
            catch { bag = null; }
            try
            {
                if (bag != null && bag.Count > 0)
                {
                    Curve longest = null;
                    double longestLen = 0, sum = 0;
                    var segs = new List<Point3d[]>();
                    foreach (DBObject obj in bag)
                    {
                        if (obj is not Curve part || !TryLen(part, out double partLen)) continue;
                        sum += partLen;
                        if (partLen > longestLen) { longestLen = partLen; longest = part; }
                        if (part is Line line) segs.Add(new[] { line.StartPoint, line.EndPoint });
                        else if (part is Polyline pl)
                        {
                            var piece = new List<Point3d>();
                            int n = pl.NumberOfVertices;
                            for (int i = 0; i < n; i++) piece.Add(pl.GetPoint3dAt(i));
                            if (piece.Count >= 2) segs.Add(piece.ToArray());
                        }
                    }
                    if (longest != null && longestLen >= sum * 0.45) return SampleCurve(longest, longestLen);
                    var chained = Chain(segs);
                    if (chained != null && chained.Length >= longestLen) return chained;
                    if (longest != null) return SampleCurve(longest, longestLen);
                }
            }
            finally
            {
                if (bag != null)
                    foreach (DBObject obj in bag)
                        try { obj.Dispose(); } catch { }
            }
            try
            {
                var grips = new Point3dCollection();
                ent.GetGripPoints(grips, new IntegerCollection(), new IntegerCollection());
                if (grips.Count >= 2)
                {
                    var pts = new List<Point3d>();
                    foreach (Point3d p in grips) pts.Add(p);
                    return OrderPoints(pts);
                }
            }
            catch { }
            return null;
        }

        static PathSample SampleCurve(Curve curve, double length)
        {
            int n = Math.Max(16, (int)Math.Min(500, Math.Ceiling(length / 10.0)));
            var pts = new List<Point3d>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                try { pts.Add(curve.GetPointAtDist(Math.Min(length, length * i / n))); }
                catch { }
            }
            if (pts.Count < 2) return null;
            return new PathSample { Points = pts, Length = length };
        }

        static PathSample Chain(List<Point3d[]> segs)
        {
            if (segs == null || segs.Count == 0) return null;
            var used = new bool[segs.Count];
            Point3d end = segs[0][segs[0].Length - 1];
            int best = 0;
            double bestDeg = double.MaxValue;
            for (int i = 0; i < segs.Count; i++)
            {
                int deg = 0;
                Point3d a = segs[i][0];
                for (int j = 0; j < segs.Count; j++)
                {
                    if (i == j) continue;
                    if (Near(a, segs[j][0], 0.5) || Near(a, segs[j][segs[j].Length - 1], 0.5)) deg++;
                }
                if (deg < bestDeg) { bestDeg = deg; best = i; end = a; }
            }
            var pts = new List<Point3d>();
            for (int guard = 0; guard < segs.Count; guard++)
            {
                int pick = -1;
                bool flip = false;
                double pickDist = 2.0;
                for (int i = 0; i < segs.Count; i++)
                {
                    if (used[i]) continue;
                    double d0 = Dist2(end, segs[i][0]);
                    double d1 = Dist2(end, segs[i][segs[i].Length - 1]);
                    if (d0 <= d1 && d0 < pickDist) { pickDist = d0; pick = i; flip = false; }
                    else if (d1 < pickDist) { pickDist = d1; pick = i; flip = true; }
                }
                if (pick < 0) break;
                used[pick] = true;
                var seg = segs[pick];
                if (flip) Array.Reverse(seg);
                if (pts.Count == 0) pts.Add(seg[0]);
                for (int k = 1; k < seg.Length; k++) pts.Add(seg[k]);
                end = pts[pts.Count - 1];
            }
            return pts.Count >= 2 ? Measure(pts) : null;
        }

        static PathSample OrderPoints(List<Point3d> pts)
        {
            if (pts.Count < 2) return null;
            int start = 0;
            double far = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                double d = Dist2(pts[i], pts[0]);
                if (d > far) { far = d; start = i; }
            }
            var left = new List<Point3d>(pts);
            var ordered = new List<Point3d> { left[start] };
            left.RemoveAt(start);
            while (left.Count > 0)
            {
                int near = 0;
                double best = double.MaxValue;
                Point3d tail = ordered[ordered.Count - 1];
                for (int i = 0; i < left.Count; i++)
                {
                    double d = Dist2(tail, left[i]);
                    if (d < best) { best = d; near = i; }
                }
                ordered.Add(left[near]);
                left.RemoveAt(near);
            }
            return Measure(ordered);
        }

        static PathSample Measure(List<Point3d> pts)
        {
            double len = 0;
            for (int i = 1; i < pts.Count; i++) len += Math.Sqrt(Dist2(pts[i - 1], pts[i]));
            if (len < 1) return null;
            return new PathSample { Points = pts, Length = len };
        }

        static bool Near(Point3d a, Point3d b, double tol) => Dist2(a, b) <= tol * tol;
        static double Dist2(Point3d a, Point3d b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        static Point3d At(PathSample path, double dist)
        {
            var pts = path.Points;
            if (dist <= 0) return pts[0];
            double walked = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                double step = Math.Sqrt(Dist2(pts[i - 1], pts[i]));
                if (walked + step >= dist || i == pts.Count - 1)
                {
                    double t = step < 1e-9 ? 0 : Math.Max(0, Math.Min(1, (dist - walked) / step));
                    return new Point3d(pts[i - 1].X + (pts[i].X - pts[i - 1].X) * t, pts[i - 1].Y + (pts[i].Y - pts[i - 1].Y) * t, 0);
                }
                walked += step;
            }
            return pts[pts.Count - 1];
        }

        public static bool QuetBinhDo(out ObjectId id, out double len, out bool estimated)
        {
            id = ObjectId.Null; len = 0; estimated = false;
            if (Db == null) return false;
            ObjectId alignId = ObjectId.Null, polyId = ObjectId.Null, fallbackId = ObjectId.Null;
            double alignLen = 0, polyLen = 0, fallbackLen = 0;
            bool alignGuess = false;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    if (tr.GetObject(eid, OpenMode.ForRead, false) is not Entity ent) continue;
                    string layer = (ent.Layer ?? "").ToUpperInvariant();
                    bool align = IsAlignment(ent);
                    bool onTim = layer.Contains("TIM") || layer == "TUYEN" || layer.Contains("CENTER");
                    if (!align && ent is not Polyline && ent is not Polyline2d && ent is not Polyline3d) continue;
                    if (IsProfile(ent) || IsSection(ent)) continue;
                    var sample = SampleEntity(ent);
                    double d = sample == null ? 0 : sample.Length;
                    bool guess = false;
                    if (d < 1 && align && TryExtents(ent, out Extents3d ext))
                    {
                        d = Math.Sqrt(Math.Pow(ext.MaxPoint.X - ext.MinPoint.X, 2) + Math.Pow(ext.MaxPoint.Y - ext.MinPoint.Y, 2));
                        guess = true;
                    }
                    if (d < 1) continue;
                    if (d > fallbackLen && ent is Curve) { fallbackLen = d; fallbackId = eid; }
                    if (align && d > alignLen) { alignLen = d; alignId = eid; alignGuess = guess; }
                    else if (!align && onTim && d > polyLen) { polyLen = d; polyId = eid; }
                }
                tr.Commit();
            }
            if (!alignId.IsNull) { id = alignId; len = alignLen; estimated = alignGuess; return true; }
            if (!polyId.IsNull) { id = polyId; len = polyLen; return true; }
            if (!fallbackId.IsNull) { id = fallbackId; len = fallbackLen; estimated = true; return true; }
            return false;
        }

        public static int QuetTracDocKm(out Extents3d? source)
        {
            int texts = 0, objects = 0;
            source = null;
            Extents3d? profile = null;
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var all = new List<Extents3d>();
                foreach (ObjectId eid in ms)
                {
                    var ent = tr.GetObject(eid, OpenMode.ForRead, false);
                    if (ent is Entity entity && IsProfile(entity) && TryExtents(entity, out Extents3d profileExt))
                    {
                        objects++;
                        profile = Union(profile, profileExt);
                    }
                    if (ent is Entity drawn && TryExtents(drawn, out Extents3d ext)) all.Add(ext);
                    string s = ent switch
                    {
                        DBText t => t.TextString,
                        MText m => m.Contents,
                        _ => null
                    };
                    if (s != null && StationRegex.IsMatch(s))
                    {
                        texts++;
                        if (ent is Entity marker && TryExtents(marker, out Extents3d markerExt))
                            source = Union(source, markerExt);
                    }
                }
                // TRACDOCTHIETKE/PLINETDTN are only the terrain/design curves.
                // Grow from those stable VNroad layers to include the table,
                // station labels and ordinates that belong to the same profile.
                if (objects > 0) source = GrowToNearbyGeometry(profile, all, 0.06, 2.50) ?? profile;
                else source = GrowToNearbyGeometry(source, all, 0.08, 2.50);
                tr.Commit();
            }
            // The number of matching polylines is not the number of profiles.
            return source != null ? 1 : texts;
        }

        public static int QuetTracNgang(out Extents3d? source, out List<Extents3d> items)
        {
            source = null;
            items = new List<Extents3d>();
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var all = new List<Extents3d>();
                var parts = new List<Extents3d>();
                var markers = new List<Extents3d>();
                foreach (ObjectId eid in ms)
                {
                    if (tr.GetObject(eid, OpenMode.ForRead, false) is not Entity entity) continue;
                    bool hasExt = TryExtents(entity, out Extents3d ext);
                    if (hasExt) all.Add(ext);
                    if (hasExt && HasXDataApp(entity, "KS_TN")) markers.Add(ext);
                    if (entity is BlockReference br)
                    {
                        string nm = EffectiveName(br).ToUpperInvariant();
                        if (nm.Contains("TN") || nm.Contains("TNCT") || nm.Contains("MATCAT") || nm.Contains("TRACNGANG"))
                        {
                            if (hasExt) items.Add(ext);
                            continue;
                        }
                    }
                    if (hasExt && IsSection(entity)) parts.Add(ext);
                }
                if (items.Count == 0 && markers.Count > 0)
                {
                    // VNroad 7.1 writes KS_TN on the representative polyline of
                    // each cross-section.  Use it as the primary seed instead of
                    // guessing from every line on PLINETNTN.
                    foreach (Extents3d marker in MergeOverlapping(markers))
                        items.Add(GrowToNearbyGeometry(marker, all, 0.10, 0.60) ?? marker);
                }
                else if (items.Count == 0 && parts.Count > 0) items = ClusterSections(parts);
                else
                {
                    var grown = new List<Extents3d>();
                    foreach (Extents3d marker in items)
                        grown.Add(GrowToNearbyGeometry(marker, all, 0.12, 0.20) ?? marker);
                    items = grown;
                }
                foreach (Extents3d item in items) source = Union(source, item);
                tr.Commit();
            }
            return items.Count;
        }

        static List<Extents3d> MergeOverlapping(IList<Extents3d> values)
        {
            var result = new List<Extents3d>();
            if (values == null) return result;
            foreach (Extents3d value in values.OrderByDescending(x => x.MaxPoint.Y).ThenBy(x => x.MinPoint.X))
            {
                int match = -1;
                Extents3d probe = Expand(value, 0.02, 0.05);
                for (int i = 0; i < result.Count; i++)
                {
                    if (!Intersects2d(Expand(result[i], 0.02, 0.05), probe)) continue;
                    match = i;
                    break;
                }
                if (match < 0) result.Add(value);
                else result[match] = Union(result[match], value).Value;
            }
            return result;
        }

        static List<Extents3d> ClusterSections(List<Extents3d> raw)
        {
            if (raw.Count == 0) return raw;
            Extents3d cloud = raw[0];
            foreach (Extents3d ext in raw) cloud = Union(cloud, ext).Value;
            double cloudW = Math.Max(1, cloud.MaxPoint.X - cloud.MinPoint.X);
            double cloudH = Math.Max(1, cloud.MaxPoint.Y - cloud.MinPoint.Y);
            var parts = raw.Where(ext => Width(ext) < cloudW * 0.55 && Height(ext) < cloudH * 0.55).ToList();
            if (parts.Count == 0) parts = raw;
            if (parts.Count == 1) return parts;
            var sizes = parts.Select(ext => Math.Max(Width(ext), Height(ext))).Where(v => v > 0.01).ToList();
            double unit = Math.Max(0.5, Median(sizes));
            List<Extents3d> best = null;
            int bestScore = int.MinValue;
            foreach (double factor in new[] { 3.0, 6, 10, 16, 24, 36 })
            {
                var clusters = SplitGrid(parts, Math.Max(1, unit * factor));
                if (clusters.Count == 0) continue;
                double avg = parts.Count / (double)clusters.Count;
                int score = Math.Min(clusters.Count, 120);
                if (clusters.Count >= 2 && clusters.Count <= 400) score += 80;
                if (avg >= 6) score += 40;
                if (avg >= 15) score += 20;
                if (score > bestScore) { bestScore = score; best = clusters; }
            }
            if (best == null || best.Count == 0)
            {
                best = new List<Extents3d>();
                Extents3d one = parts[0];
                foreach (Extents3d ext in parts) one = Union(one, ext).Value;
                best.Add(one);
            }
            return best.Select(ext => Expand(ext, 0.06, 0.10)).ToList();
        }

        static List<Extents3d> SplitGrid(List<Extents3d> parts, double gap)
        {
            var yCuts = Cuts(parts.Select(ext => (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0).ToList(), gap);
            var xCuts = Cuts(parts.Select(ext => (ext.MinPoint.X + ext.MaxPoint.X) / 2.0).ToList(), gap);
            int rows = yCuts.Count + 1, cols = xCuts.Count + 1;
            var bins = new Extents3d?[rows, cols];
            var counts = new int[rows, cols];
            foreach (Extents3d ext in parts)
            {
                int r = Band(yCuts, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0);
                int c = Band(xCuts, (ext.MinPoint.X + ext.MaxPoint.X) / 2.0);
                bins[r, c] = bins[r, c] == null ? ext : Union(bins[r, c], ext);
                counts[r, c]++;
            }
            var result = new List<Extents3d>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (counts[r, c] >= 4 && bins[r, c] != null) result.Add(bins[r, c].Value);
            return result;
        }

        static List<double> Cuts(List<double> coords, double minGap)
        {
            var cuts = new List<double>();
            if (coords.Count < 2) return cuts;
            coords.Sort();
            for (int i = 1; i < coords.Count; i++)
                if (coords[i] - coords[i - 1] > minGap) cuts.Add((coords[i] + coords[i - 1]) / 2.0);
            return cuts;
        }

        static int Band(List<double> cuts, double value)
        {
            int i = 0;
            while (i < cuts.Count && value > cuts[i]) i++;
            return i;
        }

        static double Width(Extents3d ext) => Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
        static double Height(Extents3d ext) => Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
        static double Median(List<double> values)
        {
            if (values.Count == 0) return 1;
            values.Sort();
            return values[values.Count / 2];
        }

        public static List<LayoutSheetInfo> ScanLayoutSheets()
        {
            var result = new List<LayoutSheetInfo>();
            if (Db == null) return result;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var dictionary = (DBDictionary)tr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry entry in dictionary)
                {
                    Match match = SheetLayoutRegex.Match(entry.Key);
                    if (!match.Success) continue;
                    var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                    var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                    ObjectId frameId = ObjectId.Null;
                    int bestPriority = 0;
                    double bestArea = 0;
                    foreach (ObjectId entityId in paper)
                    {
                        if (tr.GetObject(entityId, OpenMode.ForRead, false) is not BlockReference frame) continue;
                        int priority = FramePriority(frame, tr);
                        if (priority <= 0) continue;
                        double area = 0;
                        if (TryExtents(frame, out Extents3d ext))
                            area = Math.Abs((ext.MaxPoint.X - ext.MinPoint.X) * (ext.MaxPoint.Y - ext.MinPoint.Y));
                        if (priority < bestPriority || (priority == bestPriority && area <= bestArea)) continue;
                        bestPriority = priority;
                        bestArea = area;
                        frameId = entityId;
                    }
                    if (frameId.IsNull) continue;
                    result.Add(new LayoutSheetInfo
                    {
                        LayoutName = layout.LayoutName,
                        Type = match.Groups[1].Value.ToUpperInvariant(),
                        Index = int.Parse(match.Groups[2].Value),
                        LayoutId = entry.Value,
                        FrameId = frameId
                    });
                }
                tr.Commit();
            }
            int TypeOrder(string type) => type == "BD" ? 0 : type == "TD" ? 1 : 2;
            return result.OrderBy(x => TypeOrder(x.Type)).ThenBy(x => x.Index).ThenBy(x => x.LayoutName).ToList();
        }

        static bool TryExtents(Entity entity, out Extents3d extents)
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

        public static Extents3d Expand(Extents3d ext, double px, double py)
        {
            double dx = Math.Max(1.0, ext.MaxPoint.X - ext.MinPoint.X) * px;
            double dy = Math.Max(1.0, ext.MaxPoint.Y - ext.MinPoint.Y) * py;
            return new Extents3d(
                new Point3d(ext.MinPoint.X - dx, ext.MinPoint.Y - dy, ext.MinPoint.Z),
                new Point3d(ext.MaxPoint.X + dx, ext.MaxPoint.Y + dy, ext.MaxPoint.Z));
        }

        static Extents3d? GrowToNearbyGeometry(Extents3d? seed, List<Extents3d> all, double px, double py)
        {
            if (seed == null) return null;
            var window = Expand(seed.Value, px, py);
            Extents3d? result = null;
            foreach (var ext in all)
                if (Intersects2d(window, ext)) result = Union(result, ext);
            return result == null ? window : Expand(result.Value, 0.03, 0.05);
        }

        static bool Intersects2d(Extents3d a, Extents3d b) =>
            a.MinPoint.X <= b.MaxPoint.X && a.MaxPoint.X >= b.MinPoint.X
            && a.MinPoint.Y <= b.MaxPoint.Y && a.MaxPoint.Y >= b.MinPoint.Y;

        public static PromptEntityResult Pick(string msg)
        {
            return Ed.GetEntity(new PromptEntityOptions("\n" + msg) { AllowNone = false });
        }

        public static PromptPointResult PickPoint(string msg, string keyword)
        {
            var opt = new PromptPointOptions("\n" + msg) { AllowNone = true };
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                opt.Keywords.Add(keyword);
                opt.AppendKeywordsToMessage = true;
            }
            return Ed.GetPoint(opt);
        }

        public static List<ObjectId> KhungRai(string name)
        {
            var list = new List<(ObjectId id, double x, double y, double h)>();
            if (Db == null || string.IsNullOrEmpty(name)) return new List<ObjectId>();
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    if (tr.GetObject(eid, OpenMode.ForRead) is not BlockReference br) continue;
                    if (!string.Equals(EffectiveName(br), name, StringComparison.OrdinalIgnoreCase)) continue;
                    double h = 0;
                    try
                    {
                        var ext = br.GeometricExtents;
                        h = Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
                    }
                    catch { }
                    list.Add((eid, br.Position.X, br.Position.Y, h));
                }
                tr.Commit();
            }

            if (list.Count < 2) return list.Select(a => a.id).ToList();
            var heights = list.Where(a => a.h > 1e-6).Select(a => a.h).OrderBy(h => h).ToList();
            double medianHeight = heights.Count == 0 ? 1.0 : heights[heights.Count / 2];
            double tolerance = Math.Max(1e-6, medianHeight * 0.45);
            var rows = new List<List<(ObjectId id, double x, double y, double h)>>();

            foreach (var item in list.OrderByDescending(a => a.y))
            {
                var row = rows.FirstOrDefault(r =>
                    Math.Abs(item.y - r.Average(a => a.y)) <= Math.Max(tolerance, item.h * 0.45));
                if (row == null)
                {
                    row = new List<(ObjectId id, double x, double y, double h)>();
                    rows.Add(row);
                }
                row.Add(item);
            }

            return rows.OrderByDescending(r => r.Average(a => a.y))
                .SelectMany(r => r.OrderBy(a => a.x).ThenBy(a => a.id.Handle.Value))
                .Select(a => a.id)
                .ToList();
        }

        public static List<string> Tags(ObjectId id)
        {
            var tags = new List<string>();
            if (id.IsNull || id.Database == null || !IsCurrentDatabase(id)) return tags;
            using (Doc.LockDocument())
            using (var tr = id.Database.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(id, OpenMode.ForRead, false);
                if (obj is BlockReference br && br.AttributeCollection != null)
                {
                    foreach (ObjectId aid in br.AttributeCollection)
                        if (tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)
                            tags.Add(ar.Tag);
                }
                else if (obj is BlockTableRecord btr)
                {
                    foreach (ObjectId eid in btr)
                        if (tr.GetObject(eid, OpenMode.ForRead, false) is AttributeDefinition ad && !ad.Constant)
                            tags.Add(ad.Tag);
                }
                tr.Commit();
            }
            return tags;
        }

        public static string BlockName(ObjectId id)
        {
            if (Db == null || !IsCurrentDatabase(id)) return null;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                string name = tr.GetObject(id, OpenMode.ForRead, false) is BlockReference block
                    ? EffectiveName(block)
                    : null;
                tr.Commit();
                return name;
            }
        }

        public static int GanAttrs(IEnumerable<KeyValuePair<ObjectId, Dictionary<string, string>>> updates)
        {
            if (Db == null || updates == null) return 0;
            var batch = updates.Where(x => !x.Key.IsNull
                                           && IsCurrentDatabase(x.Key)
                                           && x.Value != null
                                           && x.Value.Count > 0)
                               .ToList();
            if (batch.Count == 0) return 0;
            int changed = 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                foreach (var update in batch)
                {
                    if (tr.GetObject(update.Key, OpenMode.ForWrite, false) is not BlockReference br) continue;
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        if (tr.GetObject(aid, OpenMode.ForWrite) is AttributeReference ar
                            && update.Value.TryGetValue(ar.Tag, out string value))
                        {
                            ar.TextString = value;
                            changed++;
                        }
                    }
                }
                tr.Commit();
            }
            return changed;
        }

        public static Extents3d? BBox(ObjectId id)
        {
            if (Db == null || !IsCurrentDatabase(id)) return null;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Entity e)
                {
                    try { var x = e.GeometricExtents; tr.Commit(); return x; }
                    catch { }
                }
                tr.Commit();
            }
            return null;
        }

        public static double MillimetersToDrawingUnits(double millimeters)
        {
            if (Db == null || millimeters <= 0) return 0;
            UnitsValue target = Db.Insunits;
            if (target == UnitsValue.Undefined) return millimeters;
            return millimeters / MillimetersPerDrawingUnit(target);
        }

        public static double MetersToDrawingUnits(double meters)
        {
            if (Db == null || meters <= 0) return 0;
            UnitsValue target = Db.Insunits;
            if (target == UnitsValue.Undefined) return meters;
            return meters * 1000.0 / MillimetersPerDrawingUnit(target);
        }

        public static double DrawingUnitsToMeters(double drawingUnits)
        {
            if (Db == null || drawingUnits <= 0) return 0;
            UnitsValue source = Db.Insunits;
            if (source == UnitsValue.Undefined) return drawingUnits;
            return drawingUnits * MillimetersPerDrawingUnit(source) / 1000.0;
        }

        static double MillimetersPerDrawingUnit(UnitsValue units)
        {
            switch (units.ToString())
            {
                case "Microinches": return 0.0000254;
                case "Mils": return 0.0254;
                case "Inches": return 25.4;
                case "Feet": return 304.8;
                case "Yards": return 914.4;
                case "Miles": return 1609344.0;
                case "Microns": return 0.001;
                case "Centimeters": return 10.0;
                case "Decimeters": return 100.0;
                case "Meters": return 1000.0;
                case "Dekameters": return 10000.0;
                case "Hectometers": return 100000.0;
                case "Kilometers": return 1000000.0;
                default: return 1.0;
            }
        }

        public static ObjectId ImportTemplateFrame(string file, out string frameName, out string error)
        {
            frameName = null;
            error = null;
            if (Db == null || string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                error = "File khung mẫu không tồn tại.";
                return ObjectId.Null;
            }

            try
            {
                using var source = new Database(false, true);
                source.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, false, null);
                try
                {
                    var previous = HostApplicationServices.WorkingDatabase;
                    try
                    {
                        HostApplicationServices.WorkingDatabase = source;
                        source.ResolveXrefs(false, false);
                    }
                    finally { HostApplicationServices.WorkingDatabase = previous; }
                }
                catch { }
                ObjectId sourceDefinition = ObjectId.Null;
                Scale3d scale = new Scale3d(1);
                double rotation = 0;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var sourceTransaction = source.TransactionManager.StartTransaction())
                {
                    var found = new Dictionary<string, FrameInfo>(StringComparer.OrdinalIgnoreCase);
                    RememberDefinitions(source, sourceTransaction, found);
                    RememberInserts(source, sourceTransaction, found);
                    FrameInfo best = found.Values.OrderByDescending(x => x.Priority).FirstOrDefault();
                    if (best != null)
                    {
                        frameName = best.Name;
                        sourceDefinition = best.Definition;
                        if (!best.Sample.IsNull
                            && sourceTransaction.GetObject(best.Sample, OpenMode.ForRead, false) is BlockReference candidate)
                        {
                            if (sourceDefinition.IsNull)
                                sourceDefinition = candidate.IsDynamicBlock ? candidate.DynamicBlockTableRecord : candidate.BlockTableRecord;
                            scale = candidate.ScaleFactors;
                            rotation = candidate.Rotation;
                            foreach (ObjectId attributeId in candidate.AttributeCollection)
                                if (sourceTransaction.GetObject(attributeId, OpenMode.ForRead, false) is AttributeReference attribute)
                                    values[attribute.Tag] = attribute.TextString;
                        }
                    }
                    sourceTransaction.Commit();
                }
                if (sourceDefinition.IsNull)
                {
                    error = "Không thấy block khung. Cần block tên chứa KHUNG (KHUNGIN, KHUNG CHINH) hoặc thẻ STT, TENBV, SBV, TYLE. File chỉ có nét thì chưa dùng được.";
                    return ObjectId.Null;
                }

                var ids = new ObjectIdCollection { sourceDefinition };
                var mapping = new IdMapping();
                using (Doc.LockDocument())
                {
                    source.WblockCloneObjects(ids, Db.BlockTableId, mapping, DuplicateRecordCloning.MangleName, false);
                    ObjectId definitionId = mapping[sourceDefinition].Value;
                    using (var tr = Db.TransactionManager.StartTransaction())
                    {
                        var table = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                        var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        ObjectId layerId = EnsureLayer("GKIN-TEMPLATE", tr, false);
                        var frame = new BlockReference(Point3d.Origin, definitionId)
                        {
                            ScaleFactors = scale,
                            Rotation = rotation,
                            LayerId = layerId,
                            Visible = false
                        };
                        model.AppendEntity(frame);
                        tr.AddNewlyCreatedDBObject(frame, true);
                        AddAttributes(frame, definitionId, values, tr);
                        tr.Commit();
                        return frame.ObjectId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return ObjectId.Null;
            }
        }

        public static void DeleteEntity(ObjectId id)
        {
            if (!IsCurrentDatabase(id)) return;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(id, OpenMode.ForWrite, false) is DBObject value && !value.IsErased) value.Erase();
                tr.Commit();
            }
        }

        public static ObjectId InsertFrameInstance(ObjectId definitionId)
        {
            if (Db == null || definitionId.IsNull || !IsCurrentDatabase(definitionId)) return ObjectId.Null;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId layerId = EnsureLayer("GKIN-TEMPLATE", tr, false);
                var frame = new BlockReference(Point3d.Origin, definitionId)
                {
                    LayerId = layerId,
                    Visible = false
                };
                model.AppendEntity(frame);
                tr.AddNewlyCreatedDBObject(frame, true);
                AddAttributes(frame, definitionId, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), tr);
                tr.Commit();
                return frame.ObjectId;
            }
        }

        public static List<ObjectId> CreateModelSheets(
            ObjectId sampleFrame, Extents3d? bd, Extents3d? td, IList<Extents3d> tnItems,
            int tdCount, double tdLength, double tdStep, bool mergeBdTd, bool verticalTn, bool rowLayout,
            string layerName, double overlap, int sheetsPerRow, bool hideCopiedGeometry, Point3d? origin,
            ObjectId bdCurve, int bdPerSheet, int tnPerSheet,
            out int tdCreated, out string error)
        {
            error = null;
            tdCreated = 0;
            var result = new List<ObjectId>();
            if (Db == null || !IsCurrentDatabase(sampleFrame))
            {
                error = "Khung mẫu không thuộc bản vẽ đang mở.";
                return result;
            }

            var plans = BuildSheetPlans(bdCurve, bd, td, tnItems, tdCount, tdLength, tdStep, mergeBdTd, verticalTn, bdPerSheet, tnPerSheet);
            if (plans.Count == 0)
            {
                error = "Không tìm thấy vùng nguồn bình đồ, trắc dọc hoặc trắc ngang.";
                return result;
            }

            int skipped = 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var sample = (BlockReference)tr.GetObject(sampleFrame, OpenMode.ForRead);
                    ObjectId definitionId = sample.IsDynamicBlock ? sample.DynamicBlockTableRecord : sample.BlockTableRecord;
                    ObjectId layerId = EnsureLayer(layerName, tr);
                    ObjectId hiddenLayerId = hideCopiedGeometry ? EnsureLayer("GKIN-NONPLOT", tr, false) : ObjectId.Null;

                    if (!TrySampleExtents(sample, tr, out Extents3d sampleExt))
                    {
                        error = "Khung mẫu không có kích thước. Block rỗng hoặc xref chưa nạp.";
                        return result;
                    }
                    double frameWidth = Math.Max(1.0, sampleExt.MaxPoint.X - sampleExt.MinPoint.X);
                    double frameHeight = Math.Max(1.0, sampleExt.MaxPoint.Y - sampleExt.MinPoint.Y);
                    Extents3d? drawingExt = null;
                    foreach (ObjectId id in model)
                        if (tr.GetObject(id, OpenMode.ForRead, false) is Entity entity && TryExtents(entity, out Extents3d ext)) drawingExt = Union(drawingExt, ext);

                    double startX = origin.HasValue ? origin.Value.X : (drawingExt.HasValue ? drawingExt.Value.MaxPoint.X + frameWidth * 0.30 : frameWidth * 0.30);
                    double startY = origin.HasValue ? origin.Value.Y : (drawingExt.HasValue ? drawingExt.Value.MaxPoint.Y : 0);
                    int perRow = rowLayout ? Math.Max(1, sheetsPerRow) : Math.Max(1, plans.Count);
                    double gapX = frameWidth * 0.10;
                    double gapY = frameHeight * 0.15;
                    string sampleName = EffectiveName(sample);

                    for (int index = 0; index < plans.Count; index++)
                    {
                        int column = index % perRow;
                        int row = index / perRow;
                        var targetMin = new Point3d(startX + column * (frameWidth + gapX), startY - row * (frameHeight + gapY), 0);
                        var frame = new BlockReference(sample.Position, definitionId)
                        {
                            ScaleFactors = sample.ScaleFactors,
                            Rotation = sample.Rotation,
                            LayerId = layerId
                        };
                        model.AppendEntity(frame);
                        tr.AddNewlyCreatedDBObject(frame, true);
                        frame.TransformBy(Matrix3d.Displacement(targetMin - sampleExt.MinPoint));
                        AddAttributes(frame, sample, definitionId, tr);
                        result.Add(frame.ObjectId);
                        if (plans[index].Type == "TD") tdCreated++;

                        Extents3d targetFrame = TryExtents(frame, out Extents3d placed) ? placed : new Extents3d(targetMin, targetMin + new Vector3d(frameWidth, frameHeight, 0));
                        double usableWidth = frameWidth * 0.76;
                        double usableHeight = frameHeight * 0.88;
                        double left = targetFrame.MinPoint.X + frameWidth * 0.04;
                        double bottom = targetFrame.MinPoint.Y + frameHeight * 0.06;
                        int windowCount = Math.Max(1, plans[index].Windows.Count);
                        bool horizontal = !plans[index].StackVertical && windowCount > 1;
                        double targetWidth = horizontal ? usableWidth / windowCount : usableWidth;
                        double targetHeight = horizontal ? usableHeight : usableHeight / windowCount;

                        for (int windowIndex = 0; windowIndex < plans[index].Windows.Count; windowIndex++)
                        {
                            var sourceWindow = plans[index].Windows[windowIndex];
                            if (overlap > 0)
                            {
                                sourceWindow = new Extents3d(
                                    new Point3d(sourceWindow.MinPoint.X - overlap, sourceWindow.MinPoint.Y - overlap, sourceWindow.MinPoint.Z),
                                    new Point3d(sourceWindow.MaxPoint.X + overlap, sourceWindow.MaxPoint.Y + overlap, sourceWindow.MaxPoint.Z));
                            }
                            double sourceWidth = Math.Max(1e-6, sourceWindow.MaxPoint.X - sourceWindow.MinPoint.X);
                            double sourceHeight = Math.Max(1e-6, sourceWindow.MaxPoint.Y - sourceWindow.MinPoint.Y);
                            double scale = Math.Min(targetWidth * 0.94 / sourceWidth, targetHeight * 0.94 / sourceHeight);
                            var sourceCenter = new Point3d((sourceWindow.MinPoint.X + sourceWindow.MaxPoint.X) / 2.0, (sourceWindow.MinPoint.Y + sourceWindow.MaxPoint.Y) / 2.0, 0);
                            var targetCenter = horizontal
                                ? new Point3d(left + targetWidth * (windowIndex + 0.5), bottom + usableHeight / 2.0, 0)
                                : new Point3d(left + usableWidth / 2.0, bottom + targetHeight * (windowIndex + 0.5), 0);
                            var transform = Matrix3d.Displacement(targetCenter - Point3d.Origin)
                                * Matrix3d.Scaling(scale, Point3d.Origin)
                                * Matrix3d.Displacement(Point3d.Origin - sourceCenter);

                            var sourceIds = model.Cast<ObjectId>().ToList();
                            foreach (ObjectId sourceId in sourceIds)
                            {
                                if (sourceId == sampleFrame || result.Contains(sourceId)) continue;
                                try
                                {
                                    if (tr.GetObject(sourceId, OpenMode.ForRead, false) is not Entity source || !TryExtents(source, out Extents3d sourceExt) || !Intersects2d(sourceWindow, sourceExt)) continue;
                                    if (source is Viewport || source is AttributeDefinition) continue;
                                    if (source is BlockReference block)
                                    {
                                        if (string.Equals(EffectiveName(block), sampleName, StringComparison.OrdinalIgnoreCase)) continue;
                                        var owner = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                                        if (owner.IsFromExternalReference && (owner.Name ?? "").IndexOf('|') < 0) continue;
                                    }
                                    if (source.Clone() is not Entity clone) continue;
                                    clone.TransformBy(transform);
                                    if (!hiddenLayerId.IsNull) clone.LayerId = hiddenLayerId;
                                    model.AppendEntity(clone);
                                    tr.AddNewlyCreatedDBObject(clone, true);
                                }
                                catch { skipped++; }
                            }
                        }
                    }
                    tr.Commit();
                    Db.TransactionManager.QueueForGraphicsFlush();
                    if (skipped > 0)
                        error = "Đã bỏ qua " + skipped + " đối tượng không sao chép được (xref, viewport hoặc proxy).";
                    LastFrames = new List<ObjectId>(result);
                    LastTypes = plans.Take(result.Count).Select(p => p.Type).ToList();
                }
                catch (System.Exception ex)
                {
                    result.Clear();
                    tdCreated = 0;
                    error = Describe(ex);
                }
            }
            return result;
        }

        static bool TrySampleExtents(BlockReference sample, Transaction tr, out Extents3d ext)
        {
            if (TryExtents(sample, out ext))
            {
                double w = ext.MaxPoint.X - ext.MinPoint.X;
                double h = ext.MaxPoint.Y - ext.MinPoint.Y;
                if (w > 1 && h > 1) return true;
            }
            try
            {
                var def = (BlockTableRecord)tr.GetObject(sample.IsDynamicBlock ? sample.DynamicBlockTableRecord : sample.BlockTableRecord, OpenMode.ForRead);
                Extents3d? acc = null;
                foreach (ObjectId id in def)
                    if (tr.GetObject(id, OpenMode.ForRead, false) is Entity e && e is not AttributeDefinition && TryExtents(e, out Extents3d ee))
                        acc = Union(acc, ee);
                if (acc == null) return false;
                ext = TransformExtents(acc.Value, sample.BlockTransform);
                return ext.MaxPoint.X - ext.MinPoint.X > 1 && ext.MaxPoint.Y - ext.MinPoint.Y > 1;
            }
            catch
            {
                ext = default;
                return false;
            }
        }

        static Extents3d TransformExtents(Extents3d source, Matrix3d matrix)
        {
            Point3d[] corners =
            {
                new Point3d(source.MinPoint.X, source.MinPoint.Y, 0),
                new Point3d(source.MaxPoint.X, source.MinPoint.Y, 0),
                new Point3d(source.MinPoint.X, source.MaxPoint.Y, 0),
                new Point3d(source.MaxPoint.X, source.MaxPoint.Y, 0)
            };
            var result = new Extents3d(corners[0].TransformBy(matrix), corners[0].TransformBy(matrix));
            for (int i = 1; i < corners.Length; i++) result.AddPoint(corners[i].TransformBy(matrix));
            return result;
        }

        static string Describe(System.Exception ex)
        {
            if (ex is Autodesk.AutoCAD.Runtime.Exception acad)
            {
                if (acad.ErrorStatus == ErrorStatus.NotApplicable)
                    return "eNotApplicable: khung hoặc đối tượng nguồn không dùng được (xref chưa nạp, block rỗng, viewport).";
                return acad.ErrorStatus.ToString();
            }
            return ex.Message;
        }

        static ObjectId EnsureLayer(string requestedName, Transaction tr, bool? isPlottable = null)
        {
            string name = string.IsNullOrWhiteSpace(requestedName) ? "GKIN-KHUNG" : requestedName.Trim();
            var table = (LayerTable)tr.GetObject(Db.LayerTableId, OpenMode.ForRead);
            if (table.Has(name))
            {
                ObjectId existingId = table[name];
                if (isPlottable != null)
                {
                    var existing = (LayerTableRecord)tr.GetObject(existingId, OpenMode.ForWrite);
                    existing.IsPlottable = isPlottable.Value;
                }
                return existingId;
            }
            table.UpgradeOpen();
            var record = new LayerTableRecord { Name = name };
            if (isPlottable != null) record.IsPlottable = isPlottable.Value;
            ObjectId id = table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
            return id;
        }

        public static List<LayoutSheetInfo> CreateFrontMatter(
            ObjectId sampleFrame, bool includeCover, bool includeIndex, IList<string> entries, out string error)
        {
            error = null;
            var result = new List<LayoutSheetInfo>();
            if ((!includeCover && !includeIndex) || Db == null) return result;
            if (!IsCurrentDatabase(sampleFrame))
            {
                error = "Chưa có khung mẫu để tạo bìa hoặc mục lục.";
                return result;
            }

            var pages = new List<(string type, string title, List<string> lines)>();
            if (includeCover)
                pages.Add(("BIA", "HỒ SƠ BẢN VẼ", new List<string> { "BÌNH ĐỒ — TRẮC DỌC — TRẮC NGANG", "Tạo bởi GKIN" }));
            if (includeIndex)
            {
                var source = entries ?? new List<string>();
                int pageCount = Math.Max(1, (int)Math.Ceiling(source.Count / 18.0));
                for (int page = 0; page < pageCount; page++)
                    pages.Add(("MUC", pageCount == 1 ? "MỤC LỤC BẢN VẼ" : $"MỤC LỤC BẢN VẼ ({page + 1}/{pageCount})", source.Skip(page * 18).Take(18).ToList()));
            }

            string originalLayout = LayoutManager.Current.CurrentLayout;
            using (Doc.LockDocument())
            {
                try
                {
                    foreach (var page in pages)
                    {
                        string layoutName = null;
                        try
                        {
                            layoutName = UniqueLayoutName("GKIN-" + page.type);
                            ObjectId layoutId = LayoutManager.Current.CreateLayout(layoutName);
                            LayoutManager.Current.CurrentLayout = layoutName;
                            using (var tr = Db.TransactionManager.StartTransaction())
                            {
                                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                                var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                                foreach (ObjectId id in paper)
                                    if (tr.GetObject(id, OpenMode.ForWrite, false) is Viewport viewport && viewport.Number > 1) viewport.Erase();

                                var sample = (BlockReference)tr.GetObject(sampleFrame, OpenMode.ForRead);
                                ObjectId definitionId = sample.IsDynamicBlock ? sample.DynamicBlockTableRecord : sample.BlockTableRecord;
                                var frame = new BlockReference(Point3d.Origin, definitionId) { ScaleFactors = sample.ScaleFactors, Rotation = sample.Rotation };
                                paper.AppendEntity(frame); tr.AddNewlyCreatedDBObject(frame, true);
                                var ext = frame.GeometricExtents;
                                frame.TransformBy(Matrix3d.Displacement(Point3d.Origin - ext.MinPoint));
                                AddAttributes(frame, sample, definitionId, tr);
                                ext = frame.GeometricExtents;
                                using (var settings = BuildPlotSettings(layout, null,
                                    new Extents2d(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y),
                                    "DWG To PDF.pc3", null))
                                    layout.CopyFrom(settings);
                                double width = Math.Max(1.0, ext.MaxPoint.X - ext.MinPoint.X);
                                double height = Math.Max(1.0, ext.MaxPoint.Y - ext.MinPoint.Y);
                                AddPaperText(paper, tr, page.title, new Point3d(width * 0.10, height * 0.76, 0), height * 0.045, width * 0.78);
                                for (int i = 0; i < page.lines.Count; i++)
                                    AddPaperText(paper, tr, page.lines[i], new Point3d(width * 0.11, height * (0.68 - i * 0.032), 0), height * 0.020, width * 0.76);

                                var sheet = new LayoutSheetInfo { LayoutName = layoutName, LayoutId = layoutId, FrameId = frame.ObjectId, Type = page.type, Index = result.Count + 1 };
                                tr.Commit();
                                result.Add(sheet);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            error = ex.Message;
                            if (!string.IsNullOrWhiteSpace(layoutName))
                            {
                                try
                                {
                                    if (string.Equals(LayoutManager.Current.CurrentLayout, layoutName, StringComparison.OrdinalIgnoreCase))
                                        LayoutManager.Current.CurrentLayout = originalLayout;
                                    LayoutManager.Current.DeleteLayout(layoutName);
                                }
                                catch { }
                            }
                            break;
                        }
                    }
                }
                finally
                {
                    try { LayoutManager.Current.CurrentLayout = originalLayout; }
                    catch { }
                }
            }
            return result;
        }

        public static void DeleteLayouts(IEnumerable<string> layoutNames)
        {
            if (Db == null || layoutNames == null) return;
            string current = LayoutManager.Current.CurrentLayout;
            using (Doc.LockDocument())
            {
                foreach (string name in layoutNames.Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, current, StringComparison.OrdinalIgnoreCase)))
                {
                    try { LayoutManager.Current.DeleteLayout(name); }
                    catch { }
                }
            }
        }

        static void AddPaperText(BlockTableRecord paper, Transaction tr, string text, Point3d position, double height, double width)
        {
            var entity = new MText
            {
                Contents = text ?? "",
                Location = position,
                TextHeight = Math.Max(1.0, height),
                Attachment = AttachmentPoint.BottomLeft,
                Width = Math.Max(10.0, width),
                Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 0, 0)
            };
            paper.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
        }

        public static List<LayoutSheetInfo> CreateLayouts(
            ObjectId sampleFrame, Extents3d? bd, Extents3d? td, IList<Extents3d> tnItems,
            int tdCount, double tdLength, double tdStep, bool mergeBdTd, bool verticalTn,
            ObjectId bdCurve, int bdPerSheet, int tnPerSheet, out string error)
        {
            error = null;
            var result = new List<LayoutSheetInfo>();
            if (Db == null || !IsCurrentDatabase(sampleFrame))
            {
                error = "Khung mẫu không thuộc bản vẽ đang mở.";
                return result;
            }

            var plans = BuildSheetPlans(bdCurve, bd, td, tnItems, tdCount, tdLength, tdStep, mergeBdTd, verticalTn, bdPerSheet, tnPerSheet);
            if (plans.Count == 0)
            {
                error = "Không tìm thấy vùng nguồn BĐ/TĐ/TN để tạo Layout.";
                return result;
            }

            string originalLayout = LayoutManager.Current.CurrentLayout;
            using (Doc.LockDocument())
            {
                try
                {
                    foreach (var plan in plans)
                    {
                        string layoutName = null;
                        try
                        {
                            layoutName = UniqueLayoutName($"GKIN-{plan.Type}-{Pad(plan.Index, 2)}");
                            ObjectId layoutId = LayoutManager.Current.CreateLayout(layoutName);
                            LayoutManager.Current.CurrentLayout = layoutName;
                            var viewportIds = new List<ObjectId>();
                            ObjectId frameId;
                            using (var tr = Db.TransactionManager.StartTransaction())
                            {
                                var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                                var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                                foreach (ObjectId id in paper)
                                {
                                    if (tr.GetObject(id, OpenMode.ForWrite, false) is Viewport oldViewport
                                        && oldViewport.Number > 1)
                                        oldViewport.Erase();
                                }

                                var sample = (BlockReference)tr.GetObject(sampleFrame, OpenMode.ForRead);
                                ObjectId definitionId = sample.IsDynamicBlock
                                    ? sample.DynamicBlockTableRecord
                                    : sample.BlockTableRecord;
                                var frame = new BlockReference(Point3d.Origin, definitionId)
                                {
                                    ScaleFactors = sample.ScaleFactors,
                                    Rotation = sample.Rotation
                                };
                                paper.AppendEntity(frame);
                                tr.AddNewlyCreatedDBObject(frame, true);
                                if (!TrySampleExtents(frame, tr, out Extents3d frameExt))
                                    throw new InvalidOperationException("Khung mẫu không có kích thước.");
                                frame.TransformBy(Matrix3d.Displacement(Point3d.Origin - frameExt.MinPoint));
                                AddAttributes(frame, sample, definitionId, tr);
                                if (!TrySampleExtents(frame, tr, out frameExt))
                                    frameExt = new Extents3d(Point3d.Origin, new Point3d(420, 297, 0));
                                frameId = frame.ObjectId;

                                double frameWidth = Math.Max(1.0, frameExt.MaxPoint.X - frameExt.MinPoint.X);
                                double frameHeight = Math.Max(1.0, frameExt.MaxPoint.Y - frameExt.MinPoint.Y);
                                double marginL = 0.03, marginR = 0.03, marginT = 0.04, marginB = 0.20;
                                double usableWidth = frameWidth * (1 - marginL - marginR);
                                double usableHeight = frameHeight * (1 - marginT - marginB);
                                double left = frameExt.MinPoint.X + frameWidth * marginL;
                                double bottom = frameExt.MinPoint.Y + frameHeight * marginB;
                                int windowCount = Math.Max(1, plan.Windows.Count);
                                int slots = Math.Max(plan.Slots, windowCount);
                                bool horizontal = !plan.StackVertical && windowCount > 1;
                                double viewportWidth = horizontal ? usableWidth / windowCount * 0.96 : usableWidth * 0.98;
                                double viewportHeight = horizontal
                                    ? usableHeight * 0.96
                                    : (plan.Type == "BD" ? usableHeight / slots : windowCount > 1 ? usableHeight / windowCount : usableHeight) * 0.94;
                                double usedHeight = horizontal ? viewportHeight : viewportHeight * windowCount;
                                double yBase = bottom + Math.Max(0, usableHeight - usedHeight) / 2.0;

                                for (int i = 0; i < plan.Windows.Count; i++)
                                {
                                    var source = plan.Windows[i];
                                    double centerX = horizontal
                                        ? left + usableWidth / windowCount * (i + 0.5)
                                        : left + usableWidth / 2.0;
                                    double centerY = horizontal
                                        ? yBase + viewportHeight / 2.0
                                        : yBase + viewportHeight * (windowCount - 1 - i) + viewportHeight / 2.0;
                                    Point3d look = i < plan.Look.Count
                                        ? plan.Look[i]
                                        : new Point3d((source.MinPoint.X + source.MaxPoint.X) / 2.0, (source.MinPoint.Y + source.MaxPoint.Y) / 2.0, 0);
                                    double twist = i < plan.Twist.Count ? plan.Twist[i] : 0;
                                    double span = i < plan.Span.Count ? plan.Span[i] : 0;
                                    var viewport = LayoutViewportService.Create(
                                        paper, tr, new Point3d(centerX, centerY, 0), viewportWidth, viewportHeight,
                                        source, look, twist, span);
                                    viewportIds.Add(viewport.ObjectId);
                                }

                                using (var settings = BuildPlotSettings(layout, null,
                                    new Extents2d(frameExt.MinPoint.X, frameExt.MinPoint.Y, frameExt.MaxPoint.X, frameExt.MaxPoint.Y),
                                    "DWG To PDF.pc3", null))
                                    layout.CopyFrom(settings);
                                tr.Commit();
                            }

                            // A newly appended viewport must be committed before it is enabled and locked.
                            LayoutManager.Current.CurrentLayout = layoutName;
                            ActivateViewports(viewportIds, layoutName);
                            result.Add(new LayoutSheetInfo
                            {
                                LayoutName = layoutName,
                                Type = plan.Type,
                                Index = plan.Index,
                                LayoutId = layoutId,
                                FrameId = frameId
                            });
                        }
                        catch (System.Exception ex)
                        {
                            error = $"{layoutName ?? plan.Type}: {ex.Message}";
                            if (!string.IsNullOrWhiteSpace(layoutName))
                            {
                                try
                                {
                                    if (string.Equals(LayoutManager.Current.CurrentLayout, layoutName, StringComparison.OrdinalIgnoreCase))
                                        LayoutManager.Current.CurrentLayout = originalLayout;
                                    LayoutManager.Current.DeleteLayout(layoutName);
                                }
                                catch { }
                            }
                            break;
                        }
                    }
                    Db.TransactionManager.QueueForGraphicsFlush();
                }
                catch (System.Exception ex)
                {
                    error = ex.Message;
                }
                finally
                {
                    try { LayoutManager.Current.CurrentLayout = originalLayout; }
                    catch { }
                }
            }
            LastFrames = result.Select(x => x.FrameId).ToList();
            LastTypes = result.Select(x => x.Type).ToList();
            return result;
        }

        static void ActivateViewports(IList<ObjectId> viewportIds, string layoutName)
        {
            void Activate()
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in viewportIds)
                    {
                        var viewport = (Viewport)tr.GetObject(id, OpenMode.ForWrite);
                        viewport.On = true;
                        viewport.Locked = true;
                        viewport.UpdateDisplay();
                    }
                    tr.Commit();
                }
            }

            try { Activate(); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == ErrorStatus.NotApplicable)
            {
                LayoutManager.Current.CurrentLayout = layoutName;
                Ed.Regen();
                Activate();
            }
        }

        static List<SheetPlan> BuildSheetPlans(ObjectId bdCurve, Extents3d? bd, Extents3d? td, IList<Extents3d> tnItems,
            int tdCount, double tdLength, double tdStep, bool mergeBdTd, bool verticalTn, int bdPerSheet, int tnPerSheet)
        {
            var plans = new List<SheetPlan>();
            if (bd != null && !(mergeBdTd && td != null))
            {
                var strips = CutAlignment(bdCurve, bd.Value, tdStep);
                int per = Math.Max(1, bdPerSheet);
                for (int i = 0; i < strips.Count; i += per)
                {
                    var plan = new SheetPlan { Type = "BD", Index = i / per + 1, StackVertical = true, Slots = per };
                    foreach (var strip in strips.Skip(i).Take(per))
                    {
                        plan.Windows.Add(strip.Ext);
                        plan.Twist.Add(strip.Twist);
                        plan.Look.Add(strip.Look);
                        plan.Span.Add(strip.Span);
                    }
                    plans.Add(plan);
                }
            }
            if (td != null)
            {
                var bands = ProfileCutterService.Cut(td.Value, tdLength, tdStep, Math.Max(1, tdCount));
                for (int i = 0; i < bands.Count; i++)
                {
                    var band = bands[i];
                    var plan = new SheetPlan
                    {
                        Type = "TD",
                        Index = i + 1,
                        StackVertical = false,
                        Windows = new List<Extents3d>(band.Windows),
                        Look = new List<Point3d>(band.Look),
                        Span = new List<double>(band.Span),
                        Twist = new List<double>(band.Twist),
                        Slots = Math.Max(1, band.Windows.Count)
                    };
                    if (mergeBdTd && bd != null && i == 0)
                    {
                        plan.Windows.Insert(0, bd.Value);
                        plan.Look.Insert(0, new Point3d(
                            (bd.Value.MinPoint.X + bd.Value.MaxPoint.X) / 2.0,
                            (bd.Value.MinPoint.Y + bd.Value.MaxPoint.Y) / 2.0, 0));
                        plan.Span.Insert(0, 0);
                        plan.Twist.Insert(0, 0);
                        plan.Slots = plan.Windows.Count;
                    }
                    plans.Add(plan);
                }
            }
            if (tnItems != null && tnItems.Count > 0)
            {
                int per = Math.Max(1, tnPerSheet);
                foreach (var sheet in CrossSectionPackerService.Pack(tnItems, per, verticalTn))
                    plans.Add(new SheetPlan
                    {
                        Type = "TN",
                        Index = sheet.Index,
                        StackVertical = sheet.StackVertical,
                        Slots = per,
                        Windows = sheet.Windows
                    });
            }
            return plans;
        }

        static List<Strip> CutAlignment(ObjectId curveId, Extents3d full, double step)
        {
            var whole = new Strip
            {
                Ext = full,
                Look = new Point3d((full.MinPoint.X + full.MaxPoint.X) / 2.0, (full.MinPoint.Y + full.MaxPoint.Y) / 2.0, 0),
                Twist = 0,
                Span = Math.Max(1, full.MaxPoint.X - full.MinPoint.X)
            };
            if (step <= 1 || curveId.IsNull || curveId.Database == null) return new List<Strip> { whole };
            try
            {
                using (var tr = curveId.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    if (tr.GetObject(curveId, OpenMode.ForRead, false) is not Entity ent) return new List<Strip> { whole };
                    PathSample path = SampleEntity(ent);
                    if (path == null || path.Length < 1 || path.Points == null || path.Points.Count < 2) return new List<Strip> { whole };
                    double len = path.Length;
                    var list = new List<Strip>();
                    int count = Math.Max(1, (int)Math.Ceiling(len / step));
                    for (int i = 0; i < count; i++)
                    {
                        double d0 = Math.Min(len, i * step);
                        double d1 = Math.Min(len, (i + 1) * step);
                        if (d1 - d0 < 1) continue;
                        var p0 = At(path, d0);
                        var p1 = At(path, d1);
                        var mid = At(path, (d0 + d1) / 2.0);
                        var ext = new Extents3d(p0, p0);
                        for (int s = 0; s <= 8; s++)
                            ext.AddPoint(At(path, Math.Min(len, d0 + (d1 - d0) * s / 8.0)));
                        list.Add(new Strip
                        {
                            Ext = Expand(ext, 0.35, 0.35),
                            Look = mid,
                            Twist = -Math.Atan2(p1.Y - p0.Y, p1.X - p0.X),
                            Span = Math.Max(1, (d1 - d0) * 1.06)
                        });
                    }
                    return list.Count > 0 ? list : new List<Strip> { whole };
                }
            }
            catch { return new List<Strip> { whole }; }
        }

        static List<Extents3d> SplitByDistance(Extents3d source, double totalLength, double step)
        {
            var result = new List<Extents3d>();
            int count = Math.Max(1, (int)Math.Ceiling(totalLength / step));
            for (int i = 0; i < count; i++)
            {
                double t0 = Math.Min(1.0, i * step / totalLength);
                double t1 = Math.Min(1.0, (i + 1) * step / totalLength);
                result.Add(new Extents3d(
                    new Point3d(source.MinPoint.X + (source.MaxPoint.X - source.MinPoint.X) * t0, source.MinPoint.Y, source.MinPoint.Z),
                    new Point3d(source.MinPoint.X + (source.MaxPoint.X - source.MinPoint.X) * t1, source.MaxPoint.Y, source.MaxPoint.Z)));
            }
            return result;
        }

        static List<Extents3d> Split(Extents3d source, int count, bool vertical)
        {
            var result = new List<Extents3d>();
            for (int i = 0; i < count; i++)
            {
                double t0 = i / (double)count;
                double t1 = (i + 1) / (double)count;
                result.Add(vertical
                    ? new Extents3d(
                        new Point3d(source.MinPoint.X, source.MinPoint.Y + (source.MaxPoint.Y - source.MinPoint.Y) * t0, source.MinPoint.Z),
                        new Point3d(source.MaxPoint.X, source.MinPoint.Y + (source.MaxPoint.Y - source.MinPoint.Y) * t1, source.MaxPoint.Z))
                    : new Extents3d(
                        new Point3d(source.MinPoint.X + (source.MaxPoint.X - source.MinPoint.X) * t0, source.MinPoint.Y, source.MinPoint.Z),
                        new Point3d(source.MinPoint.X + (source.MaxPoint.X - source.MinPoint.X) * t1, source.MaxPoint.Y, source.MaxPoint.Z)));
            }
            return result;
        }

        static string UniqueLayoutName(string baseName)
        {
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            {
                var layouts = (DBDictionary)tr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
                string name = baseName;
                for (int i = 1; layouts.Contains(name); i++) name = baseName + "-" + i;
                return name;
            }
        }

        static void AddAttributes(BlockReference frame, BlockReference sample, ObjectId definitionId, Transaction tr)
        {
            var sampleValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in sample.AttributeCollection)
                if (tr.GetObject(id, OpenMode.ForRead) is AttributeReference sourceAttribute)
                    sampleValues[sourceAttribute.Tag] = sourceAttribute.TextString;

            AddAttributes(frame, definitionId, sampleValues, tr);
        }

        static void AddAttributes(BlockReference frame, ObjectId definitionId,
            IDictionary<string, string> sampleValues, Transaction tr)
        {
            var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);
            foreach (ObjectId id in definition)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not AttributeDefinition definitionAttribute
                    || definitionAttribute.Constant) continue;
                try
                {
                    var attribute = new AttributeReference();
                    attribute.SetAttributeFromBlock(definitionAttribute, frame.BlockTransform);
                    attribute.TextString = sampleValues != null && sampleValues.TryGetValue(definitionAttribute.Tag, out string value)
                        ? value
                        : definitionAttribute.TextString;
                    frame.AttributeCollection.AppendAttribute(attribute);
                    tr.AddNewlyCreatedDBObject(attribute, true);
                }
                catch { }
            }
        }

        public static string[] ListPc3()
        {
            try
            {
                string d = AcadApp.GetSystemVariable("PrinterConfigDir") as string;
                if (!string.IsNullOrEmpty(d) && Directory.Exists(d))
                    return Directory.GetFiles(d, "*.pc3").Select(Path.GetFileName).OrderBy(s => s).ToArray();
            }
            catch { }
            return new[] { "DWG To PDF.pc3" };
        }

        public static string[] ListCtb()
        {
            try
            {
                string d = AcadApp.GetSystemVariable("PrinterStyleSheetDir") as string;
                if (!string.IsNullOrEmpty(d) && Directory.Exists(d))
                    return Directory.GetFiles(d, "*.ctb").Select(Path.GetFileName).OrderBy(s => s).ToArray();
            }
            catch { }
            return new[] { "monochrome.ctb", "acad.ctb" };
        }

        public static bool PlotWindow(ObjectId frame, string pc3, string ctb, string file)
        {
            LastError = null;
            var bb = BBox(frame);
            if (bb == null || Doc == null || string.IsNullOrWhiteSpace(pc3)
                || string.IsNullOrWhiteSpace(file))
            {
                LastError = "Khung, máy in hoặc đường dẫn PDF không hợp lệ.";
                return false;
            }
            ObjectId layoutId;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                layoutId = model.LayoutId;
            }
            return PlotToFile(layoutId, new Extents2d(
                bb.Value.MinPoint.X, bb.Value.MinPoint.Y,
                bb.Value.MaxPoint.X, bb.Value.MaxPoint.Y), pc3, ctb, file);
        }

        public static bool PlotLayout(ObjectId layoutId, string pc3, string ctb, string file)
        {
            LastError = null;
            if (Db == null || !IsCurrentDatabase(layoutId))
            {
                LastError = "Layout không thuộc bản vẽ đang mở.";
                return false;
            }
            return PlotToFile(layoutId, null, pc3, ctb, file);
        }

        public static ObjectId ModelLayoutId()
        {
            if (Db == null) return ObjectId.Null;
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                return model.LayoutId;
            }
        }

        public static bool PlotSheets(IEnumerable<PlotSheetRequest> requests, string pc3, string file)
        {
            LastError = null;
            var sheets = requests?.Where(x => x != null && IsCurrentDatabase(x.LayoutId)).ToList()
                ?? new List<PlotSheetRequest>();
            if (Doc == null || sheets.Count == 0 || string.IsNullOrWhiteSpace(pc3) || string.IsNullOrWhiteSpace(file))
            {
                LastError = "Danh sách tờ, máy in hoặc đường dẫn PDF không hợp lệ.";
                return false;
            }
            int pageIndex = -1;
            string fullPath = null;
            try
            {
                fullPath = Path.GetFullPath(file);
                string outputDir = Path.GetDirectoryName(fullPath) ?? ".";
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
                if (File.Exists(fullPath)) File.Delete(fullPath);

                using (Doc.LockDocument())
                {
                    string originalLayout = LayoutManager.Current.CurrentLayout;
                    var settingsToDispose = new List<PlotSettings>();
                    try
                    {
                        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                            throw new InvalidOperationException("AutoCAD đang có một tác vụ plot khác.");

                        using (var tr = Db.TransactionManager.StartTransaction())
                        {
                            var infos = new List<PlotInfo>();
                            foreach (var sheet in sheets)
                            {
                                pageIndex++;
                                var layout = (Layout)tr.GetObject(sheet.LayoutId, OpenMode.ForRead);
                                LayoutManager.Current.CurrentLayout = layout.LayoutName;
                                Db.UpdateExt(true);
                                Ed.Regen();
                                var settings = BuildPlotSettings(layout, sheet.Window,
                                    sheet.Window == null ? GetPaperBounds(layout, tr) : null, pc3, sheet.Ctb);
                                settingsToDispose.Add(settings);
                                var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
                                new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled }.Validate(info);
                                infos.Add(info);
                            }

                            using (var engine = PlotFactory.CreatePublishEngine())
                            {
                                engine.BeginPlot(null, null);
                                engine.BeginDocument(infos[0], Doc.Name, null, 1, true, fullPath);
                                for (int i = 0; i < infos.Count; i++)
                                {
                                    pageIndex = i;
                                    var page = new PlotPageInfo();
                                    engine.BeginPage(page, infos[i], i == infos.Count - 1, null);
                                    engine.BeginGenerateGraphics(null);
                                    engine.EndGenerateGraphics(null);
                                    engine.EndPage(null);
                                }
                                engine.EndDocument(null);
                                engine.EndPlot(null);
                            }
                            tr.Commit();
                        }
                    }
                    finally
                    {
                        foreach (var settings in settingsToDispose) settings.Dispose();
                        try { LayoutManager.Current.CurrentLayout = originalLayout; }
                        catch { }
                    }
                }

                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                    throw new InvalidOperationException("AutoCAD không tạo được file PDF cuối.");
                using (var pdf = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import))
                {
                    if (pdf.PageCount != sheets.Count)
                        throw new InvalidOperationException($"PDF có {pdf.PageCount} trang, cần {sheets.Count} trang.");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                LastError = pageIndex >= 0 ? $"Tờ {pageIndex + 1}: {ex.Message}" : ex.Message;
                if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
                {
                    try { File.Delete(fullPath); }
                    catch { }
                }
                return false;
            }
        }

        static bool PlotToFile(ObjectId layoutId, Extents2d? window, string pc3, string ctb, string file)
        {
            if (Doc == null || string.IsNullOrWhiteSpace(pc3) || string.IsNullOrWhiteSpace(file)) return false;
            LastError = null;
            try
            {
                string fullPath = Path.GetFullPath(file);
                string outputDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);
                if (File.Exists(fullPath)) File.Delete(fullPath);

                using (Doc.LockDocument())
                {
                    string originalLayout = LayoutManager.Current.CurrentLayout;
                    try
                    {
                        string targetLayout;
                        using (var nameTransaction = Db.TransactionManager.StartOpenCloseTransaction())
                            targetLayout = ((Layout)nameTransaction.GetObject(layoutId, OpenMode.ForRead)).LayoutName;
                        LayoutManager.Current.CurrentLayout = targetLayout;
                        Db.UpdateExt(true);
                        Ed.Regen();
                        using (var tr = Db.TransactionManager.StartTransaction())
                        {
                            var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                            using var settings = BuildPlotSettings(layout, window, window == null ? GetPaperBounds(layout, tr) : null, pc3, ctb);

                            var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
                            var infoValidator = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
                            infoValidator.Validate(info);
                            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                            {
                                LastError = "AutoCAD đang có một tác vụ plot khác.";
                                return false;
                            }

                            using (var engine = PlotFactory.CreatePublishEngine())
                            {
                                var page = new PlotPageInfo();
                                engine.BeginPlot(null, null);
                                engine.BeginDocument(info, Doc.Name, null, 1, true, fullPath);
                                engine.BeginPage(page, info, true, null);
                                engine.BeginGenerateGraphics(null);
                                engine.EndGenerateGraphics(null);
                                engine.EndPage(null);
                                engine.EndDocument(null);
                                engine.EndPlot(null);
                            }
                            tr.Commit();
                        }
                    }
                    finally
                    {
                        try { LayoutManager.Current.CurrentLayout = originalLayout; }
                        catch { }
                    }
                }
                return File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
            }
            catch (System.Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        static PlotSettings BuildPlotSettings(Layout layout, Extents2d? window, Extents2d? paperBounds, string pc3, string ctb)
        {
            var settings = new PlotSettings(layout.ModelType);
            settings.CopyFrom(layout);
            var validator = PlotSettingsValidator.Current;
            validator.SetPlotConfigurationName(settings, pc3, null);
            validator.RefreshLists(settings);
            validator.SetPlotPaperUnits(settings, PlotPaperUnit.Millimeters);
            validator.SetUseStandardScale(settings, true);
            if (window != null)
            {
                SelectBestMedia(settings, validator,
                    Math.Abs(window.Value.MaxPoint.X - window.Value.MinPoint.X),
                    Math.Abs(window.Value.MaxPoint.Y - window.Value.MinPoint.Y));
                validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
                validator.SetPlotWindowArea(settings, window.Value);
                validator.SetStdScaleType(settings, StdScaleType.StdScale1To1);
                validator.SetPlotCentered(settings, true);
            }
            else
            {
                if (paperBounds != null)
                {
                    SelectBestMedia(settings, validator,
                        Math.Abs(paperBounds.Value.MaxPoint.X - paperBounds.Value.MinPoint.X),
                        Math.Abs(paperBounds.Value.MaxPoint.Y - paperBounds.Value.MinPoint.Y));
                }
                validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
                validator.SetStdScaleType(settings, StdScaleType.StdScale1To1);
                validator.SetPlotCentered(settings, true);
            }
            if (!string.IsNullOrWhiteSpace(ctb)) validator.SetCurrentStyleSheet(settings, ctb);
            return settings;
        }

        static Extents2d? GetPaperBounds(Layout layout, Transaction tr)
        {
            var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            Extents3d? bounds = null;
            foreach (ObjectId id in paper)
            {
                if (tr.GetObject(id, OpenMode.ForRead, false) is not Entity entity) continue;
                if (entity is Viewport viewport && viewport.Number == 1) continue;
                if (TryExtents(entity, out Extents3d ext)) bounds = Union(bounds, ext);
            }
            return bounds == null ? null : new Extents2d(bounds.Value.MinPoint.X, bounds.Value.MinPoint.Y, bounds.Value.MaxPoint.X, bounds.Value.MaxPoint.Y);
        }

        static void SelectBestMedia(PlotSettings settings, PlotSettingsValidator validator, double width, double height)
        {
            string best = null;
            PlotRotation rotation = PlotRotation.Degrees000;
            double bestArea = double.MaxValue;
            bool bestNeedsRotation = true;
            foreach (string media in validator.GetCanonicalMediaNameList(settings))
            {
                try
                {
                    validator.SetCanonicalMediaName(settings, media);
                    double paperWidth = settings.PlotPaperSize.X;
                    double paperHeight = settings.PlotPaperSize.Y;
                    if (paperWidth <= 0 || paperHeight <= 0) continue;
                    var margins = settings.PlotPaperMargins;
                    double printableWidth = paperWidth - margins.MinPoint.X - margins.MaxPoint.X;
                    double printableHeight = paperHeight - margins.MinPoint.Y - margins.MaxPoint.Y;
                    if (printableWidth <= 0 || printableHeight <= 0) { printableWidth = paperWidth; printableHeight = paperHeight; }
                    bool normal = printableWidth + 0.5 >= width && printableHeight + 0.5 >= height;
                    bool rotated = printableWidth + 0.5 >= height && printableHeight + 0.5 >= width;
                    if (!normal && !rotated) continue;
                    double area = paperWidth * paperHeight;
                    bool needsRotation = !normal && rotated;
                    if (area > bestArea + 0.01 || (Math.Abs(area - bestArea) <= 0.01 && (!bestNeedsRotation || needsRotation))) continue;
                    bestArea = area;
                    best = media;
                    bestNeedsRotation = needsRotation;
                    rotation = needsRotation ? PlotRotation.Degrees090 : PlotRotation.Degrees000;
                }
                catch { }
            }
            if (best == null) return;
            validator.SetCanonicalMediaName(settings, best);
            validator.SetPlotRotation(settings, rotation);
        }
    }
}
