using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GKIN
{
    public static partial class CadEngine
    {
        public static List<ObjectId> CreateModelSheets(
            ObjectId sampleFrame, Extents3d? bd, Extents3d? td, IList<Extents3d> tnItems,
            int tdCount, double tdLength, double tdStep, bool mergeBdTd, bool verticalTn, bool rowLayout,
            string layerName, double overlap, int sheetsPerRow, bool hideCopiedGeometry,
            Point3d? origin,
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

            var plans = BuildSheetPlans(bd, td, tnItems, tdCount, tdLength, tdStep, mergeBdTd, verticalTn);
            if (plans.Count == 0)
            {
                error = "Không tìm thấy vùng nguồn bình đồ, trắc dọc hoặc trắc ngang.";
                return result;
            }

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

                    var sampleExt = sample.GeometricExtents;
                    double frameWidth = Math.Max(1.0, sampleExt.MaxPoint.X - sampleExt.MinPoint.X);
                    double frameHeight = Math.Max(1.0, sampleExt.MaxPoint.Y - sampleExt.MinPoint.Y);
                    Extents3d? drawingExt = null;
                    foreach (ObjectId id in model)
                        if (tr.GetObject(id, OpenMode.ForRead, false) is Entity entity && TryExtents(entity, out Extents3d ext)) drawingExt = Union(drawingExt, ext);

                    double startX = origin?.X ?? ((drawingExt?.MaxPoint.X ?? 0) + frameWidth * 0.30);
                    double startY = origin?.Y ?? (drawingExt?.MaxPoint.Y ?? 0);
                    int perRow = rowLayout ? Math.Max(1, sheetsPerRow) : Math.Max(1, plans.Count);
                    double gapX = frameWidth * 0.10;
                    double gapY = frameHeight * 0.15;

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

                        Extents3d targetFrame = frame.GeometricExtents;
                        double usableWidth = frameWidth * 0.76;
                        double usableHeight = frameHeight * 0.88;
                        double left = targetFrame.MinPoint.X + frameWidth * 0.04;
                        double bottom = targetFrame.MinPoint.Y + frameHeight * 0.06;
                        int windowCount = Math.Max(1, plans[index].Windows.Count);
                        bool horizontal = plans[index].Type == "TN" && !plans[index].StackVertical && windowCount > 1;
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
                                if (tr.GetObject(sourceId, OpenMode.ForRead, false) is not Entity source || !TryExtents(source, out Extents3d sourceExt) || !Intersects2d(sourceWindow, sourceExt)) continue;
                                if (source is BlockReference block && string.Equals(EffectiveName(block), EffectiveName(sample), StringComparison.OrdinalIgnoreCase)) continue;
                                if (source.Clone() is not Entity clone) continue;
                                clone.TransformBy(transform);
                                if (!hiddenLayerId.IsNull) clone.LayerId = hiddenLayerId;
                                model.AppendEntity(clone);
                                tr.AddNewlyCreatedDBObject(clone, true);
                            }
                        }
                    }
                    tr.Commit();
                    Db.TransactionManager.QueueForGraphicsFlush();
                }
                catch (System.Exception ex)
                {
                    result.Clear();
                    tdCreated = 0;
                    error = ex.Message;
                }
            }
            return result;
        }
    }
}
