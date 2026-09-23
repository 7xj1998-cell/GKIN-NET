using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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

    public static class CadEngine
    {
        public static Document Doc => AcadApp.DocumentManager.MdiActiveDocument;
        public static Editor Ed => Doc?.Editor;
        public static Database Db => Doc?.Database;

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

        public static int QuetTracDocKm()
        {
            int n = 0;
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    var ent = tr.GetObject(eid, OpenMode.ForRead);
                    string s = ent switch
                    {
                        DBText t => t.TextString,
                        MText m => m.Contents,
                        _ => null
                    };
                    if (s != null && s.ToUpperInvariant().Contains("KM")) n++;
                }
                tr.Commit();
            }
            return n;
        }

        public static int QuetTracNgang()
        {
            int n = 0;
            if (Db == null) return 0;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    if (tr.GetObject(eid, OpenMode.ForRead) is not BlockReference br) continue;
                    string nm = EffectiveName(br).ToUpperInvariant();
                    if (nm.Contains("TN") || nm.Contains("TNCT") || nm.Contains("MATCAT") || nm.Contains("TRACNGANG"))
                        n++;
                }
                tr.Commit();
            }
            return n;
        }

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
                    list.Add((eid, br.Position.X, br.Position.Y, 0));
                }
                tr.Commit();
            }
            return list.OrderByDescending(a => a.y).ThenBy(a => a.x).Select(a => a.id).ToList();
        }

        public static List<string> Tags(ObjectId id)
        {
            var tags = new List<string>();
            if (id.IsNull) return tags;
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

        public static bool GanAttr(ObjectId id, string tag, string val)
        {
            if (id.IsNull || string.IsNullOrEmpty(tag) || val == null) return false;
            bool ok = false;
            using (Doc.LockDocument())
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(id, OpenMode.ForWrite) is BlockReference br)
                {
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        if (tr.GetObject(aid, OpenMode.ForWrite) is AttributeReference ar
                            && string.Equals(ar.Tag, tag, StringComparison.OrdinalIgnoreCase))
                        {
                            ar.TextString = val;
                            ok = true;
                        }
                    }
                }
                tr.Commit();
            }
            return ok;
        }

        public static Extents3d? BBox(ObjectId id)
        {
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
            var bb = BBox(frame);
            if (bb == null || Doc == null) return false;
            try
            {
                using (Doc.LockDocument())
                {
                    string cmd =
                        $"(command \"_.-PLOT\" \"_Y\" \"\" \"{pc3}\" \"\" \"_M\" \"_I\" \"_N\" \"_W\" " +
                        $"\"{bb.Value.MinPoint.X},{bb.Value.MinPoint.Y}\" " +
                        $"\"{bb.Value.MaxPoint.X},{bb.Value.MaxPoint.Y}\" " +
                        $"\"_F\" \"_C\" \"_Y\" \"{ctb}\" \"_Y\" \"_N\" \"_N\" \"_N\" \"{file.Replace("\\", "/")}\" \"_N\" \"_Y\") ";
                    Doc.SendStringToExecute(cmd + "\n", true, false, false);
                }
                return true;
            }
            catch { return false; }
        }
    }
}
