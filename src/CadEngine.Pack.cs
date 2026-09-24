using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    public static partial class CadEngine
    {
        public const string PackedLayoutName = "IN-BDTDTN";

        public static List<LayoutSheetInfo> CreatePackedLayout(
            ObjectId sampleFrame, Extents3d? bd, Extents3d? td, IList<Extents3d> tnItems,
            int tdCount, double tdLength, double tdStep, bool mergeBdTd, bool verticalTn,
            ObjectId bdCurve, int bdPerSheet, int tnPerSheet, int sheetsPerRow, out string error)
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
                error = "Không tìm thấy vùng nguồn BĐ/TĐ/TN để cắt.";
                return result;
            }

            int columns = Math.Max(1, sheetsPerRow);
            if (columns < 2)
            {
                int nbd = plans.Count(p => p.Type == "BD");
                int ntd = plans.Count(p => p.Type == "TD");
                columns = Math.Max(1, Math.Max(nbd, ntd));
                if (columns > 8) columns = 8;
            }

            string originalLayout = LayoutManager.Current.CurrentLayout;
            using (Doc.LockDocument())
            {
                try
                {
                    RecreateLayout(PackedLayoutName);
                    LayoutManager.Current.CurrentLayout = PackedLayoutName;
                    ObjectId layoutId;
                    using (var idTr = Db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var layouts = (DBDictionary)idTr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
                        layoutId = (ObjectId)layouts.GetAt(PackedLayoutName);
                    }

                    var viewportIds = new List<ObjectId>();
                    using (var tr = Db.TransactionManager.StartTransaction())
                    {
                        var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                        var paper = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);
                        foreach (ObjectId id in paper)
                        {
                            if (tr.GetObject(id, OpenMode.ForWrite, false) is Viewport old && old.Number > 1)
                                old.Erase();
                        }

                        var sample = (BlockReference)tr.GetObject(sampleFrame, OpenMode.ForRead);
                        ObjectId definitionId = sample.IsDynamicBlock
                            ? sample.DynamicBlockTableRecord
                            : sample.BlockTableRecord;
                        if (!TrySampleExtents(sample, tr, out Extents3d sampleExt))
                            throw new InvalidOperationException("Khung mẫu không có kích thước.");

                        double frameWidth = Math.Max(1.0, sampleExt.MaxPoint.X - sampleExt.MinPoint.X);
                        double frameHeight = Math.Max(1.0, sampleExt.MaxPoint.Y - sampleExt.MinPoint.Y);
                        double gapX = frameWidth * 0.04;
                        double gapY = frameHeight * 0.08;

                        var rows = new List<List<SheetPlan>>();
                        foreach (string type in new[] { "BD", "TD", "TN" })
                        {
                            var ofType = plans.Where(p => p.Type == type).ToList();
                            for (int i = 0; i < ofType.Count; i += columns)
                                rows.Add(ofType.Skip(i).Take(columns).ToList());
                        }

                        Extents3d? paperBounds = null;
                        int rowIndex = 0;
                        foreach (var row in rows)
                        {
                            for (int col = 0; col < row.Count; col++)
                            {
                                var plan = row[col];
                                var origin = new Point3d(col * (frameWidth + gapX), -(rowIndex * (frameHeight + gapY)), 0);
                                var frame = new BlockReference(sample.Position, definitionId)
                                {
                                    ScaleFactors = sample.ScaleFactors,
                                    Rotation = sample.Rotation
                                };
                                paper.AppendEntity(frame);
                                tr.AddNewlyCreatedDBObject(frame, true);
                                if (!TrySampleExtents(frame, tr, out Extents3d frameExt))
                                    frameExt = new Extents3d(Point3d.Origin, new Point3d(frameWidth, frameHeight, 0));
                                frame.TransformBy(Matrix3d.Displacement(origin - frameExt.MinPoint));
                                AddAttributes(frame, sample, definitionId, tr);
                                if (!TrySampleExtents(frame, tr, out frameExt))
                                    frameExt = new Extents3d(origin, origin + new Vector3d(frameWidth, frameHeight, 0));
                                paperBounds = Union(paperBounds, frameExt);

                                PlacePlanViewports(paper, tr, plan, frameExt, viewportIds);
                                result.Add(new LayoutSheetInfo
                                {
                                    LayoutName = PackedLayoutName,
                                    Type = plan.Type,
                                    Index = plan.Index,
                                    LayoutId = layoutId,
                                    FrameId = frame.ObjectId
                                });
                            }
                            rowIndex++;
                        }

                        if (paperBounds != null)
                        {
                            using (var settings = BuildPlotSettings(layout, null,
                                new Extents2d(paperBounds.Value.MinPoint.X, paperBounds.Value.MinPoint.Y,
                                    paperBounds.Value.MaxPoint.X, paperBounds.Value.MaxPoint.Y),
                                "DWG To PDF.pc3", null))
                                layout.CopyFrom(settings);
                        }
                        tr.Commit();
                    }

                    LayoutManager.Current.CurrentLayout = PackedLayoutName;
                    ActivateViewports(viewportIds, PackedLayoutName);
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

        static void PlacePlanViewports(BlockTableRecord paper, Transaction tr, SheetPlan plan, Extents3d frameExt, List<ObjectId> viewportIds)
        {
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
            var weights = new double[windowCount];
            double weightSum = 0;
            for (int w = 0; w < windowCount; w++)
            {
                weights[w] = w < plan.Span.Count && plan.Span[w] > 1 ? plan.Span[w] : 1;
                weightSum += weights[w];
            }
            if (weightSum < 1e-6) weightSum = windowCount;
            double viewportWidth = usableWidth * 0.98;
            double viewportHeight = horizontal
                ? usableHeight * 0.96
                : (plan.Type == "BD" ? usableHeight / slots : windowCount > 1 ? usableHeight / windowCount : usableHeight) * 0.94;
            double usedHeight = horizontal ? viewportHeight : viewportHeight * windowCount;
            double yBase = bottom + Math.Max(0, usableHeight - usedHeight) / 2.0;

            for (int i = 0; i < plan.Windows.Count; i++)
            {
                var source = plan.Windows[i];
                double thisW = horizontal ? usableWidth * 0.98 * weights[i] / weightSum : viewportWidth;
                double xCursor = left;
                if (horizontal)
                    for (int w = 0; w < i; w++) xCursor += usableWidth * 0.98 * weights[w] / weightSum;
                double centerX = horizontal ? xCursor + thisW / 2.0 : left + usableWidth / 2.0;
                double centerY = horizontal
                    ? yBase + viewportHeight / 2.0
                    : yBase + viewportHeight * (windowCount - 1 - i) + viewportHeight / 2.0;
                Point3d look = i < plan.Look.Count
                    ? plan.Look[i]
                    : new Point3d((source.MinPoint.X + source.MaxPoint.X) / 2.0, (source.MinPoint.Y + source.MaxPoint.Y) / 2.0, 0);
                double twist = i < plan.Twist.Count ? plan.Twist[i] : 0;
                double span = i < plan.Span.Count ? plan.Span[i] : 0;
                var viewport = LayoutViewportService.Create(
                    paper, tr, new Point3d(centerX, centerY, 0),
                    horizontal ? thisW : viewportWidth, viewportHeight,
                    source, look, twist, span);
                viewportIds.Add(viewport.ObjectId);
            }
        }

        static void RecreateLayout(string name)
        {
            var lm = LayoutManager.Current;
            string current = lm.CurrentLayout;
            bool exists;
            using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
            {
                var layouts = (DBDictionary)tr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
                exists = layouts.Contains(name);
            }
            if (exists)
            {
                if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
                {
                    using (var tr = Db.TransactionManager.StartOpenCloseTransaction())
                    {
                        var layouts = (DBDictionary)tr.GetObject(Db.LayoutDictionaryId, OpenMode.ForRead);
                        foreach (DBDictionaryEntry entry in layouts)
                        {
                            if (!string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(entry.Key, "Model", StringComparison.OrdinalIgnoreCase))
                            {
                                lm.CurrentLayout = entry.Key;
                                break;
                            }
                        }
                    }
                }
                try { lm.DeleteLayout(name); }
                catch { }
            }
            lm.CreateLayout(name);
        }
    }
}
