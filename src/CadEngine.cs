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
        public string Title;
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
        static CadEngine() { DependencyResolver.Register(); }
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
            public List<Extents2d> Regions = new List<Extents2d>();
            public string StationTitle;
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
        public static string DetectionNote { get; private set; }
        public static List<Extents3d> ProfileCandidates { get; private set; } = new List<Extents3d>();
        public static List<ObjectId> LastFrames { get; private set; } = new List<ObjectId>();
        public static List<string> LastTypes { get; private set; } = new List<string>();

        public static void ClearTransientSheets()
        {
            LastFrames = new List<ObjectId>();
            LastTypes = new List<string>();
            LastTitles = new List<string>();
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

        static bool IsProfile(Entity ent, string originalLayer = null)
        {
            string layer = (originalLayer ?? ent.Layer ?? "").ToUpperInvariant();
            if (layer.Contains("PLINETNTN") || layer.Contains("TRACNGANG")) return false;
            return layer.Contains("PLINETDTN") || layer.Contains("PLINETD") || layer.Contains("TRACDOC")
                || layer.Contains("PROFILE");
        }

        static bool IsSection(Entity ent, string originalLayer = null)
        {
            string layer = (originalLayer ?? ent.Layer ?? "").ToUpperInvariant();
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
            try { using (var reference = RoadInteropService.ReadAlignment(ent)) return reference?.Length ?? 0; }
            catch (Autodesk.AutoCAD.Runtime.Exception) { return 0; }
        }

        public static bool ExtentsOf(Entity entity, out Extents3d extents) => TryExtents(entity, out extents);


        public static bool QuetBinhDo(out ObjectId id, out double len, out bool estimated)
        {
            id = ObjectId.Null; len = 0; estimated = false;
            DetectionNote = null;
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
                    string layer = SheetMetadataService.SourceLayer(ent, tr).ToUpperInvariant();
                    bool align = IsAlignment(ent);
                    bool onTim = layer.Contains("TIM") || layer == "TUYEN" || layer.Contains("CENTER");
                    if (!align && ent is not Polyline && ent is not Polyline2d && ent is not Polyline3d) continue;
                    if (IsProfile(ent, layer) || IsSection(ent, layer)) continue;
                    double d = MeasureLength(ent);
                    bool guess = false;
                    if (d < 1 && align && alignId.IsNull && TryExtents(ent, out _))
                    {
                        alignId = eid;
                        alignGuess = true;
                        DetectionNote = "Đã thấy tim TDT dạng proxy; mở bản vẽ bằng profile TDT/VNroad để đọc hình học tim và cọc.";
                    }
                    if (d < 1) continue;
                    if (d > fallbackLen && ent is Curve) { fallbackLen = d; fallbackId = eid; }
                    if (align && d > alignLen) { alignLen = d; alignId = eid; alignGuess = guess; }
                    else if (!align && onTim && d > polyLen) { polyLen = d; polyId = eid; }
                }
                tr.Commit();
            }
            if (!alignId.IsNull && alignLen > 0) { id = alignId; len = alignLen; estimated = alignGuess; DetectionNote = null; return true; }
            if (!polyId.IsNull)
            {
                id = polyId; len = polyLen; estimated = !alignId.IsNull;
                if (estimated) DetectionNote = "Đang dùng polyline tham chiếu vì tim TDT là proxy. Cần profile TDT/VNroad phù hợp và đủ nhãn cọc để ghép BĐ + TĐ khớp lý trình.";
                else DetectionNote = null;
                return true;
            }
            if (!alignId.IsNull) { id = alignId; estimated = true; return true; }
            if (!fallbackId.IsNull) { id = fallbackId; len = fallbackLen; estimated = true; return true; }
            return false;
        }

        public static int QuetTracDocKm(out Extents3d? source)
        {
            source = null;
            ProfileCandidates = new List<Extents3d>();
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var all = new List<Extents3d>();
                var terrain = new List<Extents3d>();
                var design = new List<Extents3d>();
                foreach (ObjectId eid in ms)
                {
                    var ent = tr.GetObject(eid, OpenMode.ForRead, false);
                    if (ent is not Entity entity) continue;
                    string originalLayer = SheetMetadataService.SourceLayer(entity, tr);
                    if (IsProfile(entity, originalLayer) && (entity is Curve || entity is ProxyEntity) && TryExtents(entity, out Extents3d profileExt))
                    {
                        if (originalLayer.IndexOf("PLINETDTN", StringComparison.OrdinalIgnoreCase) >= 0) terrain.Add(profileExt);
                        else design.Add(profileExt);
                    }
                    if (ent is Entity drawn && !IsAlignment(drawn) && TryExtents(drawn, out Extents3d ext)) all.Add(ext);
                }
                // TRACDOCTHIETKE/PLINETDTN are only the terrain/design curves.
                // Grow from those stable VNroad layers to include the table,
                // station labels and ordinates that belong to the same profile.
                ProfileCandidates = RoadDrawingDetectionService.Profiles(terrain.Count > 0 ? terrain : MergeOverlapping(design), all);
                if (ProfileCandidates.Count > 0) source = ProfileCandidates[0];
                tr.Commit();
            }
            // The number of matching polylines is not the number of profiles.
            return ProfileCandidates.Count;
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
                    if (hasExt && IsSection(entity, SheetMetadataService.SourceLayer(entity, tr))) parts.Add(ext);
                }
                if (items.Count == 0 && markers.Count > 0)
                {
                    // VNroad 7.1 writes KS_TN on the representative polyline of
                    // each cross-section.  Use it as the primary seed instead of
                    // guessing from every line on PLINETNTN.
                    items = RoadDrawingDetectionService.Sections(MergeOverlapping(markers), all);
                }
                else if (items.Count == 0 && parts.Count > 0)
                {
                    items = RoadDrawingDetectionService.Sections(MergeOverlapping(parts), all);
                }
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
                Extents3d probe = Expand(value, 0.001, 0.005);
                for (int i = 0; i < result.Count; i++)
                {
                    if (!Intersects2d(Expand(result[i], 0.001, 0.005), probe)) continue;
                    match = i;
                    break;
                }
                if (match < 0) result.Add(value);
                else result[match] = Union(result[match], value).Value;
            }
            return result;
        }


        static double Width(Extents3d ext) => Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
        static double Height(Extents3d ext) => Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);

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
                    var selectedFrame = (Entity)tr.GetObject(frameId, OpenMode.ForRead);
                    SheetMetadataService.TryRead(selectedFrame, tr, out _, out _, out string title);
                    result.Add(new LayoutSheetInfo
                    {
                        LayoutName = layout.LayoutName,
                        Type = match.Groups[1].Value.ToUpperInvariant(),
                        Index = int.Parse(match.Groups[2].Value),
                        LayoutId = entry.Value,
                        FrameId = frameId,
                        Title = title
                    });
                }
                tr.Commit();
            }
            int TypeOrder(string type) => type == "BD" ? 0 : type == "TD" ? 1 : 2;
            return result.OrderBy(x => TypeOrder(x.Type)).ThenBy(x => x.Index).ThenBy(x => x.LayoutName).ToList();
        }

        public static void ScanModelSheets()
        {
            ClearTransientSheets();
            if (Db == null) return;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var sheets = new List<(ObjectId Id, string Type, int Index, string Title)>();
                foreach (ObjectId id in model)
                    if (tr.GetObject(id, OpenMode.ForRead, false) is BlockReference frame
                        && SheetMetadataService.TryRead(frame, tr, out string type, out int index, out string title))
                        sheets.Add((id, type, index, title));
                var sorted = sheets.OrderBy(x => x.Type == "BD" ? 0 : x.Type == "TD" ? 1 : 2).ThenBy(x => x.Index).ThenBy(x => x.Id.Handle.Value).ToList();
                LastFrames = sorted.Select(x => x.Id).ToList(); LastTypes = sorted.Select(x => x.Type).ToList(); LastTitles = sorted.Select(x => x.Title).ToList();
            }
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

            if (hideCopiedGeometry && ScanLayoutSheets().Count > 0)
            {
                error = "Layout GKIN đang dùng hình nguồn. Tắt 'Không in hình nguồn đã ghép' để giữ nội dung các viewport.";
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
                    // Snapshot source ids once. Generated windows from earlier
                    // sheets must never become sources for later sheets.
                    var originalIds = model.Cast<ObjectId>().Where(id => id != sampleFrame).ToList();
                    var copiedSources = new HashSet<ObjectId>();

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
                        SheetMetadataService.Store(frame, tr, plans[index].Type, plans[index].Index, TitleOf(plans[index]));
                        result.Add(frame.ObjectId);
                        if (plans[index].Type == "TD") tdCreated++;

                        Extents3d targetFrame = TryExtents(frame, out Extents3d placed) ? placed : new Extents3d(targetMin, targetMin + new Vector3d(frameWidth, frameHeight, 0));
                        double usableWidth = frameWidth * 0.76;
                        double usableHeight = frameHeight * 0.88;
                        double left = targetFrame.MinPoint.X + frameWidth * 0.04;
                        double bottom = targetFrame.MinPoint.Y + frameHeight * 0.06;
                        int windowCount = Math.Max(1, plans[index].Windows.Count);

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
                            var region = PlanRegion(plans[index], windowIndex);
                            var placement = WindowPlacement(plans[index], windowIndex, left, bottom, usableWidth, usableHeight);
                            double targetWidth = placement.Width;
                            double targetHeight = placement.Height;
                            double twist = windowIndex < plans[index].Twist.Count ? plans[index].Twist[windowIndex] : 0;
                            double span = windowIndex < plans[index].Span.Count ? plans[index].Span[windowIndex] : 0;
                            var rotatedBounds = sourceWindow;
                            rotatedBounds.TransformBy(Matrix3d.Rotation(twist, Vector3d.ZAxis, Point3d.Origin));
                            sourceWidth = Math.Max(1e-6, Width(rotatedBounds)); sourceHeight = Math.Max(1e-6, Height(rotatedBounds));
                            if (span > 0) sourceWidth = Math.Max(sourceWidth, span);
                            double scale = placement.Scale;
                            var sourceCenter = windowIndex < plans[index].Look.Count ? plans[index].Look[windowIndex]
                                : new Point3d((sourceWindow.MinPoint.X + sourceWindow.MaxPoint.X) / 2.0, (sourceWindow.MinPoint.Y + sourceWindow.MaxPoint.Y) / 2.0, 0);
                            var targetCenter = placement.Center;
                            var transform = Matrix3d.Displacement(targetCenter - Point3d.Origin)
                                * Matrix3d.Scaling(scale, Point3d.Origin)
                                * Matrix3d.Rotation(twist, Vector3d.ZAxis, Point3d.Origin)
                                * Matrix3d.Displacement(Point3d.Origin - sourceCenter);

                            var sources = new List<ObjectId>();
                            foreach (ObjectId sourceId in originalIds)
                            {
                                if (sourceId == sampleFrame || result.Contains(sourceId)) continue;
                                try
                                {
                                    if (tr.GetObject(sourceId, OpenMode.ForRead, false) is not Entity source || !TryExtents(source, out Extents3d sourceExt) || !Intersects2d(sourceWindow, sourceExt)) continue;
                                    if (source is Viewport || source is AttributeDefinition) continue;
                                    if (source is BlockReference block)
                                    {
                                        if (FramePriority(block, tr) > 0 || string.Equals(EffectiveName(block), sampleName, StringComparison.OrdinalIgnoreCase)) continue;
                                        var owner = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                                        if ((owner.Name ?? "").StartsWith("GKIN-VIEW-", StringComparison.OrdinalIgnoreCase)) continue;
                                    }
                                    sources.Add(sourceId);
                                }
                                catch { skipped++; }
                            }
                            var destination = new Extents2d(targetCenter.X - targetWidth * 0.5 - overlap * scale, targetCenter.Y - targetHeight * 0.5 - overlap * scale,
                                targetCenter.X + targetWidth * 0.5 + overlap * scale, targetCenter.Y + targetHeight * 0.5 + overlap * scale);
                            ObjectId windowId = ModelWindowService.Create(Db, tr, model, sources, transform, destination, layerId, sourceWindow);
                            if (windowId.IsNull) throw new InvalidOperationException("Không có hình học nguồn trong vùng tờ " + (index + 1));
                            foreach (ObjectId sourceId in sources) copiedSources.Add(sourceId);
                        }
                    }
                    // Move ORIGINALS after all print copies have been produced.
                    // The same transaction makes this action undoable.
                    if (!hiddenLayerId.IsNull)
                        foreach (ObjectId id in copiedSources)
                            if (tr.GetObject(id, OpenMode.ForWrite, false) is Entity source) SheetMetadataService.HideSource(source, hiddenLayerId, tr);
                    tr.Commit();
                    Db.TransactionManager.QueueForGraphicsFlush();
                    if (skipped > 0)
                        error = "Đã bỏ qua " + skipped + " đối tượng không sao chép được (xref, viewport hoặc proxy).";
                    LastFrames = new List<ObjectId>(result);
                    LastTypes = plans.Take(result.Count).Select(p => p.Type).ToList();
                    LastTitles = plans.Take(result.Count).Select(TitleOf).ToList();
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
                    AssertSourcesPrintable(plans);
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

                                SheetMetadataService.Store(frame, tr, plan.Type, plan.Index, TitleOf(plan));


                                PlacePlanViewports(paper, tr, plan, frameExt, viewportIds);

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
                                FrameId = frameId,
                                Title = TitleOf(plan)
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
            LastTitles = plans.Take(result.Count).Select(TitleOf).ToList();
            return result;
        }

        static void AssertSourcesPrintable(IList<SheetPlan> plans)
        {
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in model)
                    if (tr.GetObject(id, OpenMode.ForRead, false) is Entity source && SheetMetadataService.IsHiddenSource(source, tr)
                        && TryExtents(source, out Extents3d ext) && plans.Any(p => p.Windows.Any(w => Intersects2d(w, ext))))
                        throw new InvalidOperationException("Hình nguồn đang ở layer không in. Undo hoặc khôi phục layer nguồn trước khi xuất Layout.");
            }
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
            List<RoadInteropService.Label> drawingLabels;
            using (Doc.LockDocument())
            using (var read = Db.TransactionManager.StartOpenCloseTransaction())
                drawingLabels = RoadInteropService.ReadLabels(Db, read);
            List<ProfileCutterService.Band> bands = td == null ? null
                : ProfileCutterService.Cut(td.Value, tdLength, tdStep, Math.Max(1, tdCount), drawingLabels);
            if (bd != null && td != null && mergeBdTd)
            {
                if (bands == null || bands.Any(x => !x.HasRealStations))
                    throw new InvalidOperationException("Chưa đọc đủ lý trình trên trắc dọc để ghép đúng cọc. Hãy chọn trắc dọc có ít nhất hai nhãn Km.");
                using (Doc.LockDocument())
                using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
                using (var reference = bdCurve.IsNull ? null : RoadInteropService.ReadAlignment(tr.GetObject(bdCurve, OpenMode.ForRead) as Entity))
                {
                    if (reference == null)
                        throw new InvalidOperationException("Chưa đọc được hình học tim. Mở bản vẽ bằng profile TDT/VNroad hoặc chọn polyline tim tham chiếu.");
                    var anchors = RoadInteropService.RouteStations(reference, drawingLabels, Math.Max(MetersToDrawingUnits(30), reference.Length * 0.005));
                    if (!RoadInteropService.MatchesRouteDistances(anchors))
                        throw new InvalidOperationException("Cọc trên bình đồ chưa tạo được dãy lý trình liên tục; hãy chọn đúng tuyến hoặc kiểm tra nhãn cọc.");
                    double lower = anchors.Min(x => x.Station), upper = anchors.Max(x => x.Station);
                    foreach (var band in bands)
                    {
                        if (band.StartStation < lower - 0.01 || band.EndStation > upper + 0.01)
                            throw new InvalidOperationException("Khoảng lý trình bình đồ không phủ hết trắc dọc; chưa thể ghép tờ khớp cọc.");
                        double d0 = RoadInteropService.Interpolate(anchors, band.StartStation);
                        double d1 = RoadInteropService.Interpolate(anchors, band.EndStation);
                        var strip = AlignmentStrip(reference, d0, d1);
                        var plan = new SheetPlan { Type = "TD", Index = plans.Count + 1,
                            StationTitle = "BÌNH ĐỒ + TRẮC DỌC · " + band.From + " — " + band.To };
                        AddPlanWindow(plan, strip.Ext, strip.Look, strip.Twist, strip.Span, Region(0, 0.52, 1, 1));
                        AddBandWindows(plan, band.Windows, Region(0, 0, 1, 0.48));
                        plans.Add(plan);
                    }
                }
            }
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
            if (td != null && !(mergeBdTd && bd != null))
            {
                for (int i = 0; i < bands.Count; i++)
                {
                    var band = bands[i];
                    var plan = new SheetPlan
                    {
                        Type = "TD",
                        Index = i + 1,
                        StackVertical = false,
                        StationTitle = band.HasRealStations ? "TRẮC DỌC · " + band.From + " — " + band.To : null,
                        Slots = Math.Max(1, band.Windows.Count)
                    };
                    AddBandWindows(plan, band.Windows, Region(0, 0, 1, 1));
                    plans.Add(plan);
                }
            }
            if (tnItems != null && tnItems.Count > 0)
            {
                int per = Math.Max(1, Math.Min(4, tnPerSheet));
                double StationOf(Extents3d item)
                {
                    var stationLabel = drawingLabels.Where(x => x.Position.X >= item.MinPoint.X && x.Position.X <= item.MaxPoint.X
                        && x.Position.Y >= item.MinPoint.Y && x.Position.Y <= item.MaxPoint.Y && RoadInteropService.TryStation(x.Text, out _))
                        .OrderByDescending(x => x.Layer.IndexOf("DAUCO", StringComparison.OrdinalIgnoreCase) >= 0).FirstOrDefault();
                    return stationLabel != null && RoadInteropService.TryStation(stationLabel.Text, out double value) ? value : double.MaxValue;
                }
                tnItems = tnItems.OrderBy(StationOf).ThenByDescending(x => x.MaxPoint.Y).ThenBy(x => x.MinPoint.X).ToList();
                var ownHeaders = tnItems.Select(item => ProfileCutterService.FindHeader(item, drawingLabels)).ToList();
                var headers = ownHeaders.Where(x => x != null).Select(x => x.Value).ToList();
                foreach (var sheet in CrossSectionPackerService.Pack(tnItems, per, verticalTn))
                {
                    var plan = new SheetPlan
                    {
                        Type = "TN",
                        Index = sheet.Index,
                        StackVertical = sheet.StackVertical,
                        Slots = per,
                    };
                    for (int i = 0; i < sheet.Windows.Count; i++)
                    {
                        var item = sheet.Windows[i];
                        var cell = verticalTn ? Region(0, 1 - (i + 1.0) / per, 1, 1 - i / (double)per)
                            : Region(i / (double)per, 0, (i + 1.0) / per, 1);
                        var ownHeader = ProfileCutterService.FindHeader(item, drawingLabels);
                        if (headers.Count > 0)
                        {
                            // Reuse the actual first table heading as its own
                            // clipped window for every section lacking one.
                            var header = ownHeader ?? headers.OrderBy(x => Math.Abs(Height(x) - Height(item))).First();
                            if (ownHeader != null && header.MaxPoint.X > item.MinPoint.X && header.MaxPoint.X < item.MaxPoint.X)
                                item = new Extents3d(new Point3d(header.MaxPoint.X, item.MinPoint.Y, 0), item.MaxPoint);
                            AddBandWindows(plan, new List<Extents3d> { header, item }, cell);
                        }
                        else AddBandWindows(plan, new List<Extents3d> { item }, cell);
                    }
                    plans.Add(plan);
                }
            }
            return plans;
        }

        static Extents2d Region(double left, double bottom, double right, double top) => new Extents2d(left, bottom, right, top);

        static void AddPlanWindow(SheetPlan plan, Extents3d source, Point3d look, double twist, double span, Extents2d region)
        {
            plan.Windows.Add(source); plan.Look.Add(look); plan.Twist.Add(twist); plan.Span.Add(span); plan.Regions.Add(region);
        }

        static void AddBandWindows(SheetPlan plan, IList<Extents3d> windows, Extents2d cell)
        {
            double total = windows.Sum(x => Math.Max(1e-6, Width(x)));
            double cursor = cell.MinPoint.X;
            foreach (var window in windows)
            {
                double width = (cell.MaxPoint.X - cell.MinPoint.X) * Math.Max(1e-6, Width(window)) / total;
                AddPlanWindow(plan, window, new Point3d((window.MinPoint.X + window.MaxPoint.X) / 2,
                    (window.MinPoint.Y + window.MaxPoint.Y) / 2, 0), 0, 0,
                    Region(cursor, cell.MinPoint.Y, cursor + width, cell.MaxPoint.Y));
                cursor += width;
            }
        }

        static Extents2d PlanRegion(SheetPlan plan, int index)
        {
            if (plan.Regions.Count == plan.Windows.Count) return plan.Regions[index];
            int count = Math.Max(1, plan.Windows.Count);
            if (!plan.StackVertical) return Region(index / (double)count, 0, (index + 1.0) / count, 1);
            int slots = Math.Max(count, plan.Slots);
            return Region(0, 1 - (index + 1.0) / slots, 1, 1 - index / (double)slots);
        }

        static (double Width, double Height) SourceSize(SheetPlan plan, int index)
        {
            var source = plan.Windows[index];
            var look = plan.Look[index];
            var rotated = source;
            rotated.TransformBy(Matrix3d.Rotation(plan.Twist[index], Vector3d.ZAxis, look));
            return (Math.Max(plan.Span[index], 2 * Math.Max(Math.Abs(rotated.MinPoint.X - look.X), Math.Abs(rotated.MaxPoint.X - look.X))),
                Math.Max(1e-6, 2 * Math.Max(Math.Abs(rotated.MinPoint.Y - look.Y), Math.Abs(rotated.MaxPoint.Y - look.Y))));
        }

        static (Point3d Center, double Width, double Height, double Scale) WindowPlacement(SheetPlan plan, int index,
            double left, double bottom, double width, double height)
        {
            var cell = PlanRegion(plan, index);
            var row = Enumerable.Range(0, plan.Windows.Count).Where(i =>
                Math.Abs(PlanRegion(plan, i).MinPoint.Y - cell.MinPoint.Y) < 1e-6
                && Math.Abs(PlanRegion(plan, i).MaxPoint.Y - cell.MaxPoint.Y) < 1e-6).ToList();
            double x0 = row.Min(i => PlanRegion(plan, i).MinPoint.X), x1 = row.Max(i => PlanRegion(plan, i).MaxPoint.X);
            double total = row.Sum(i => SourceSize(plan, i).Width), maxHeight = row.Max(i => SourceSize(plan, i).Height);
            double scale = Math.Min(width * (x1 - x0) / Math.Max(1e-6, total), height * (cell.MaxPoint.Y - cell.MinPoint.Y) / maxHeight) * 0.96;
            double cursor = left + width * (x0 + x1) / 2 - total * scale / 2;
            foreach (int i in row)
            {
                var size = SourceSize(plan, i);
                if (i == index) return (new Point3d(cursor + size.Width * scale / 2,
                    bottom + height * (cell.MinPoint.Y + cell.MaxPoint.Y) / 2 - (maxHeight - size.Height) * scale / 2, 0), size.Width * scale, size.Height * scale, scale);
                cursor += size.Width * scale;
            }
            throw new InvalidOperationException("Không tìm thấy vị trí cửa sổ tờ.");
        }

        static Strip AlignmentStrip(Polyline route, double start, double end)
        {
            start = Math.Max(0, Math.Min(route.Length, start)); end = Math.Max(0, Math.Min(route.Length, end));
            Point3d a = route.GetPointAtDist(start), b = route.GetPointAtDist(end);
            var ext = new Extents3d(a, a);
            for (int i = 1; i <= 32; i++) ext.AddPoint(route.GetPointAtDist(start + (end - start) * i / 32));
            double pad = Math.Max(MetersToDrawingUnits(15), Math.Abs(end - start) * 0.08);
            return new Strip { Ext = new Extents3d(ext.MinPoint - new Vector3d(pad, pad, 0), ext.MaxPoint + new Vector3d(pad, pad, 0)),
                Look = route.GetPointAtDist((start + end) / 2), Twist = -Math.Atan2(b.Y - a.Y, b.X - a.X),
                Span = Math.Abs(end - start) + pad * 2 };
        }

        static List<Strip> CutAlignment(ObjectId curveId, Extents3d full, double step)
        {
            if (!IsCurrentDatabase(curveId)) throw new InvalidOperationException("Chọn tim tuyến thuộc bản vẽ đang mở.");
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            using (var route = RoadInteropService.ReadAlignment(tr.GetObject(curveId, OpenMode.ForRead, false) as Entity))
            {
                if (route == null || route.Length < 1e-6)
                    throw new InvalidOperationException("Tim đang là proxy hoặc chưa đọc được hình học. Mở profile TDT/VNroad phù hợp hoặc chọn polyline tim.");
                double length = route.Length;
                if (step <= 1e-6) step = length;
                int count = Math.Max(1, (int)Math.Ceiling(length / step));
                var result = new List<Strip>();
                for (int i = 0; i < count; i++)
                    result.Add(AlignmentStrip(route, i * step, Math.Min(length, (i + 1) * step)));
                return result;
            }
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
                                    var pageLayout = (Layout)tr.GetObject(sheets[i].LayoutId, OpenMode.ForRead);
                                    LayoutManager.Current.CurrentLayout = pageLayout.LayoutName;
                                    Ed.Regen();
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
                validator.SetPlotWindowArea(settings, window.Value);
                validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
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
