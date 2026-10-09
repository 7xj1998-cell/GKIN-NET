using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using GKIN;

public class GkinWorkflowProbe
{
    readonly List<string> report = new List<string>();
    int failures;
    void Check(string name, bool passed) { report.Add((passed ? "PASS " : "FAIL ") + name); if (!passed) failures++; }

    [CommandMethod("GKINWORKFLOWPROBE")]
    public void Run()
    {
        string output = Environment.GetEnvironmentVariable("GKIN_PROBE_REPORT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set GKIN_PROBE_REPORT.");
        try
        {
            Check("parse-comma", RoadInteropService.TryStation("KM12+345,50", out double station) && station == 12345.5);
            Check("reject-overlong", !RoadInteropService.TryStation("Km12+1000", out _));
            Check("reject-malformed", !RoadInteropService.TryStation("Km12+345.5.2", out _));
            Check("station-interpolation", Math.Abs(RoadInteropService.Interpolate(new List<RoadInteropService.Anchor> {
                new RoadInteropService.Anchor {Station=12000,Coordinate=2000},
                new RoadInteropService.Anchor {Station=12500,Coordinate=2500}}, 12350) - 2350) < 1e-8);
            Check("reversed-axis", RoadInteropService.Monotonic(new List<RoadInteropService.Anchor> {
                new RoadInteropService.Anchor {Station=12000,Coordinate=1000},
                new RoadInteropService.Anchor {Station=12500,Coordinate=500}}));
            Check("ambiguous-axis-rejected", !RoadInteropService.Monotonic(new List<RoadInteropService.Anchor> {
                new RoadInteropService.Anchor {Station=12000,Coordinate=0},
                new RoadInteropService.Anchor {Station=12500,Coordinate=500},
                new RoadInteropService.Anchor {Station=12600,Coordinate=400}}));
            using (var arc = new Arc(Point3d.Origin, Vector3d.ZAxis, 20, 0, Math.PI / 2))
            using (var reference = RoadInteropService.ReadAlignment(arc))
                Check("arc-bulge-preserved", reference != null && Math.Abs(reference.Length - arc.TotalAngle * arc.Radius) < 1e-6 && Math.Abs(reference.GetBulgeAt(0) - Math.Tan(Math.PI / 8)) < 1e-6);

            bool fixture = Environment.GetEnvironmentVariable("GKIN_PROBE_FIXTURE") == "1";
            if (!fixture) CreateFixture();
            bool found = CadEngine.QuetBinhDo(out ObjectId routeId, out double length, out bool estimated);
            int td = CadEngine.QuetTracDocKm(out Extents3d? profile);
            int tn = CadEngine.QuetTracNgang(out _, out List<Extents3d> sections);
            report.Add("DRAWING=" + CadEngine.Doc.Name);
            report.Add("ROUTE=" + routeId + " LENGTH=" + length + " ESTIMATED=" + estimated);
            report.Add("TD=" + td + " EXT=" + profile + " TN=" + tn);
            report.Add("DETECTION_NOTE=" + CadEngine.DetectionNote);
            using (var tr = CadEngine.Db.TransactionManager.StartOpenCloseTransaction())
            {
                var labels = RoadInteropService.ReadLabels(CadEngine.Db, tr);
                report.Add("LABELS=" + labels.Count + " KM=" + labels.Count(x => RoadInteropService.TryStation(x.Text, out _)));
                foreach (var group in labels.GroupBy(x => x.Layer).OrderByDescending(x => x.Count()).Take(15))
                    report.Add("LABEL_LAYER=" + group.Key + " COUNT=" + group.Count());
                foreach (var group in labels.Where(x => RoadInteropService.TryStation(x.Text, out _)).GroupBy(x => x.Layer))
                    report.Add("KM_LAYER=" + group.Key + " COUNT=" + group.Count() + " SAMPLE=" + group.First().Position + " TEXT=" + group.First().Text);
                var table = (BlockTable)tr.GetObject(CadEngine.Db.BlockTableId, OpenMode.ForRead);
                var space = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (var group in space.Cast<ObjectId>().Select(id => tr.GetObject(id, OpenMode.ForRead)).OfType<Entity>()
                    .Where(x => x.Layer.IndexOf("TRACDOC", StringComparison.OrdinalIgnoreCase) >= 0 || x.Layer.IndexOf("PLINETD", StringComparison.OrdinalIgnoreCase) >= 0)
                    .GroupBy(x => x.Layer))
                    report.Add("PROFILE_LAYER=" + group.Key + " COUNT=" + group.Count() + " EXAMPLE=" + group.First().GeometricExtents);
                foreach (var entity in space.Cast<ObjectId>().Select(id => tr.GetObject(id, OpenMode.ForRead)).OfType<Entity>()
                    .Where(x => x.Layer.Equals("PLINETDTN", StringComparison.OrdinalIgnoreCase)).Take(6))
                    report.Add("TD_SEED=" + entity.GeometricExtents);
                foreach (var label in labels.Where(x => RoadInteropService.TryStation(x.Text, out _)).Take(8))
                    report.Add("LABEL_STATION=" + label.Text + " " + label.Position + " " + label.Layer);
                foreach (var entity in space.Cast<ObjectId>().Select(id => tr.GetObject(id, OpenMode.ForRead)).OfType<Entity>()
                    .Where(x => x.Layer.IndexOf("PLINET", StringComparison.OrdinalIgnoreCase) >= 0).Take(8))
                {
                    using (var data = entity.XData)
                        report.Add("SECTION_ENTITY=" + entity.Layer + " " + entity.GeometricExtents + " XDATA="
                            + (data == null ? "none" : string.Join(";", data.AsArray().Select(x => x.TypeCode + ":" + x.Value))));
                }
                if (found)
                {
                    var routeEntity = tr.GetObject(routeId, OpenMode.ForRead) as Entity;
                    report.Add("ROUTE_CLASS=" + routeEntity?.GetRXClass()?.Name + " LAYER=" + routeEntity?.Layer);
                    using (var reference = RoadInteropService.ReadAlignment(tr.GetObject(routeId, OpenMode.ForRead) as Entity))
                    {
                        report.Add("REFERENCE=" + (reference == null ? "unavailable" : reference.Length.ToString()));
                        if (reference != null)
                        {
                            var anchors = RoadInteropService.RouteStations(reference, labels, CadEngine.MetersToDrawingUnits(30));
                            report.Add("ROUTE_STAKES=" + anchors.Count);
                            foreach (var anchor in anchors.Take(10)) report.Add("STAKE=" + anchor.Station + " DIST=" + anchor.Coordinate);
                        }
                    }
                }
            }
            if (fixture)
            {
                Check("fixture-profile-local", profile != null && profile.Value.MaxPoint.Y - profile.Value.MinPoint.Y < 1000);
                if (profile != null)
                {
                    var actualBands = ProfileCutterService.Cut(profile.Value, length, CadEngine.MetersToDrawingUnits(350), 1);
                    report.Add("FIXTURE_TD_BANDS=" + actualBands.Count + " START=" + actualBands.First().From + " END=" + actualBands.Last().To + " REAL=" + actualBands.All(x => x.HasRealStations));
                    report.Add("FIXTURE_HEADER=" + ProfileCutterService.FindHeader(profile.Value));
                    Check("fixture-profile-stations", actualBands.All(x => x.HasRealStations));
                    Check("fixture-profile-heading", ProfileCutterService.FindHeader(profile.Value) != null);
                }
                var fixtureLabels = RoadLabels();
                if (profile != null)
                    foreach (var label in fixtureLabels.Where(x => x.Position.X <= profile.Value.MinPoint.X + 50
                        && x.Position.X >= profile.Value.MinPoint.X - 50 && x.Position.Y >= profile.Value.MinPoint.Y && x.Position.Y <= profile.Value.MaxPoint.Y).Take(24))
                        report.Add("PROFILE_HEADING_LABEL=" + label.Layer + " " + label.Position + " " + label.Text);
                if (sections.Count > 0)
                    foreach (var label in fixtureLabels.Where(x => x.Position.X <= sections[0].MinPoint.X + 3
                        && x.Position.X >= sections[0].MinPoint.X - 15 && x.Position.Y >= sections[0].MinPoint.Y - 1 && x.Position.Y <= sections[0].MaxPoint.Y + 1).Take(24))
                        report.Add("TN_HEADING_LABEL=" + label.Layer + " " + label.Position + " " + label.Text);
                report.Add("FIXTURE_TN_HEADERS=" + sections.Count(x => ProfileCutterService.FindHeader(x, fixtureLabels) != null));
                Check("fixture-tn-heading", sections.Any(x => ProfileCutterService.FindHeader(x, fixtureLabels) != null));
                string fixtureOutput = Environment.GetEnvironmentVariable("GKIN_PROBE_FIXTURE_OUTPUT");
                if (!string.IsNullOrEmpty(fixtureOutput) && profile != null)
                {
                    ObjectId sample = CreateProbeFrame();
                    var actualLayouts = CadEngine.CreateLayouts(sample, null, profile, sections.Take(4).ToList(),
                        4, length, CadEngine.MetersToDrawingUnits(350), false, false, ObjectId.Null, 2, 4, out string actualError);
                    Check("fixture-layout-five-sheets", actualLayouts.Count == 5 && string.IsNullOrEmpty(actualError));
                    report.Add("FIXTURE_LAYOUT_ERROR=" + actualError);
                    Check("fixture-layout-pdf", CadEngine.PlotSheets(actualLayouts.Select(x => new PlotSheetRequest { LayoutId=x.LayoutId }), "DWG To PDF.pc3", fixtureOutput));
                    report.Add("FIXTURE_PDF_ERROR=" + CadEngine.LastError);
                }
                return;
            }
            Check("detect-route", found && !estimated && Math.Abs(length - 1000) < 1e-6);
            Check("detect-td", td == 1 && profile != null);
            Check("detect-tn", tn == 4);
            var bounds = new Extents3d(new Point3d(1970, 80, 0), new Point3d(3000, 155, 0));
            var bands = ProfileCutterService.Cut(bounds, 1000, 350, 3);
            Check("profile-count", bands.Count == 3);
            Check("profile-start-station", bands[0].HasRealStations && bands[0].StartStation == 12000);
            Check("profile-end-station", Math.Abs(bands.Last().EndStation - 13000) < 1e-6);
            var frames = CadEngine.QuetKhung();
            Check("detect-frame", frames.Count > 0);
            var made = CadEngine.CreateModelSheets(frames[0].Sample, CadEngine.BBox(routeId), bounds, sections,
                3, 1000, 350, true, false, true, "GKIN-KHUNG", 0, 4, false,
                new Point3d(7000, 0, 0), routeId, 2, 4, out int createdTd, out string error);
            report.Add("MODEL_ERROR=" + error);
            Check("combined-model-count", made.Count == 4 && createdTd == 3);
            Check("paired-titles", CadEngine.LastTitles.Take(3).All(x => x.Contains("BÌNH ĐỒ + TRẮC DỌC")));
            using (var tr = CadEngine.Db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(CadEngine.Db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                var windows = model.Cast<ObjectId>().Select(id => tr.GetObject(id, OpenMode.ForRead)).OfType<BlockReference>()
                    .Where(x => x.Name.StartsWith("GKIN-VIEW-", StringComparison.OrdinalIgnoreCase)).ToList();
                Check("model-windows-created", windows.Count >= 10);
                Check("model-clip-persistent", windows.All(x => !x.ExtensionDictionary.IsNull
                    && ((DBDictionary)tr.GetObject(x.ExtensionDictionary, OpenMode.ForRead)).Contains("ACAD_FILTER")));
            }
            var layouts = CadEngine.CreateLayouts(frames[0].Sample, CadEngine.BBox(routeId), bounds, null,
                3, 1000, 350, true, false, routeId, 2, 4, out error);
            report.Add("LAYOUT_ERROR=" + error);
            Check("combined-layout-count", layouts.Count == 3 && string.IsNullOrEmpty(error));
            Check("layout-rescan", CadEngine.ScanLayoutSheets().Count == 3);
            Check("layout-title-persistence", CadEngine.ScanLayoutSheets().All(x => x.Title.Contains("Km12+")));
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name.StartsWith("PdfSharp") || x.GetName().Name == "GKIN"))
                report.Add("ASSEMBLY=" + assembly.FullName + " PATH=" + assembly.Location);
            string pdfFolder = Environment.GetEnvironmentVariable("GKIN_PROBE_PDF_DIR");
            if (!string.IsNullOrEmpty(pdfFolder))
            {
                Directory.CreateDirectory(pdfFolder);
                var modelRequests = made.Select(id => { var ext = CadEngine.BBox(id).Value;
                    return new PlotSheetRequest { LayoutId = CadEngine.ModelLayoutId(), Window = new Extents2d(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y) }; }).ToList();
                Check("model-pdf-four-pages", CadEngine.PlotSheets(modelRequests, "DWG To PDF.pc3", Path.Combine(pdfFolder, "model-regression.pdf")));
                report.Add("MODEL_PDF_ERROR=" + CadEngine.LastError);
                Check("layout-pdf-three-pages", CadEngine.PlotSheets(layouts.Select(x => new PlotSheetRequest {LayoutId=x.LayoutId}), "DWG To PDF.pc3", Path.Combine(pdfFolder, "layout-regression.pdf")));
                report.Add("LAYOUT_PDF_ERROR=" + CadEngine.LastError);
            }
            CadEngine.ClearTransientSheets(); CadEngine.ScanModelSheets();
            Check("model-rescan", CadEngine.LastFrames.Count == 4 && CadEngine.LastTitles.Take(3).All(x => x.Contains("Km12+")));
            var blockedHide = CadEngine.CreateModelSheets(frames[0].Sample, CadEngine.BBox(routeId), bounds, null,
                3, 1000, 350, true, false, true, "GKIN-KHUNG", 0, 4, true,
                new Point3d(7000, -1000, 0), routeId, 2, 4, out _, out error);
            Check("existing-viewport-protected", blockedHide.Count == 0 && !string.IsNullOrEmpty(error));
            CadEngine.DeleteLayouts(layouts.Select(x => x.LayoutName));
            var modelAgain = CadEngine.CreateModelSheets(frames[0].Sample, CadEngine.BBox(routeId), bounds, null,
                3, 1000, 350, true, false, true, "GKIN-KHUNG", 0, 4, true,
                new Point3d(7000, -1000, 0), routeId, 2, 4, out _, out error);
            Check("hide-original-success", modelAgain.Count == 3 && string.IsNullOrEmpty(error));
            var modelThird = CadEngine.CreateModelSheets(frames[0].Sample, CadEngine.BBox(routeId), bounds, null,
                3, 1000, 350, true, false, true, "GKIN-KHUNG", 0, 4, false,
                new Point3d(7000, -2000, 0), routeId, 2, 4, out _, out error);
            Check("repeat-after-hide", modelThird.Count == 3 && string.IsNullOrEmpty(error));
            using (var tr = CadEngine.Db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(CadEngine.Db.BlockTableId, OpenMode.ForRead);
                var copies = bt.Cast<ObjectId>().Select(id => (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead))
                    .Where(x => x.Name.StartsWith("GKIN-VIEW-"));
                Check("copied-layer-remains-printable", copies.SelectMany(x => x.Cast<ObjectId>()).Select(id => tr.GetObject(id, OpenMode.ForRead)).OfType<Entity>()
                    .All(x => !x.Layer.Equals("GKIN-NONPLOT", StringComparison.OrdinalIgnoreCase)));
            }
            var hiddenLayouts = CadEngine.CreateLayouts(frames[0].Sample, null, bounds, null,
                3, 1000, 350, false, false, ObjectId.Null, 2, 4, out error);
            Check("hidden-source-layout-rejected", hiddenLayouts.Count == 0 && !string.IsNullOrEmpty(error));
        }
        catch (System.Exception ex) { report.Add("ERROR=" + ex); failures++; }
        finally
        {
            report.Add("FAILURES=" + failures);
            File.WriteAllLines(output, report, Encoding.UTF8);
        }
    }

    static List<RoadInteropService.Label> RoadLabels()
    {
        using (var tr = CadEngine.Db.TransactionManager.StartOpenCloseTransaction()) return RoadInteropService.ReadLabels(CadEngine.Db, tr);
    }

    static ObjectId CreateProbeFrame()
    {
        using (var tr = CadEngine.Db.TransactionManager.StartTransaction())
        {
            var bt = (BlockTable)tr.GetObject(CadEngine.Db.BlockTableId, OpenMode.ForWrite);
            var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var definition = new BlockTableRecord {Name="KHUNG_GKIN_PROBE_"+Guid.NewGuid().ToString("N")};
            bt.Add(definition); tr.AddNewlyCreatedDBObject(definition, true);
            var outline = new Polyline();
            foreach(var p in new[]{new Point2d(0,0),new Point2d(420,0),new Point2d(420,297),new Point2d(0,297)}) outline.AddVertexAt(outline.NumberOfVertices,p,0,0,0);
            outline.Closed=true; definition.AppendEntity(outline);tr.AddNewlyCreatedDBObject(outline,true);
            var reference = new BlockReference(Point3d.Origin,definition.ObjectId);
            model.AppendEntity(reference);tr.AddNewlyCreatedDBObject(reference,true);tr.Commit();return reference.ObjectId;
        }
    }

    static void CreateFixture()
    {
        var db = CadEngine.Db;
        using (var tr = db.TransactionManager.StartTransaction())
        {
            db.Insunits = UnitsValue.Meters;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
            foreach (string name in new[] { "TIM", "TRACDOCTHIETKE", "PLINETNTN", "LABEL" })
                if (!lt.Has(name)) { var layer = new LayerTableRecord {Name=name}; lt.Add(layer); tr.AddNewlyCreatedDBObject(layer, true); }
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var route = new Polyline(); route.AddVertexAt(0, Point2d.Origin, 0, 0, 0); route.AddVertexAt(1,new Point2d(1000,0),0,0,0); route.Layer="TIM";
            ms.AppendEntity(route); tr.AddNewlyCreatedDBObject(route,true);
            var terrain = new Polyline(); terrain.AddVertexAt(0,new Point2d(2000,140),0,0,0); terrain.AddVertexAt(1,new Point2d(3000,145),0,0,0); terrain.Layer="TRACDOCTHIETKE";
            ms.AppendEntity(terrain); tr.AddNewlyCreatedDBObject(terrain,true);
            for(int i=0;i<=4;i++) { Text(ms,tr,CadEngine.LyTrinh(12000+i*250),i*250,2); Text(ms,tr,CadEngine.LyTrinh(12000+i*250),2000+i*250,100); }
            Text(ms,tr,"CAO ĐỘ TỰ NHIÊN",1972,85); Text(ms,tr,"LÝ TRÌNH",1972,95);
            var ra=(RegAppTable)tr.GetObject(db.RegAppTableId,OpenMode.ForWrite);
            if(!ra.Has("KS_TN")){var app=new RegAppTableRecord{Name="KS_TN"};ra.Add(app);tr.AddNewlyCreatedDBObject(app,true);}
            for(int i=0;i<4;i++)
            {
                var section=new Polyline();section.AddVertexAt(0,new Point2d(4000+i*140,200),0,0,0);section.AddVertexAt(1,new Point2d(4100+i*140,205),0,0,0);section.Layer="PLINETNTN";
                ms.AppendEntity(section);tr.AddNewlyCreatedDBObject(section,true);
                using(var data=new ResultBuffer(new TypedValue(1001,"KS_TN")))section.XData=data;
                Text(ms,tr,"Km12+"+(i*20).ToString("000"),4020+i*140,215);
            }
            Text(ms,tr,"CAO ĐỘ TỰ NHIÊN",3972,200);
            var frameDef=new BlockTableRecord{Name="KHUNG_GKIN_PROBE"};bt.Add(frameDef);tr.AddNewlyCreatedDBObject(frameDef,true);
            var outline=new Polyline();foreach(var point in new[]{new Point2d(0,0),new Point2d(420,0),new Point2d(420,297),new Point2d(0,297)})outline.AddVertexAt(outline.NumberOfVertices,point,0,0,0);outline.Closed=true;
            frameDef.AppendEntity(outline);tr.AddNewlyCreatedDBObject(outline,true);
            var frame=new BlockReference(new Point3d(5500,0,0),frameDef.ObjectId);ms.AppendEntity(frame);tr.AddNewlyCreatedDBObject(frame,true);
            tr.Commit();
        }
    }
    static void Text(BlockTableRecord owner,Transaction tr,string value,double x,double y)
    {
        var text=new DBText{TextString=value,Position=new Point3d(x,y,0),Height=1,Layer="LABEL"};owner.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);
    }
}
