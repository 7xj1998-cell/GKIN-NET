using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GKIN
{
    public class FrameInfo
    {
        public string Name;
        public int Count;
        public ObjectId Sample;
        public int W, H;
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

    public static class CadEngine
    {
        sealed class SheetPlan
        {
            public string Type;
            public int Index;
            public List<Extents3d> Windows = new List<Extents3d>();
        }

        public static Document Doc => AcadApp.DocumentManager.MdiActiveDocument;
        public static Editor Ed => Doc?.Editor;
        public static Database Db => Doc?.Database;
        public static string LastError { get; private set; }

        public static string Pad(int n, int cs) => Math.Max(0, n).ToString().PadLeft(Math.Max(1, cs), '0');

        public static string LyTrinh(double m)
        {
            int km = (int)Math.Floor(m / 1000.0);
            double le = m - km * 1000.0;
            return $"Km{km}+{le:0.00}";
        }

        public static string FmtM(double m) =>
            m >= 1000 ? $"{m / 1000.0:0.000} km" : $"{m:0} m";

        public static List<FrameInfo> QuetKhung()
        {
            var map = new Dictionary<string, FrameInfo>(StringComparer.OrdinalIgnoreCase);
            if (Db == null) return new List<FrameInfo>();
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
                    string nm = EffectiveName(br);
                    if (!map.TryGetValue(nm, out var rec))
                    {
                        rec = new FrameInfo { Name = nm, Count = 0, Sample = id };
                        try
                        {
                            var ext = br.GeometricExtents;
                            rec.W = (int)Math.Round(ext.MaxPoint.X - ext.MinPoint.X);
                            rec.H = (int)Math.Round(ext.MaxPoint.Y - ext.MinPoint.Y);
                        }
                        catch { }
                        map[nm] = rec;
                    }
                    rec.Count++;
                }
                tr.Commit();
            }
            return map.Values.OrderByDescending(x => x.Count).ToList();
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

        public static bool QuetBinhDo(out ObjectId id, out double len)
        {
            id = ObjectId.Null; len = 0;
            if (Db == null) return false;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    var ent = tr.GetObject(eid, OpenMode.ForRead) as Entity;
                    if (ent is not Curve c) continue;
                    if (!(ent is Polyline || ent is Polyline2d || ent is Polyline3d)) continue;
                    try
                    {
                        double d = c.GetDistanceAtParameter(c.EndParam);
                        if (d > len) { len = d; id = eid; }
                    }
                    catch { }
                }
                tr.Commit();
            }
            return !id.IsNull;
        }

        public static int QuetTracDocKm(out Extents3d? source)
        {
            int n = 0;
            source = null;
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var all = new List<Extents3d>();
                foreach (ObjectId eid in ms)
                {
                    var ent = tr.GetObject(eid, OpenMode.ForRead);
                    if (ent is Entity entity && TryExtents(entity, out Extents3d ext)) all.Add(ext);
                    string s = ent switch
                    {
                        DBText t => t.TextString,
                        MText m => m.Contents,
                        _ => null
                    };
                    if (s != null && s.ToUpperInvariant().Contains("KM"))
                    {
                        n++;
                        if (ent is Entity marker && TryExtents(marker, out Extents3d markerExt))
                            source = Union(source, markerExt);
                    }
                }
                source = GrowToNearbyGeometry(source, all, 0.08, 2.50);
                tr.Commit();
            }
            return n;
        }

        public static int QuetTracNgang(out Extents3d? source)
        {
            int n = 0;
            source = null;
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var all = new List<Extents3d>();
                foreach (ObjectId eid in ms)
                {
                    var entity = tr.GetObject(eid, OpenMode.ForRead) as Entity;
                    if (entity != null && TryExtents(entity, out Extents3d ext)) all.Add(ext);
                    if (entity is not BlockReference br) continue;
                    string nm = EffectiveName(br).ToUpperInvariant();
                    if (nm.Contains("TN") || nm.Contains("TNCT") || nm.Contains("MATCAT") || nm.Contains("TRACNGANG"))
                    {
                        n++;
                        if (TryExtents(br, out Extents3d markerExt)) source = Union(source, markerExt);
                    }
                }
                source = GrowToNearbyGeometry(source, all, 0.12, 0.20);
                tr.Commit();
            }
            return n;
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
            if (Db == null || id.IsNull || !ReferenceEquals(id.Database, Db)) return tags;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference br && br.AttributeCollection != null)
                {
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        if (tr.GetObject(aid, OpenMode.ForRead) is AttributeReference ar)
                            tags.Add(ar.Tag);
                    }
                }
                tr.Commit();
            }
            return tags;
        }

        public static int GanAttrs(IEnumerable<KeyValuePair<ObjectId, Dictionary<string, string>>> updates)
        {
            if (Db == null || updates == null) return 0;
            var batch = updates.Where(x => !x.Key.IsNull
                                           && ReferenceEquals(x.Key.Database, Db)
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
            if (Db == null || id.IsNull || !ReferenceEquals(id.Database, Db)) return null;
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

        public static List<LayoutSheetInfo> CreateLayouts(
            ObjectId sampleFrame, Extents3d? bd, Extents3d? td, Extents3d? tn,
            int tdCount, int tnCount, bool mergeBdTd, bool verticalTn, out string error)
        {
            error = null;
            var result = new List<LayoutSheetInfo>();
            if (Db == null || sampleFrame.IsNull || !ReferenceEquals(sampleFrame.Database, Db))
            {
                error = "Khung mau khong thuoc ban ve dang mo.";
                return result;
            }

            var plans = BuildSheetPlans(bd, td, tn, tdCount, tnCount, mergeBdTd, verticalTn);
            if (plans.Count == 0)
            {
                error = "Khong tim thay vung nguon BD/TD/TN de tao Layout.";
                return result;
            }

            string originalLayout = LayoutManager.Current.CurrentLayout;
            using (Doc.LockDocument())
            {
                try
                {
                    foreach (var plan in plans)
                    {
                        string layoutName = UniqueLayoutName($"GKIN-{plan.Type}-{Pad(plan.Index, 2)}");
                        ObjectId layoutId = LayoutManager.Current.CreateLayout(layoutName);
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
                            var frameExt = frame.GeometricExtents;
                            frame.TransformBy(Matrix3d.Displacement(Point3d.Origin - frameExt.MinPoint));
                            AddAttributes(frame, sample, definitionId, tr);
                            frameExt = frame.GeometricExtents;

                            double frameWidth = Math.Max(1.0, frameExt.MaxPoint.X - frameExt.MinPoint.X);
                            double frameHeight = Math.Max(1.0, frameExt.MaxPoint.Y - frameExt.MinPoint.Y);
                            double usableWidth = frameWidth * 0.76;
                            double usableHeight = frameHeight * 0.88;
                            double centerX = frameWidth * 0.04 + usableWidth / 2.0;
                            double rowHeight = usableHeight / plan.Windows.Count;

                            for (int i = 0; i < plan.Windows.Count; i++)
                            {
                                var source = Expand(plan.Windows[i], 0.03, 0.05);
                                double viewportHeight = rowHeight * 0.94;
                                double centerY = frameHeight * 0.06 + i * rowHeight + rowHeight / 2.0;
                                var viewport = new Viewport
                                {
                                    CenterPoint = new Point3d(centerX, centerY, 0),
                                    Width = usableWidth,
                                    Height = viewportHeight,
                                    ViewCenter = new Point2d(
                                        (source.MinPoint.X + source.MaxPoint.X) / 2.0,
                                        (source.MinPoint.Y + source.MaxPoint.Y) / 2.0),
                                    ViewDirection = Vector3d.ZAxis,
                                    TwistAngle = 0
                                };
                                double sourceWidth = Math.Max(1e-6, source.MaxPoint.X - source.MinPoint.X);
                                double sourceHeight = Math.Max(1e-6, source.MaxPoint.Y - source.MinPoint.Y);
                                viewport.ViewHeight = Math.Max(sourceHeight, sourceWidth / (usableWidth / viewportHeight)) * 1.03;
                                paper.AppendEntity(viewport);
                                tr.AddNewlyCreatedDBObject(viewport, true);
                                viewport.On = true;
                                viewport.Locked = true;
                            }

                            var sheet = new LayoutSheetInfo
                            {
                                LayoutName = layoutName,
                                Type = plan.Type,
                                Index = plan.Index,
                                LayoutId = layoutId,
                                FrameId = frame.ObjectId
                            };
                            tr.Commit();
                            result.Add(sheet);
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
            return result;
        }

        static List<SheetPlan> BuildSheetPlans(Extents3d? bd, Extents3d? td, Extents3d? tn,
            int tdCount, int tnCount, bool mergeBdTd, bool verticalTn)
        {
            var plans = new List<SheetPlan>();
            if (bd != null && !(mergeBdTd && td != null))
                plans.Add(new SheetPlan { Type = "BD", Index = 1, Windows = new List<Extents3d> { bd.Value } });
            if (td != null)
            {
                var windows = Split(td.Value, Math.Max(1, tdCount), false);
                for (int i = 0; i < windows.Count; i++)
                {
                    var plan = new SheetPlan { Type = "TD", Index = i + 1, Windows = new List<Extents3d> { windows[i] } };
                    if (mergeBdTd && bd != null && i == 0) plan.Windows.Insert(0, bd.Value);
                    plans.Add(plan);
                }
            }
            if (tn != null)
            {
                var windows = Split(tn.Value, Math.Max(1, tnCount), verticalTn);
                for (int i = 0; i < windows.Count; i++)
                    plans.Add(new SheetPlan { Type = "TN", Index = i + 1, Windows = new List<Extents3d> { windows[i] } });
            }
            return plans;
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

            var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);
            foreach (ObjectId id in definition)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not AttributeDefinition definitionAttribute
                    || definitionAttribute.Constant) continue;
                var attribute = new AttributeReference();
                attribute.SetAttributeFromBlock(definitionAttribute, frame.BlockTransform);
                attribute.TextString = sampleValues.TryGetValue(definitionAttribute.Tag, out string value)
                    ? value
                    : definitionAttribute.TextString;
                frame.AttributeCollection.AppendAttribute(attribute);
                tr.AddNewlyCreatedDBObject(attribute, true);
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
                LastError = "Khung, may in hoac duong dan PDF khong hop le.";
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
            if (Db == null || layoutId.IsNull || !ReferenceEquals(layoutId.Database, Db))
            {
                LastError = "Layout khong thuoc ban ve dang mo.";
                return false;
            }
            return PlotToFile(layoutId, null, pc3, ctb, file);
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
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                    using var settings = new PlotSettings(layout.ModelType);
                    settings.CopyFrom(layout);

                    var validator = PlotSettingsValidator.Current;
                    validator.SetPlotConfigurationName(settings, pc3, null);
                    validator.RefreshLists(settings);
                    validator.SetUseStandardScale(settings, true);
                    if (window != null)
                    {
                        validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
                        validator.SetPlotWindowArea(settings, window.Value);
                        validator.SetStdScaleType(settings, StdScaleType.ScaleToFit);
                        validator.SetPlotCentered(settings, true);
                    }
                    else
                    {
                        validator.SetPlotType(settings, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                        validator.SetStdScaleType(settings, StdScaleType.StdScale1To1);
                    }
                    if (!string.IsNullOrWhiteSpace(ctb)) validator.SetCurrentStyleSheet(settings, ctb);

                    var info = new PlotInfo { Layout = layout.ObjectId, OverrideSettings = settings };
                    var infoValidator = new PlotInfoValidator
                    {
                        MediaMatchingPolicy = MatchingPolicy.MatchEnabled
                    };
                    infoValidator.Validate(info);
                    if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                    {
                        LastError = "AutoCAD dang co mot tac vu plot khac.";
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
                return File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
            }
            catch (System.Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }
    }
}
