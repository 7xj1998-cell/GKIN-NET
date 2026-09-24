using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    /// <summary>
    /// Tạo viewport paperspace, gán tâm nhìn / tỷ lệ / TwistAngle theo hướng tuyến.
    /// </summary>
    public static class LayoutViewportService
    {
        public static Viewport Create(
            BlockTableRecord paper,
            Transaction tr,
            Point3d paperCenter,
            double paperWidth,
            double paperHeight,
            Extents3d source,
            Point3d look,
            double twist,
            double span)
        {
            var viewport = new Viewport
            {
                CenterPoint = paperCenter,
                Width = Math.Max(1, paperWidth),
                Height = Math.Max(1, paperHeight),
                ViewCenter = Point2d.Origin,
                ViewTarget = look,
                ViewDirection = Vector3d.ZAxis,
                TwistAngle = twist
            };
            double sourceWidth = Math.Max(1e-6, source.MaxPoint.X - source.MinPoint.X);
            double sourceHeight = Math.Max(1e-6, source.MaxPoint.Y - source.MinPoint.Y);
            double viewHeight = span > 1
                ? span * viewport.Height / viewport.Width
                : Math.Max(sourceHeight, sourceWidth / (viewport.Width / viewport.Height)) * 1.03;
            viewport.ViewHeight = Math.Max(1e-6, viewHeight);
            paper.AppendEntity(viewport);
            tr.AddNewlyCreatedDBObject(viewport, true);
            return viewport;
        }

        public static double AlignmentTwist(Point3d from, Point3d to) =>
            -Math.Atan2(to.Y - from.Y, to.X - from.X);

        public static double CustomScale(string type, double requested)
        {
            if (requested > 1) return 1.0 / requested;
            if (requested > 0) return requested;
            return type == "TN" ? 1.0 / 200.0 : 1.0 / 1000.0;
        }
    }
}
