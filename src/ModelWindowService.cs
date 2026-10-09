using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    public static class ModelWindowService
    {
        public static ObjectId Create(Database db, Transaction tr, BlockTableRecord model,
            IList<ObjectId> sources, Matrix3d transform, Extents2d target, ObjectId layerId, Extents3d sourceWindow)
        {
            if (sources.Count == 0) return ObjectId.Null;
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var definition = new BlockTableRecord { Name = "GKIN-VIEW-" + Guid.NewGuid().ToString("N") };
            table.Add(definition); tr.AddNewlyCreatedDBObject(definition, true);
            var ids = new ObjectIdCollection();
            foreach (ObjectId id in sources) ids.Add(id);
            // Deep cloning preserves attributes, nested native objects and
            // extension data. Source objects remain untouched until commit.
            var mapping = new IdMapping();
            db.DeepCloneObjects(ids, definition.ObjectId, mapping, false);
            foreach (IdPair pair in mapping)
                if (pair.IsCloned && tr.GetObject(pair.Value, OpenMode.ForWrite, false) is Entity copy)
                    SheetMetadataService.RestoreCopyLayer(copy, tr);
            var reference = new BlockReference(Point3d.Origin, definition.ObjectId) { LayerId = layerId };
            model.AppendEntity(reference); tr.AddNewlyCreatedDBObject(reference, true);
            reference.TransformBy(transform);

            Matrix3d inverse = transform.Inverse();
            var polygon = new List<Point2d>();
            foreach (Point2d corner in new[] { target.MinPoint, new Point2d(target.MaxPoint.X, target.MinPoint.Y),
                target.MaxPoint, new Point2d(target.MinPoint.X, target.MaxPoint.Y) })
            {
                var local = new Point3d(corner.X, corner.Y, 0).TransformBy(inverse);
                polygon.Add(new Point2d(local.X, local.Y));
            }
            polygon = Clip(polygon, p => p.X - sourceWindow.MinPoint.X);
            polygon = Clip(polygon, p => sourceWindow.MaxPoint.X - p.X);
            polygon = Clip(polygon, p => p.Y - sourceWindow.MinPoint.Y);
            polygon = Clip(polygon, p => sourceWindow.MaxPoint.Y - p.Y);
            if (polygon.Count < 3) throw new InvalidOperationException("Cửa sổ nguồn và vùng in không giao nhau.");
            var boundary = new Point2dCollection(polygon.ToArray());
            var filter = new SpatialFilter
            {
                Definition = new SpatialFilterDefinition(boundary, Vector3d.ZAxis, 0, 0, 0, true)
            };
            reference.CreateExtensionDictionary();
            var extension = (DBDictionary)tr.GetObject(reference.ExtensionDictionary, OpenMode.ForWrite);
            var filters = new DBDictionary();
            extension.SetAt("ACAD_FILTER", filters); tr.AddNewlyCreatedDBObject(filters, true);
            filters.SetAt("SPATIAL", filter); tr.AddNewlyCreatedDBObject(filter, true);
            return reference.ObjectId;
        }

        static List<Point2d> Clip(List<Point2d> polygon, Func<Point2d, double> distance)
        {
            var output = new List<Point2d>();
            if (polygon.Count == 0) return output;
            var previous = polygon[polygon.Count - 1]; double prev = distance(previous);
            foreach (var current in polygon)
            {
                double next = distance(current);
                if ((prev >= 0) != (next >= 0))
                {
                    double t = prev / (prev - next);
                    output.Add(new Point2d(previous.X + (current.X - previous.X) * t, previous.Y + (current.Y - previous.Y) * t));
                }
                if (next >= 0) output.Add(current);
                previous = current; prev = next;
            }
            return output;
        }
    }
}
