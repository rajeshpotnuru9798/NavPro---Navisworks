using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NavPro.Backend.Core
{
    public struct AlignmentPoint
    {
        public double E, N;
        public AlignmentPoint(double e, double n) { E = e; N = n; }
        public static double Distance(AlignmentPoint a, AlignmentPoint b) => Math.Sqrt((a.E - b.E) * (a.E - b.E) + (a.N - b.N) * (a.N - b.N));
    }
    public sealed class AlignmentProjection
    {
        public double Station, Offset, Distance, Easting, Northing;
        public string Note;
    }
    public sealed class HorizontalSegment
    {
        public AlignmentPoint Start { get; }
        public AlignmentPoint End { get; }
        public AlignmentPoint Center { get; }
        public double StartStation { get; }
        public double Length { get; }
        public double Radius { get; }
        private readonly double sweep, startAngle;
        public bool IsArc => Radius > 0;
        public AlignmentPoint PointAt(double distance)
        {
            if (distance < 0 || distance > Length || double.IsNaN(distance)) throw new ArgumentOutOfRangeException(nameof(distance));
            if (!IsArc) return new AlignmentPoint(Start.E + (End.E - Start.E) * distance / Length, Start.N + (End.N - Start.N) * distance / Length);
            double angle = startAngle + Math.Sign(sweep) * distance / Radius;
            return new AlignmentPoint(Center.E + Radius * Math.Cos(angle), Center.N + Radius * Math.Sin(angle));
        }
        private HorizontalSegment(AlignmentPoint start, AlignmentPoint end, AlignmentPoint center, double station, double radius, double angle)
        {
            if (new[] { start.E, start.N, end.E, end.N, center.E, center.N, station, radius, angle }.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                throw new InvalidOperationException("Alignment coordinates and stations must be finite numbers.");
            Start = start; End = end; Center = center; StartStation = station; Radius = radius; sweep = angle;
            startAngle = Math.Atan2(start.N - center.N, start.E - center.E);
            Length = radius > 0 ? Math.Abs(angle) * radius : AlignmentPoint.Distance(start, end);
            if (Length < 1e-8 || double.IsInfinity(Length) || double.IsNaN(Length) || double.IsInfinity(station + Length))
                throw new InvalidOperationException("Zero-length, full-circle or numerically unbounded segments are unsupported. Supply bounded nonzero geometry.");
        }
        public static HorizontalSegment Line(AlignmentPoint start, AlignmentPoint end, double station) => new HorizontalSegment(start, end, new AlignmentPoint(), station, 0, 0);
        public static HorizontalSegment Arc(AlignmentPoint start, AlignmentPoint end, AlignmentPoint center, double station, bool clockwise)
        {
            double radius = AlignmentPoint.Distance(start, center);
            if (radius < 1e-8 || Math.Abs(radius - AlignmentPoint.Distance(end, center)) > Math.Max(1e-5, radius * 1e-7))
                throw new InvalidOperationException("Arc start/end radii disagree. Supply a valid circular alignment.");
            double a = Math.Atan2(start.N - center.N, start.E - center.E), b = Math.Atan2(end.N - center.N, end.E - center.E);
            double angle = clockwise ? -Positive(a - b) : Positive(b - a);
            return new HorizontalSegment(start, end, center, station, radius, angle);
        }
        private static double Positive(double angle) => (angle % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
        public AlignmentProjection Project(AlignmentPoint point)
        {
            double distanceAlong, tangentE, tangentN; AlignmentPoint nearest; bool beyond = false, ambiguousEndpoint = false;
            if (!IsArc)
            {
                double e = End.E - Start.E, n = End.N - Start.N;
                double raw = ((point.E - Start.E) * e + (point.N - Start.N) * n) / (Length * Length);
                double t = Math.Max(0, Math.Min(1, raw)); beyond = raw < -1e-10 || raw > 1 + 1e-10;
                distanceAlong = t * Length; nearest = new AlignmentPoint(Start.E + e * t, Start.N + n * t);
                tangentE = e / Length; tangentN = n / Length;
            }
            else
            {
                double radius = AlignmentPoint.Distance(point, Center);
                if (radius < 1e-9) return new AlignmentProjection { Station = StartStation, Distance = Radius, Offset = double.NaN,
                    Easting = Start.E, Northing = Start.N, Note = "Ambiguous: point at arc centre" };
                double angle = Math.Atan2(point.N - Center.N, point.E - Center.E);
                double along = sweep > 0 ? Positive(angle - startAngle) : Positive(startAngle - angle);
                if (along <= Math.Abs(sweep) + 1e-10)
                {
                    distanceAlong = Math.Min(along, Math.Abs(sweep)) * Radius;
                    nearest = new AlignmentPoint(Center.E + Radius * Math.Cos(angle), Center.N + Radius * Math.Sin(angle));
                }
                else
                {
                    beyond = true; double startDistance = AlignmentPoint.Distance(point, Start), endDistance = AlignmentPoint.Distance(point, End);
                    ambiguousEndpoint = Math.Abs(startDistance - endDistance) < 1e-6;
                    bool first = startDistance <= endDistance;
                    nearest = first ? Start : End; distanceAlong = first ? 0 : Length;
                }
                double a = startAngle + Math.Sign(sweep) * distanceAlong / Radius;
                tangentE = -Math.Sin(a) * Math.Sign(sweep); tangentN = Math.Cos(a) * Math.Sign(sweep);
            }
            double deltaE = point.E - nearest.E, deltaN = point.N - nearest.N;
            return new AlignmentProjection { Station = StartStation + distanceAlong, Distance = AlignmentPoint.Distance(point, nearest),
                Offset = tangentN * deltaE - tangentE * deltaN, Easting = nearest.E, Northing = nearest.N,
                Note = ambiguousEndpoint ? "Ambiguous: equally near arc endpoints" : beyond ? "Nearest endpoint; perpendicular projection outside segment" : "Located" };
        }
    }
    public sealed class HorizontalAlignment
    {
        public string Name { get; set; }
        public string Units { get; set; }
        public double MetersPerUnit { get; set; }
        public List<HorizontalSegment> Segments { get; } = new List<HorizontalSegment>();
        public List<StationEquation> Equations { get; } = new List<StationEquation>();
        public double DisplayStation(double geometricStation)
        {
            var equation = Equations.LastOrDefault(e => e.Internal <= geometricStation);
            return equation == null ? geometricStation : geometricStation + equation.Ahead - equation.Internal;
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || Segments.Count == 0 || MetersPerUnit <= 0 || double.IsNaN(MetersPerUnit) || double.IsInfinity(MetersPerUnit))
                throw new InvalidOperationException("Every alignment needs a name, known linear units and nonzero segments.");
            for (int i = 1; i < Segments.Count; i++)
                if (AlignmentPoint.Distance(Segments[i - 1].End, Segments[i].Start) > 1e-5
                    || Math.Abs(Segments[i - 1].StartStation + Segments[i - 1].Length - Segments[i].StartStation) > 1e-5)
                    throw new InvalidOperationException("Alignment '" + Name + "' has a gap or discontinuous internal geometry stations. Supply station equations separately.");
            double adjustment = 0, last = double.NegativeInfinity;
            foreach (var equation in Equations)
            {
                if (new[] { equation.Internal, equation.Back, equation.Ahead }.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || equation.Internal <= last
                    || equation.Internal < Segments[0].StartStation || equation.Internal > Segments.Last().StartStation + Segments.Last().Length
                    || Math.Abs(equation.Back - (equation.Internal + adjustment)) > 1e-5)
                    throw new InvalidOperationException("Station equations must be ordered, within the geometry, and have consistent back stations.");
                adjustment = equation.Ahead - equation.Internal; last = equation.Internal;
            }
        }
        public AlignmentProjection Locate(double easting, double northing, double maximumOffset)
        {
            Validate();
            if (new[] { easting, northing, maximumOffset }.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || maximumOffset <= 0)
                throw new InvalidOperationException("Coordinates must be finite and maximum offset must be positive.");
            var candidates = Segments.Select(segment => segment.Project(new AlignmentPoint(easting, northing))).OrderBy(p => p.Distance).ToList();
            var best = candidates[0];
            if (best.Distance > maximumOffset) best.Note = "Outside maximum search distance";
            else if (candidates.Skip(1).Any(p => Math.Abs(p.Distance - best.Distance) < 1e-6 && Math.Abs(p.Station - best.Station) > 1e-5))
                best.Note = "Ambiguous: multiple equally near stations";
            return best;
        }
        public override string ToString() => Name + " (" + Units + ", " + Segments.Count + " segments)";
    }
    public sealed class StationEquation { public double Internal, Back, Ahead; }
    public static class AlignmentCoordinates
    {
        public static AlignmentPoint Transform(double x, double y, double scale, double rotationDegrees, double east, double north)
        {
            if (new[] { x, y, scale, rotationDegrees, east, north }.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || scale <= 0) throw new InvalidOperationException("Transform requires finite values and positive scale.");
            double angle = rotationDegrees * Math.PI / 180;
            return new AlignmentPoint(east + scale * (x * Math.Cos(angle) - y * Math.Sin(angle)), north + scale * (x * Math.Sin(angle) + y * Math.Cos(angle)));
        }
        public static string Station(double station)
        {
            if (double.IsNaN(station) || double.IsInfinity(station)) throw new InvalidOperationException("Station must be finite.");
            double rounded = Math.Round(Math.Abs(station), 3, MidpointRounding.AwayFromZero);
            return (station < 0 ? "-" : "") + Math.Floor(rounded / 1000).ToString("0", CultureInfo.InvariantCulture) + "+" + (rounded % 1000).ToString("000.###", CultureInfo.InvariantCulture);
        }
    }
}
