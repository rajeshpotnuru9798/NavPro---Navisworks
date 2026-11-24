using System;
using System.Collections.Generic;
using System.Linq;

namespace NavPro.Backend.Core
{
    public struct CoverPoint
    {
        public double X, Y, Z;
        public CoverPoint(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
    public sealed class CoverTriangle
    {
        public readonly CoverPoint A, B, C;
        public CoverTriangle(CoverPoint a, CoverPoint b, CoverPoint c)
        {
            if (new[] { a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z }.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                throw new InvalidOperationException("Geometry contains non-finite coordinates.");
            A = a; B = b; C = c;
            if(double.IsNaN(TwiceArea)||double.IsInfinity(TwiceArea)) throw new InvalidOperationException("Geometry coordinate range exceeds supported precision.");
        }
        public double MinX => Math.Min(A.X, Math.Min(B.X, C.X));
        public double MaxX => Math.Max(A.X, Math.Max(B.X, C.X));
        public double MinY => Math.Min(A.Y, Math.Min(B.Y, C.Y));
        public double MaxY => Math.Max(A.Y, Math.Max(B.Y, C.Y));
        public double TwiceArea => (B.X-A.X)*(C.Y-A.Y)-(B.Y-A.Y)*(C.X-A.X);
        public double Height(CoverPoint p)
        {
            double d = TwiceArea;
            if (Math.Abs(d) < 1e-14) throw new InvalidOperationException("Vertical/degenerate facet has no unique Z over XY.");
            double u = ((p.X-A.X)*(C.Y-A.Y)-(p.Y-A.Y)*(C.X-A.X))/d;
            double v = ((B.X-A.X)*(p.Y-A.Y)-(B.Y-A.Y)*(p.X-A.X))/d;
            double height=A.Z + u*(B.Z-A.Z) + v*(C.Z-A.Z);
            if(double.IsNaN(height)||double.IsInfinity(height)) throw new InvalidOperationException("Cannot compute a finite surface/pipe elevation.");
            return height;
        }
    }
    public sealed class VerticalCoverResult
    {
        public string State { get; set; }
        public string Note { get; set; }
        public double? Cover { get; set; }
        public double? PipeTop { get; set; }
        public double? SurfaceLevel { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Required { get; set; }
        public double CoveragePercent { get; set; }
        public double? Shortfall => Cover.HasValue ? (double?)Math.Max(0, Required-Cover.Value) : null;
    }

    /// <summary>All coordinates/metrology are metres. Exact vertical gaps on the supplied tessellation.</summary>
    public sealed class VerticalCoverGeometry
    {
        private const double AreaEpsilon = 1e-12;
        private const int PairLimit = 20000000;
        private int pairs;
        private readonly Node surface;
        private sealed class Node
        {
            internal double MinX, MaxX, MinY, MaxY;
            internal Node Left, Right;
            internal CoverTriangle[] Faces;
            internal Node(CoverTriangle[] faces)
            {
                MinX=faces.Min(t=>t.MinX); MaxX=faces.Max(t=>t.MaxX); MinY=faces.Min(t=>t.MinY); MaxY=faces.Max(t=>t.MaxY);
                if (faces.Length<=12) { Faces=faces; return; }
                bool x = MaxX-MinX >= MaxY-MinY;
                Array.Sort(faces,(a,b)=>(x ? a.MinX+a.MaxX : a.MinY+a.MaxY).CompareTo(x ? b.MinX+b.MaxX : b.MinY+b.MaxY));
                int mid=faces.Length/2; Left=new Node(faces.Take(mid).ToArray()); Right=new Node(faces.Skip(mid).ToArray());
            }
            internal IEnumerable<CoverTriangle> Query(CoverTriangle t)
            {
                if (MaxX<t.MinX || MinX>t.MaxX || MaxY<t.MinY || MinY>t.MaxY) yield break;
                if (Faces!=null) { foreach(var f in Faces) if(f.MaxX>=t.MinX && f.MinX<=t.MaxX && f.MaxY>=t.MinY && f.MinY<=t.MaxY) yield return f; }
                else { foreach(var f in Left.Query(t)) yield return f; foreach(var f in Right.Query(t)) yield return f; }
            }
        }
        public VerticalCoverGeometry(IList<CoverTriangle> ground)
        {
            if (ground==null || ground.Count==0) throw new InvalidOperationException("Select a triangulated ground/top surface.");
            if (ground.Distinct().Count()!=ground.Count) throw new InvalidOperationException("Duplicate surface faces are not supported.");
            if (ground.Any(t=>Math.Abs(t.TwiceArea)<AreaEpsilon))
                throw new InvalidOperationException("The surface includes vertical/degenerate facets. Select a 2.5D top/TIN surface, excluding solid sides and undersides.");
            surface=new Node(ground.ToArray());
            // Coverage-by-area is valid only for a single-valued, non-overlapping TIN.
            // Reject duplicate/stacked faces rather than selecting a convenient elevation.
            var visited=new HashSet<CoverTriangle>();
            foreach(var t in ground)
            {
                visited.Add(t);
                foreach(var other in surface.Query(t))
                {
                    if(visited.Contains(other)) continue;
                    Budget();
                    if(Area(Intersect(t,other))>Math.Max(AreaEpsilon, Math.Min(Math.Abs(t.TwiceArea),Math.Abs(other.TwiceArea))*1e-9))
                        throw new InvalidOperationException("Surface faces overlap in plan (duplicate/layered/solid geometry). Select one non-overlapping top/TIN surface.");
                }
            }
        }
        private void Budget() { if(++pairs>PairLimit) throw new InvalidOperationException("Geometry comparison limit reached. Reduce the selected area; no complete verification was produced."); }
        public VerticalCoverResult Check(IList<CoverTriangle> pipe, double required, double decisionTolerance)
        {
            if(double.IsNaN(required)||double.IsInfinity(required)||required<0 || double.IsNaN(decisionTolerance)||double.IsInfinity(decisionTolerance)||decisionTolerance<0)
                throw new InvalidOperationException("Enter finite non-negative cover and decision tolerance.");
            var result=new VerticalCoverResult { Required=required,State="Unverified",Note="No pipe facets with a measurable plan footprint." };
            if(pipe==null || pipe.Count==0) return result;
            var points=pipe.SelectMany(t=>new[]{t.A,t.B,t.C}).ToArray();
            if(points.Max(p=>p.X)-points.Min(p=>p.X)<=1e-6 || points.Max(p=>p.Y)-points.Min(p=>p.Y)<=1e-6 || points.Max(p=>p.Z)-points.Min(p=>p.Z)<=1e-6)
            { result.Note="Pipe mesh has no solid 3D extent. Select complete outer pipe geometry."; return result; }
            if(!ClosedMesh(pipe)) { result.Note="Pipe mesh is open, non-manifold or incomplete. Select the complete outer pipe geometry, including ends; no cover pass is issued."; return result; }
            double area=0, covered=0, min=double.PositiveInfinity;
            foreach(var t in pipe ?? new List<CoverTriangle>())
            {
                double faceArea=Math.Abs(t.TwiceArea)/2;
                if(faceArea<AreaEpsilon) continue;
                area+=faceArea;
                foreach(var g in surface.Query(t))
                {
                    Budget(); var polygon=Intersect(t,g); double overlap=Area(polygon);
                    if(overlap<AreaEpsilon) continue;
                    covered+=overlap;
                    if(double.IsInfinity(area)||double.IsInfinity(covered)||double.IsNaN(covered)) throw new InvalidOperationException("Geometry area exceeds supported precision.");
                    foreach(var p in polygon)
                    {
                        double top=t.Height(p), level=g.Height(p), gap=level-top;
                        if(double.IsNaN(gap)||double.IsInfinity(gap)) throw new InvalidOperationException("Cannot compute a finite vertical cover.");
                        if(gap>=min) continue;
                        min=gap; result.Cover=gap; result.PipeTop=top; result.SurfaceLevel=level; result.X=p.X; result.Y=p.Y;
                    }
                }
            }
            if(area<=AreaEpsilon) return result;
            result.CoveragePercent=Math.Min(100,100*covered/area);
            if(!result.Cover.HasValue) { result.Note="No surface directly over this pipe footprint; horizontal separation is not checked."; return result; }
            if(covered<area-Math.Max(AreaEpsilon,area*1e-8))
            { result.State="Partial coverage"; result.Note="Surface gaps/uncovered pipe footprint. Cover is the minimum only in covered areas; this is not a pass."; return result; }
            if(min<0) { result.State="Exposed / intersects surface"; result.Note="Pipe top reaches above the surface at the reported XY."; }
            else if(Math.Abs(min-required)<=decisionTolerance) { result.State="Borderline"; result.Note="Within the chosen decision tolerance of the minimum; review against design geometry."; }
            else { result.State=min<required ? "Insufficient cover" : "Meets minimum (mesh)"; result.Note="Minimum vertical gap over the complete shared footprint of the selected model tessellation."; }
            return result;
        }
        private static bool ClosedMesh(IList<CoverTriangle> faces)
        {
            // Shared fragment vertices may differ below a micron after world transforms.
            Func<CoverPoint,string> key=p=>string.Join("/",new[]{p.X,p.Y,p.Z}.Select(v=>Math.Round(v/1e-6).ToString("0",System.Globalization.CultureInfo.InvariantCulture)));
            var edges=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var t in faces)
            {
                var vertices=new[]{key(t.A),key(t.B),key(t.C)};
                for(int i=0;i<3;i++)
                {
                    string a=vertices[i],b=vertices[(i+1)%3]; if(a==b) return false;
                    string edge=string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;
                    edges[edge]=edges.TryGetValue(edge,out int count)?count+1:1;
                }
            }
            return edges.Count>0 && edges.Values.All(v=>v==2);
        }
        private static double Cross(CoverPoint a,CoverPoint b,CoverPoint p) => (b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
        internal static List<CoverPoint> Intersect(CoverTriangle a,CoverTriangle b)
        {
            var polygon=new List<CoverPoint>{a.A,a.B,a.C};
            var clip=b.TwiceArea>0 ? new[]{b.A,b.B,b.C} : new[]{b.A,b.C,b.B};
            for(int edge=0;edge<3 && polygon.Count>0;edge++)
            {
                var next=new List<CoverPoint>(); var s=polygon[polygon.Count-1]; double ds=Cross(clip[edge],clip[(edge+1)%3],s);
                foreach(var e in polygon)
                {
                    double de=Cross(clip[edge],clip[(edge+1)%3],e);
                    if((ds>=0)!=(de>=0)) { double f=ds/(ds-de); next.Add(new CoverPoint(s.X+f*(e.X-s.X),s.Y+f*(e.Y-s.Y),0)); }
                    if(de>=0) next.Add(e);
                    s=e; ds=de;
                }
                polygon=next;
            }
            return polygon;
        }
        private static double Area(IList<CoverPoint> p)
        {
            if(p.Count<3) return 0;
            double sum=0; var origin=p[0];
            for(int i=1;i<p.Count-1;i++) sum+=Cross(origin,p[i],p[i+1]);
            return Math.Abs(sum)/2;
        }
        public static CoverPoint Transform(CoverPoint p,double[] m,double metresPerUnit)
        {
            if(m==null||m.Length!=16||m.Any(v=>double.IsNaN(v)||double.IsInfinity(v))||metresPerUnit<=0||double.IsInfinity(metresPerUnit)||double.IsNaN(metresPerUnit))
                throw new InvalidOperationException("Invalid fragment transform or units.");
            if(Math.Abs(m[3])+Math.Abs(m[7])+Math.Abs(m[11])>1e-12||Math.Abs(m[15]-1)>1e-12)
                throw new InvalidOperationException("Non-affine fragment transforms are unsupported.");
            return new CoverPoint((p.X*m[0]+p.Y*m[4]+p.Z*m[8]+m[12])*metresPerUnit,
                (p.X*m[1]+p.Y*m[5]+p.Z*m[9]+m[13])*metresPerUnit,(p.X*m[2]+p.Y*m[6]+p.Z*m[10]+m[14])*metresPerUnit);
        }
    }
}
