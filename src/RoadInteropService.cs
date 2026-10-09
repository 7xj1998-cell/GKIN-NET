using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace GKIN
{
    // Adapted from the BHT Tdt91Alignment/Tdt91Stakes approach: temporary
    // geometry only. No TDT assembly reference and no writes to source entities.
    public static class RoadInteropService
    {
        static readonly Regex StationRx = new Regex(@"(?<![A-Z0-9])KM\s*(\d{1,5})\s*\+\s*(\d{1,3}(?:[.,]\d+)?)(?![\d.,])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public sealed class Label
        {
            public string Text;
            public string Layer;
            public ObjectId OwnerId;
            public Point3d Position;
            public Extents3d Bounds;
            public Point3d? StakeAnchor;
            public Vector3d? StakeDirection;
        }

        public sealed class Anchor
        {
            public double Station;
            public double Coordinate;
        }

        public static bool TryStation(string text, out double metres)
        {
            metres = 0;
            var match = StationRx.Match(text ?? "");
            if (!match.Success) return false;
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double km)
                || !double.TryParse(match.Groups[2].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double m)
                || m >= 1000) return false;
            metres = km * 1000 + m;
            return true;
        }

        public static List<Label> ReadLabels(Database db, Transaction tr)
        {
            var result = new List<Label>();
            var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var model = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            int budget = 500000;
            foreach (ObjectId id in model)
            {
                if (tr.GetObject(id, OpenMode.ForRead, false) is Entity entity)
                {
                    if (SheetMetadataService.TryRead(entity, tr, out _, out _, out _)) continue;
                    ReadLabels(entity, tr, Matrix3d.Identity, id, SheetMetadataService.SourceLayer(entity, tr), new HashSet<ObjectId>(), 0, result, ref budget);
                }
                if (budget <= 0) throw new InvalidOperationException("Bản vẽ có quá nhiều nhãn để dò đầy đủ; hãy chọn vùng bản vẽ cần xuất.");
            }
            return result;
        }

        static void ReadLabels(Entity entity, Transaction tr, Matrix3d transform, ObjectId owner, string layer,
            HashSet<ObjectId> ancestors, int depth, List<Label> labels, ref int budget, Point3d? anchor = null, Vector3d? tickDirection = null)
        {
            if (entity == null || depth > 12 || --budget < 0) return;
            if (entity is not AttributeDefinition && (entity is DBText || entity is MText))
            {
                string text = entity is DBText t ? t.TextString : ((MText)entity).Text;
                Point3d position = entity is DBText dt ? dt.Position : ((MText)entity).Location;
                if (entity is DBText aligned && (aligned.HorizontalMode != TextHorizontalMode.TextLeft || aligned.VerticalMode != TextVerticalMode.TextBase))
                    position = aligned.AlignmentPoint;
                position = position.TransformBy(transform);
                var bounds = new Extents3d(position, position);
                try
                {
                    bounds = entity.GeometricExtents;
                    bounds.TransformBy(transform);
                }
                catch { }
                labels.Add(new Label { Text = text, Position = position, Bounds = bounds, OwnerId = owner,
                    Layer = layer, StakeAnchor = anchor?.TransformBy(transform), StakeDirection = tickDirection?.TransformBy(transform) });
                return;
            }
            if (entity is BlockReference block)
            {
                foreach (ObjectId id in block.AttributeCollection)
                    ReadLabels(tr.GetObject(id, OpenMode.ForRead, false) as Entity, tr, transform, owner, layer,
                        ancestors, depth + 1, labels, ref budget);
                if (!ancestors.Add(block.BlockTableRecord)) return;
                try
                {
                    var definition = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                    if ((definition.Name ?? "").StartsWith("GKIN-VIEW-", StringComparison.OrdinalIgnoreCase)) return;
                    foreach (ObjectId id in definition)
                        ReadLabels(tr.GetObject(id, OpenMode.ForRead, false) as Entity, tr, transform * block.BlockTransform,
                            owner, layer, ancestors, depth + 1, labels, ref budget);
                }
                finally { ancestors.Remove(block.BlockTableRecord); }
                return;
            }
            string rx;
            try { rx = entity.GetRXClass()?.Name ?? ""; } catch { return; }
            if (entity is ProxyEntity || (!rx.Contains("Tdt") && !rx.Contains("TDT") && !rx.ToUpperInvariant().Contains("ADS"))) return;
            var pieces = new DBObjectCollection();
            try
            {
                entity.Explode(pieces);
                for (int i = 0; i < pieces.Count; i++)
                {
                    Point3d? stake = null;
                    Vector3d? direction = null;
                    if (pieces[i] is DBText label && TryStation(label.TextString, out _))
                    {
                        // BHT: the tick preceding a TDT label gives the actual
                        // stake position, rather than the displaced text origin.
                        for (int k = i - 1; k >= Math.Max(0, i - 4); k--)
                            if (pieces[k] is Line tick) { direction = tick.EndPoint - tick.StartPoint; stake = tick.StartPoint + direction.Value * 0.5; break; }
                    }
                    ReadLabels(pieces[i] as Entity, tr, transform, owner, layer, ancestors, depth + 1, labels, ref budget, stake, direction);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { }
            finally { foreach (DBObject piece in pieces) piece.Dispose(); }
        }

        public static Polyline ReadAlignment(Entity source)
        {
            if (source == null) return null;
            try
            {
                if (source is Polyline polyline) return polyline.Closed ? null : (Polyline)polyline.Clone();
                if (source is Curve curve)
                {
                    if (curve.Closed) return null;
                    var converted = ToPolyline(curve);
                    if (converted != null) return converted;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { }
            if (source is ProxyEntity) return null;
            var exploded = new DBObjectCollection();
            Polyline chain = null;
            try
            {
                source.Explode(exploded);
                var curves = exploded.Cast<DBObject>().OfType<Curve>().Where(c => !c.Closed && Length(c) > 1e-6)
                    .OrderByDescending(Length).ToList();
                if (curves.Count == 0) return null;
                // Seed from the longest curve, extend BOTH ends using common
                // endpoints, prefer the same layer. Preserve arc bulges.
                chain = ToPolyline(curves[0]);
                if (chain == null) return null;
                var used = new HashSet<Curve> { curves[0] };
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (bool atEnd in new[] { true, false })
                    {
                        Point3d tip = atEnd ? chain.EndPoint : chain.StartPoint;
                        Curve next = curves.Where(c => !used.Contains(c)
                            && (tip.DistanceTo(c.StartPoint) < 1e-4 || tip.DistanceTo(c.EndPoint) < 1e-4))
                            .OrderByDescending(c => string.Equals(c.Layer, curves[0].Layer, StringComparison.OrdinalIgnoreCase))
                            .ThenByDescending(Length).FirstOrDefault();
                        if (next == null) continue;
                        using (var piece = ToPolyline(next))
                        {
                            if (piece == null) { used.Add(next); continue; }
                            if (atEnd ? tip.DistanceTo(piece.EndPoint) < 1e-4 : tip.DistanceTo(piece.StartPoint) < 1e-4) piece.ReverseCurve();
                            Append(chain, piece, atEnd);
                        }
                        used.Add(next);
                        changed = true;
                    }
                }
                return chain;
            }
            catch { chain?.Dispose(); return null; }
            finally { foreach (DBObject item in exploded) item.Dispose(); }
        }

        static void Append(Polyline chain, Polyline piece, bool atEnd)
        {
            if (atEnd)
            {
                chain.SetBulgeAt(chain.NumberOfVertices - 1, piece.GetBulgeAt(0));
                for (int i = 1; i < piece.NumberOfVertices; i++)
                    chain.AddVertexAt(chain.NumberOfVertices, piece.GetPoint2dAt(i), piece.GetBulgeAt(i), 0, 0);
            }
            else
            {
                for (int i = piece.NumberOfVertices - 2; i >= 0; i--)
                    chain.AddVertexAt(0, piece.GetPoint2dAt(i), piece.GetBulgeAt(i), 0, 0);
            }
        }

        static double Length(Curve curve)
        {
            try { return Math.Abs(curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam)); }
            catch { return 0; }
        }

        static Polyline ToPolyline(Curve curve)
        {
            if (curve is Polyline lw) return lw.Closed ? null : (Polyline)lw.Clone();
            var result = new Polyline();
            if (curve is Line line)
            {
                result.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                result.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
            }
            else if (curve is Arc arc)
            {
                double sweep = arc.EndAngle - arc.StartAngle;
                while (sweep <= 0) sweep += 2 * Math.PI;
                result.AddVertexAt(0, new Point2d(arc.StartPoint.X, arc.StartPoint.Y), Math.Tan(sweep / 4) * (arc.Normal.Z < 0 ? -1 : 1), 0, 0);
                result.AddVertexAt(1, new Point2d(arc.EndPoint.X, arc.EndPoint.Y), 0, 0, 0);
            }
            else
            {
                double length = Length(curve);
                if (length <= 1e-6) { result.Dispose(); return null; }
                int segments = Math.Max(16, Math.Min(3000, (int)Math.Ceiling(length / 0.5)));
                for (int i = 0; i <= segments; i++)
                {
                    Point3d p;
                    if (i == 0) p = curve.StartPoint;
                    else if (i == segments) p = curve.EndPoint;
                    else
                    {
                        try { p = curve.GetPointAtDist(curve.GetDistanceAtParameter(curve.StartParam) + length * i / segments); }
                        catch { result.Dispose(); return null; }
                    }
                    result.AddVertexAt(i, new Point2d(p.X, p.Y), 0, 0, 0);
                }
            }
            return result;
        }

        public static List<Anchor> RouteStations(Polyline route, IEnumerable<Label> labels, double maxOffset)
        {
            var candidates = new List<(Anchor Anchor, double Offset)>();
            foreach (Label label in labels)
            {
                if (!TryStation(label.Text, out double station)) continue;
                Point3d position = label.Position;
                if (label.StakeAnchor.HasValue && label.StakeDirection.HasValue && label.StakeDirection.Value.Length > 1e-6)
                {
                    var tickCenter = label.StakeAnchor.Value;
                    var foot = route.GetClosestPointTo(tickCenter, false);
                    var tangent = route.GetFirstDerivative(foot);
                    // A preceding line is not necessarily a stake. Accept only
                    // a tick on the alignment and perpendicular to its tangent.
                    if (tickCenter.DistanceTo(foot) <= CadEngine.MetersToDrawingUnits(0.02)
                        && tangent.Length > 1e-6 && Math.Abs(tangent.GetNormal().DotProduct(label.StakeDirection.Value.GetNormal())) < 0.15)
                        position = tickCenter;
                }
                Point3d closest = route.GetClosestPointTo(position, false);
                double offset = new Point2d(position.X, position.Y).GetDistanceTo(new Point2d(closest.X, closest.Y));
                if (offset > maxOffset) continue;
                candidates.Add((new Anchor { Station = station, Coordinate = route.GetDistAtPoint(closest) }, offset));
            }
            return candidates.OrderBy(x => x.Offset).GroupBy(x => Math.Round(x.Anchor.Station, 3))
                .Select(x => x.First().Anchor).OrderBy(x => x.Coordinate).ToList();
        }

        public static double Interpolate(IList<Anchor> anchors, double station)
        {
            if (anchors == null || anchors.Count < 2) throw new InvalidOperationException("Cần ít nhất hai cọc có lý trình để ghép bình đồ và trắc dọc.");
            var sorted = anchors.OrderBy(x => x.Station).ToList();
            int right = 1;
            while (right < sorted.Count - 1 && sorted[right].Station < station) right++;
            var a = sorted[right - 1]; var b = sorted[right];
            if (b.Station - a.Station < 1e-6) throw new InvalidOperationException("Lý trình cọc bị trùng; hãy chọn lại tuyến.");
            return a.Coordinate + (b.Coordinate - a.Coordinate) * (station - a.Station) / (b.Station - a.Station);
        }

        public static bool Monotonic(IList<Anchor> anchors)
        {
            if (anchors == null || anchors.Count < 2) return false;
            var sorted = anchors.OrderBy(x => x.Station).ToList();
            double sign = Math.Sign(sorted[1].Coordinate - sorted[0].Coordinate);
            return sign != 0 && sorted.Zip(sorted.Skip(1), (a, b) => b.Station > a.Station
                && Math.Sign(b.Coordinate - a.Coordinate) == sign).All(x => x);
        }

        public static bool MatchesRouteDistances(IList<Anchor> anchors)
        {
            if (!Monotonic(anchors)) return false;
            var sorted = anchors.OrderBy(x => x.Station).ToList();
            return sorted.Zip(sorted.Skip(1), (a, b) =>
            {
                double expected = CadEngine.MetersToDrawingUnits(b.Station - a.Station);
                return Math.Abs(Math.Abs(b.Coordinate - a.Coordinate) - expected)
                    <= Math.Max(CadEngine.MetersToDrawingUnits(0.5), expected * 0.005);
            }).All(x => x);
        }
    }
}
