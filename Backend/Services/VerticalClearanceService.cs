using System;
using System.Collections.Generic;
using System.Linq;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using Autodesk.Navisworks.Api;
#if NAVPRO_COM_GEOMETRY
using Autodesk.Navisworks.Api.ComApi;
using Com = Autodesk.Navisworks.Api.Interop.ComApi;
using System.Runtime.InteropServices;
#endif

namespace NavPro.Backend.Services
{
    public sealed class VerticalClearanceService
    {
#if NAVPRO_COM_GEOMETRY
        public static bool GeometrySupported => true;
#else
        public static bool GeometrySupported => false;
#endif
        private readonly ClashReviewSession session;
        public VerticalClearanceService(ClashReviewSession session) { this.session=session; }
        public List<ModelItem> CurrentSelection()
        { session.CheckHost(); return session.Document.CurrentSelection.SelectedItems.ToList(); }
        public List<ClashSetChoice> ReadChoices(string mode)
        {
            session.CheckHost();var source=new ClashMatrixService(session.Document);
            if(mode=="Standard"||mode=="Compact") return source.ReadModelTreeChoices(mode=="Compact");
            if(mode=="Sets") return source.ReadSnapshot().Sets;
            throw new InvalidOperationException("Choose Sets, Standard or Compact.");
        }
        public List<ModelItem> FromChoice(ClashSetChoice choice)
        {
            session.CheckHost();if(choice==null)throw new InvalidOperationException("Choose a set or model item.");
            return choice.ExplicitItems!=null?choice.ExplicitItems.ToList():FromSet(choice.Set);
        }
        public List<ModelItem> FromSet(Autodesk.Navisworks.Api.SelectionSet set)
        {
            session.CheckHost(); if(set==null) throw new InvalidOperationException("Choose a saved set.");
            if(set.HasSearch) using(var search=new Search(set.Search)) using(var found=search.FindAll(session.Document,false)) return found.ToList();
            using(var found=set.GetSelectedItems(session.Document)) return found.ToList();
        }
        public void Show(VerticalClearanceFinding finding)
        {
            session.CheckHost(); if(finding?.Item==null) return;
            session.Document.CurrentSelection.CopyFrom(new[]{finding.Item});
            session.Document.ActiveView.FocusOnCurrentSelection();
        }
        public List<VerticalClearanceFinding> Run(IList<ModelItem> pipes,IList<ModelItem> ground,double requiredMetres,double toleranceMetres,Action<string> progress)
        {
            session.CheckHost();
#if !NAVPRO_COM_GEOMETRY
            throw new NotSupportedException("This build has no matching Navisworks geometry bridge. Use Manage 2024 or a build compiled with this release's ComApi SDK assemblies.");
#else
            if(pipes==null||pipes.Count==0||ground==null||ground.Count==0) throw new InvalidOperationException("Load pipe elements and a ground/top surface first.");
            long revision=session.Revision;
            var roots=pipes.Distinct().Where(p=>!p.Ancestors.Any(pipes.Contains)).ToList();
            if(roots.Count>5000) throw new InvalidOperationException("Select no more than 5,000 pipe elements per review.");
            var groundItems=GeometryItems(ground);
            var pipeItems=new HashSet<ModelItem>(GeometryItems(roots));
            if(groundItems.Any(pipeItems.Contains)) throw new InvalidOperationException("Pipe and surface selections overlap. Select distinct geometry.");
            double scale=UnitConversion.ScaleFactor(session.Document.Units,Units.Meters);
            var triangles=new List<CoverTriangle>();
            foreach(var item in groundItems) { Guard(revision); progress?.Invoke("Reading surface geometry: "+(item.DisplayName??"item")); triangles.AddRange(ReadMesh(item,scale)); if(triangles.Count>250000) throw new InvalidOperationException("Surface exceeds 250,000 triangles. Select a smaller review area."); }
            var engine=new VerticalCoverGeometry(triangles);
            var output=new List<VerticalClearanceFinding>();
            foreach(var pipe in roots)
            {
                Guard(revision); progress?.Invoke("Checking pipe "+(output.Count+1)+" of "+roots.Count+". Native geometry reads are synchronous.");
                try
                {
                    var mesh=new List<CoverTriangle>();
                    foreach(var item in GeometryItems(new[]{pipe})) { mesh.AddRange(ReadMesh(item,scale)); if(mesh.Count>100000) throw new InvalidOperationException("Pipe exceeds 100,000 triangles. Reduce the selected pipe assembly."); }
                    output.Add(new VerticalClearanceFinding{Pipe=pipe.DisplayName,Item=pipe,Result=engine.Check(mesh,requiredMetres,toleranceMetres)});
                }
                catch(Exception ex) when(ex is InvalidOperationException || ex is COMException || ex is ArgumentException)
                { output.Add(new VerticalClearanceFinding{Pipe=pipe.DisplayName,Item=pipe,Result=new VerticalCoverResult{Required=requiredMetres,State="Unverified",Note=ex.Message}}); }
            }
            Guard(revision); return output;
#endif
        }
        private void Guard(long revision)
        { session.CheckHost(); if(session.Revision!=revision) throw new InvalidOperationException("Model or source data changed. Reload selections and run a fresh review."); }
#if NAVPRO_COM_GEOMETRY
        private static List<ModelItem> GeometryItems(IEnumerable<ModelItem> roots) => roots.SelectMany(r=>r.DescendantsAndSelf).Where(i=>i.HasGeometry).Distinct().ToList();
        private static List<CoverTriangle> ReadMesh(ModelItem item,double scale)
        {
            var callback=new MeshCallback(scale);
            var path=ComApiBridge.ToInwOaPath(item);
            foreach(Com.InwOaFragment3 fragment in path.Fragments())
            {
                var transform=(Com.InwLTransform3f3)(object)fragment.GetLocalToWorldMatrix();
                var matrix=(Array)transform.Matrix;
                callback.Matrix=Enumerable.Range(0,16).Select(i=>Convert.ToDouble(matrix.GetValue(i+matrix.GetLowerBound(0)),System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                fragment.GenerateSimplePrimitives(Com.nwEVertexProperty.eNONE,callback);
                if(callback.Error!=null) throw new InvalidOperationException(callback.Error);
            }
            if(callback.OtherPrimitives) throw new InvalidOperationException("Selected geometry includes lines/points; select a triangulated surface or complete solid pipe mesh.");
            if(callback.Triangles.Count==0) throw new InvalidOperationException("No triangles are available for this geometry.");
            // Never guess COM units or accept a wrongly applied instance/model transform.
            using(var box=item.BoundingBox())
            {
                var all=callback.Triangles.SelectMany(t=>new[]{t.A,t.B,t.C}).ToArray();
                var lo=box.Min; var hi=box.Max;
                double[] actual={all.Min(p=>p.X),all.Min(p=>p.Y),all.Min(p=>p.Z),all.Max(p=>p.X),all.Max(p=>p.Y),all.Max(p=>p.Z)};
                double[] expected={lo.X*scale,lo.Y*scale,lo.Z*scale,hi.X*scale,hi.Y*scale,hi.Z*scale};
                double allowance=Math.Max(.002,Math.Max(hi.X-lo.X,Math.Max(hi.Y-lo.Y,hi.Z-lo.Z))*scale*1e-6);
                if(actual.Where((v,i)=>Math.Abs(v-expected[i])>allowance).Any()) throw new InvalidOperationException("Triangle/world bounds disagree. Units or fragment transform could not be validated; this item is unverified.");
            }
            return callback.Triangles;
        }
        [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
        public sealed class MeshCallback : Com.InwSimplePrimitivesCB
        {
            internal readonly List<CoverTriangle> Triangles=new List<CoverTriangle>();
            internal double[] Matrix; internal string Error; internal bool OtherPrimitives;
            private readonly double scale;
            internal MeshCallback(double scale) { this.scale=scale; }
            private CoverPoint Read(Com.InwSimpleVertex vertex)
            { var a=(Array)vertex.coord; int n=a.GetLowerBound(0); return VerticalCoverGeometry.Transform(new CoverPoint(Convert.ToDouble(a.GetValue(n)),Convert.ToDouble(a.GetValue(n+1)),Convert.ToDouble(a.GetValue(n+2))),Matrix,scale); }
            public void Triangle(Com.InwSimpleVertex a,Com.InwSimpleVertex b,Com.InwSimpleVertex c)
            {
                if(Error!=null) return;
                try { if(Triangles.Count>=250000) {Error="Geometry exceeds the 250,000 triangle extraction limit; narrow the selection.";return;} Triangles.Add(new CoverTriangle(Read(a),Read(b),Read(c))); }
                catch(Exception ex) { Error="Cannot read triangle geometry: "+ex.Message; }
            }
            public void Line(Com.InwSimpleVertex a,Com.InwSimpleVertex b) { OtherPrimitives=true; }
            public void Point(Com.InwSimpleVertex a) { OtherPrimitives=true; }
            public void SnapPoint(Com.InwSimpleVertex a) { }
        }
#endif
    }
}
