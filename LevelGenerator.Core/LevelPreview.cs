using System.Globalization;
using System.Security;
using System.Text;

namespace CatDom.LevelGenerator;

/// <summary>
/// Static geometry preview (SVG). Not a gameplay simulation or evidence of solvability.
/// Holes are drawn from the actual shape catalog, boxes with current queue tokens.
/// </summary>
public static class LevelPreview
{
    private static readonly string[] Colors={
        "#e65b5b","#eaa631","#42a56a","#4b9adb","#9a6adc",
        "#ca6eaa","#38aeb6","#b58b5b","#668b4a","#747f9f"
    };
    public static string Render(LevelData level,ShapeSet catalog,bool certified=false,string title="CatDom level preview")
    {
        var validation=new LevelValidator(catalog).Check(level);
        if(!validation.valid)throw new InvalidDataException("Cannot preview an invalid level: "+string.Join("; ",validation.errors));
        const int cell=74;
        const int left=115;
        const int top=165;
        var w=level.sizeX*cell;
        var h=level.sizeY*cell;
        var sb=new StringBuilder();
        static string Esc(string s)=>SecurityElement.Escape(s) ?? "";
        static string N(double v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        void Text(double x,double y,string value,int size=17,string fill="#273245",string weight="500",string anchor="start")
            =>sb.AppendLine($"<text x='{N(x)}' y='{N(y)}' fill='{fill}' font-size='{size}' font-weight='{weight}' text-anchor='{anchor}' font-family='Segoe UI,Arial,sans-serif'>{Esc(value)}</text>");
        void Rect(double x,double y,double width,double height,string fill,string stroke="none",double r=0,double strokeW=1)
            =>sb.AppendLine($"<rect x='{N(x)}' y='{N(y)}' width='{N(width)}' height='{N(height)}' rx='{N(r)}' fill='{fill}' stroke='{stroke}' stroke-width='{N(strokeW)}' />");
        double X(int x)=>left+x*cell;
        double Y(int y)=>top+y*cell;
        sb.AppendLine("<svg xmlns='http://www.w3.org/2000/svg' width='1080' height='970' viewBox='0 0 1080 970' role='img'>");
        Rect(0,0,1080,970,"#f4f7fa");
        Text(50,52,title,27,"#192536","700");
        Text(50,80,certified?"CERTIFIED: solved + independent replay":"NOT CERTIFIED: geometry preview only",18,
            certified?"#25724c":"#a04f20","600");
        Rect(left-1,top-1,w+2,h+2,"#fff","#9eafc1",3,2);
        for(var y=0;y<level.sizeY;y++)for(var x=0;x<level.sizeX;x++)
        {
            Rect(X(x)+1,Y(y)+1,cell-2,cell-2,(x+y)%2==0?"#ffffff":"#f0f4f8");
        }
        for(int x=0;x<level.sizeX;x++) Text(X(x)+cell/2,top-11,x.ToString(),14,"#516176","500","middle");
        for(int y=0;y<level.sizeY;y++) Text(left-18,Y(y)+cell/2+5,y.ToString(),14,"#516176","500","middle");
        foreach(var wall in level.obstacleInfos.Concat(level.brokenObstacleInfos))
        {
            Rect(X(wall.x)+5,Y(wall.y)+5,cell-10,cell-10,"#354456","#23313e",8,2);
            Text(X(wall.x)+cell/2,Y(wall.y)+cell/2+6,"W",19,"#ffffff","700","middle");
        }
        for(int i=0;i<level.holeInfos.Count;i++)
        {
            var hole=level.holeInfos[i];
            var color=Colors[hole.colorIds[0]];
            foreach(var p in catalog.Footprints[hole.holeType])
            {
                Rect(X(hole.position.x+p.x)+4,Y(hole.position.y+p.y)+4,cell-8,cell-8,color,"#ffffff",10,2);
            }
            var cells=catalog.Footprints[hole.holeType];
            var midx=cells.Average(p=>p.x+hole.position.x+0.5);
            var midy=cells.Average(p=>p.y+hole.position.y+0.5);
            Rect(left+midx*cell-22,top+midy*cell-19,44,33,"#17212c","#ffffff",8,1.5);
            Text(left+midx*cell,top+midy*cell+5,$"H{i}",15,"#ffffff","700","middle");
        }
        foreach(var cat in level.catInfos)
        {
            var cx=X(cat.position.x)+cell/2;
            var cy=Y(cat.position.y)+cell/2;
            sb.AppendLine($"<circle cx='{N(cx)}' cy='{N(cy)}' r='24' fill='{Colors[cat.colorId]}' stroke='#192536' stroke-width='3'/>");
            Text(cx,cy+6,cat.colorId.ToString(),17,"#ffffff","700","middle");
        }
        for(int i=0;i<level.catBoxInfos.Count;i++)
        {
            var box=level.catBoxInfos[i];
            var delta=new[]{new Point(0,1),new Point(-1,0),new Point(0,-1),new Point(1,0)}[box.direction];
            var mouth=new Point(box.position.x+delta.x,box.position.y+delta.y);
            var cx=X(box.position.x)+cell/2;
            var cy=Y(box.position.y)+cell/2;
            Rect(X(box.position.x)+5,Y(box.position.y)+5,cell-10,cell-10,"#e5eaee","#657789",9,2);
            Text(cx,cy-3,$"B{i}",17,"#223445","700","middle");
            Text(cx,cy+18,$"{box.colorIds.Count} cats",12,"#334c60","500","middle");
            sb.AppendLine($"<circle cx='{N(X(mouth.x)+cell/2)}' cy='{N(Y(mouth.y)+cell/2)}' r='8' fill='none' stroke='#58697a' stroke-width='2' stroke-dasharray='3 3'/>");
        }
        var lx=730;
        Text(lx,150,"Board summary",21,"#192536","700");
        Text(lx,185,$"Hole footprint: {validation.footprint} / 56",17);
        Text(lx,216,$"Holes: {level.holeInfos.Count}",17);
        Text(lx,247,$"Board cats: {level.catInfos.Count}",17);
        Text(lx,278,$"Boxes: {level.catBoxInfos.Count}",17);
        Text(lx,309,$"Walls: {level.obstacleInfos.Count+level.brokenObstacleInfos.Count}",17);
        Text(lx,357,"Legend",20,"#192536","700");
        Text(lx,389,"H# = hole / actor ID",16);
        Text(lx,417,"Circle = board cat",16);
        Text(lx,445,"W = wall",16);
        Text(lx,473,"B# = box, dot = mouth",16);
        Text(lx,526,"Color IDs",19,"#192536","700");
        for(var i=0;i<10;i++)
        {
            var xx=lx+(i%5)*65;
            var yy=550+(i/5)*53;
            Rect(xx,yy,44,35,Colors[i],"#6d7881",7,1);
            Text(xx+22,yy+23,i.ToString(),16,"#ffffff","700","middle");
        }
        Text(50,922,"Static layout preview only. This image does not verify gameplay, solution quality or bottleneck necessity.",16,"#5d6c7b");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
