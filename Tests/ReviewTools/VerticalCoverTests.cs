using System;
using System.Collections.Generic;
using System.Linq;
using NavPro.Backend.Core;

internal static partial class Program
{
    private static List<CoverTriangle> CoverBox(double x=0,double y=0,double z=0,double size=1)
    {
        var p=new[]{new CoverPoint(x,y,z),new CoverPoint(x+size,y,z),new CoverPoint(x+size,y+size,z),new CoverPoint(x,y+size,z),new CoverPoint(x,y,z+size),new CoverPoint(x+size,y,z+size),new CoverPoint(x+size,y+size,z+size),new CoverPoint(x,y+size,z+size)};
        int[][] indices={new[]{0,1,2},new[]{0,2,3},new[]{4,6,5},new[]{4,7,6},new[]{0,4,5},new[]{0,5,1},new[]{1,5,6},new[]{1,6,2},new[]{2,6,7},new[]{2,7,3},new[]{3,7,4},new[]{3,4,0}};
        return indices.Select(i=>new CoverTriangle(p[i[0]],p[i[1]],p[i[2]])).ToList();
    }
    private static List<CoverTriangle> CoverGround(double x,double y,double width,Func<double,double,double> height)
    {
        var a=new CoverPoint(x,y,height(x,y));var b=new CoverPoint(x+width,y,height(x+width,y));var c=new CoverPoint(x+width,y+width,height(x+width,y+width));var d=new CoverPoint(x,y+width,height(x,y+width));
        return new List<CoverTriangle>{new CoverTriangle(a,b,c),new CoverTriangle(a,c,d)};
    }
    private static void VerticalCover()
    {
        var flat=new VerticalCoverGeometry(CoverGround(-1,-1,4,(x,y)=>2));
        var r=flat.Check(CoverBox(),.9,.001);Near(r.Cover.Value,1,"vertical cover uses pipe crown, not bottom or centre");Near(r.SurfaceLevel.Value,2,"reports surface level");Near(r.PipeTop.Value,1,"reports pipe top at critical XY");Check(r.State=="Meets minimum (mesh)" && r.CoveragePercent>99.999,"complete mesh footprint meets minimum");
        r=flat.Check(CoverBox(),1.1,.001);Check(r.State=="Insufficient cover","minimum cover failure classified");Near(r.Shortfall.Value,.1,"cover shortfall calculated");
        Check(flat.Check(CoverBox(),1,.001).State=="Borderline","equality within decision tolerance requires review");
        Check(flat.Check(CoverBox(z:2),.5,.001).State=="Exposed / intersects surface","pipe above surface reports negative cover");
        r=flat.Check(CoverBox(x:100),.5,.001);Check(r.State=="Unverified" && !r.Cover.HasValue,"horizontal separation never becomes a vertical clearance finding or pass");
        var partial=new VerticalCoverGeometry(CoverGround(0,0,.5,(x,y)=>2));r=partial.Check(CoverBox(),.5,.001);Check(r.State=="Partial coverage" && r.CoveragePercent<100,"uncovered footprint cannot pass");
        var open=CoverBox();open.RemoveAt(0);Check(flat.Check(open,.5,.001).State=="Unverified","open pipe mesh cannot masquerade as complete pipe crown");
        var duplicates=CoverGround(-1,-1,4,(x,y)=>2);duplicates.Add(duplicates[0]);Reject(()=>new VerticalCoverGeometry(duplicates),"same-reference duplicate surface rejected");
        var layers=CoverGround(-1,-1,4,(x,y)=>2);layers.AddRange(CoverGround(-1,-1,4,(x,y)=>3));Reject(()=>new VerticalCoverGeometry(layers),"stacked surfaces require explicit top surface selection");
        Reject(()=>new VerticalCoverGeometry(new[]{new CoverTriangle(new CoverPoint(0,0,0),new CoverPoint(0,1,0),new CoverPoint(0,1,2))}),"vertical surface facets rejected");
        Reject(()=>flat.Check(CoverBox(),double.NaN,.001),"NaN requirement rejected");Reject(()=>flat.Check(CoverBox(),1,-1),"negative decision tolerance rejected");
        var fan=new List<CoverTriangle>();var outer=new[]{new CoverPoint(-1,-1,5),new CoverPoint(2,-1,5),new CoverPoint(2,2,5),new CoverPoint(-1,2,5)};
        for(int i=0;i<4;i++)fan.Add(new CoverTriangle(outer[i],outer[(i+1)%4],new CoverPoint(.5,.5,2)));
        r=new VerticalCoverGeometry(fan).Check(CoverBox(),.9,.001);Near(r.Cover.Value,1,"interior surface valley found without interval sampling");Near(r.X,.5,"critical point inside pipe footprint");
        for(int i=0;i<40;i++)
        {
            double a=(i-20)*.03,b=(i%7-3)*.07,baseZ=4+i*.1;
            var engine=new VerticalCoverGeometry(CoverGround(-1,-1,4,(x,y)=>baseZ+a*x+b*y));
            r=engine.Check(CoverBox(),0,.00001);double expected=baseZ+Math.Min(0,a)+Math.Min(0,b)-1;
            Near(r.Cover.Value,expected,"sloped TIN analytic control "+i);
        }
        r=new VerticalCoverGeometry(CoverGround(749999,8884999,4,(x,y)=>12)).Check(CoverBox(750000,8885000,10),.5,.001);Near(r.Cover.Value,1,"large survey coordinates retain cover precision");
        var reverse=CoverGround(-1,-1,4,(x,y)=>2).Select(t=>new CoverTriangle(t.A,t.C,t.B)).ToList();Near(new VerticalCoverGeometry(reverse).Check(CoverBox(),.5,.001).Cover.Value,1,"surface winding does not alter cover");
        var m=new double[]{0,1,0,0,-1,0,0,0,0,0,1,0,10,20,30,1};var p=VerticalCoverGeometry.Transform(new CoverPoint(2,3,4),m,.3048);
        Near(p.X,7*.3048,"fragment world transform and feet-to-metres X");Near(p.Y,22*.3048,"fragment rotation and translation Y");Near(p.Z,34*.3048,"fragment world Z conversion");m[3]=1;Reject(()=>VerticalCoverGeometry.Transform(new CoverPoint(),m,1),"projective transform rejected");
    }
}
