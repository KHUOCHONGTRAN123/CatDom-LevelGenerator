namespace CatDom.LevelGenerator;

/// <summary>Intent-first geometric candidate sampling. Never certifies a motif itself.</summary>
public sealed class CandidateConstructor
{
    private readonly ShapeSet set;
    private readonly LevelValidator validator;
    public CandidateConstructor(ShapeSet shapes) { set=shapes; validator = new LevelValidator(shapes); }

    public LevelData? TryBuild(GeneratorRequest request, int attemptSeed, out string reason)
    {
        var rand = new Random(attemptSeed);
        var target = request.targetPolicy.targetHoleFootprint ?? rand.Next(25,33);
        var result = new LevelData();
        // A real 4-connected interior wall component. Never touch the border.
        var wallStart = new Point(rand.Next(2,5),rand.Next(2,5));
        result.obstacleInfos.Add(wallStart);
        result.obstacleInfos.Add(new Point(wallStart.x, wallStart.y+1));
        var occupied = new HashSet<Point>(result.obstacleInfos);
        var shapes = set.shapes.Where(s => s.footprint.Length is >= 2 and <= 5).ToList();
        var bySize = set.shapes.GroupBy(s=>s.footprint.Length).ToDictionary(g=>g.Key,g=>g.ToList());
        if (shapes.Count == 0) { reason = "Catalog lacks ordinary shapes"; return null; }
        // Ordinary external boxes, always with a mouth inside and no body cells inside.
        // Distribute around top/bottom/left/right rather than using phantom mouth obstacles.
        var boxSpots = new List<(Point pos, int dir)>();
        for (var x=0;x<7;x++) { boxSpots.Add((new Point(x,-1),0)); boxSpots.Add((new Point(x,8),2)); }
        for (var y=0;y<8;y++) { boxSpots.Add((new Point(-1,y),3)); boxSpots.Add((new Point(7,y),1)); }
        Shuffle(boxSpots,rand);
        foreach (var spot in boxSpots)
        {
            if (result.catBoxInfos.Count >= request.targetPolicy.boxCount) break;
            var mouth = Mouth(spot.pos,spot.dir);
            if (occupied.Contains(mouth)) continue;
            if (result.catBoxInfos.Any(b=>Math.Abs(Mouth(b.position,b.direction).x-mouth.x)+Math.Abs(Mouth(b.position,b.direction).y-mouth.y)<3)) continue;
            result.catBoxInfos.Add(new BoxData {position=spot.pos,direction=spot.dir});
        }
        if (result.catBoxInfos.Count != request.targetPolicy.boxCount) { reason = "Could not place spaced external boxes"; return null; }
        var mouths = result.catBoxInfos.Select(b=>Mouth(b.position,b.direction)).ToHashSet();
        // The first two hole roles are chosen relative to the blocked region/queue mouth.
        int area = 0;
        var index = 0;
        var largeFirst = shapes.Where(s=>s.footprint.Length is 3 or 4).ToList();
        var placementsTried=0;
        while (area < target && index < 24)
        {
            var remaining = target-area;
            List<Shape> available;
            if (index < 2 && remaining >= 4) available = largeFirst;
            else available = shapes.Where(s=>s.footprint.Length <= remaining && remaining-s.footprint.Length != 1).ToList();
            if (available.Count == 0) available = shapes.Where(s=>s.footprint.Length<=remaining).ToList();
            if (available.Count == 0 && remaining == 1 && bySize.TryGetValue(1,out var ones)) available = ones;
            if (available.Count == 0) { reason = $"No shape fits remaining area {remaining}"; return null; }
            // Shape preferences: 2/3 tiles predominate; occasionally 4/5 tiles.
            var weighted = available.OrderBy(s=>s.footprint.Length == 2 || s.footprint.Length == 3 ? 0 : 1).ToList();
            var pool = weighted.Where(s=>s.footprint.Length is 2 or 3).ToList();
            if (index < 2) pool = weighted;
            if (pool.Count == 0 || rand.Next(100)<23) pool=available;
            bool fitted=false;
            for(int tries=0;tries<180;tries++)
            {
                placementsTried++;
                var shape=pool[rand.Next(pool.Count)];
                var maxX = shape.footprint.Max(p => p.x);
                var maxY = shape.footprint.Max(p => p.y);
                var point = new Point(rand.Next(0,7-maxX),rand.Next(0,8-maxY));
                var cells = shape.footprint.Select(p=>new Point(p.x+point.x,p.y+point.y)).ToArray();
                if (cells.Any(c=>occupied.Contains(c))) continue;
                // Don't fully block every mouth; keep at least one initially reachable free cell.
                if (index>=2 && cells.Any(c=>mouths.Contains(c)) && rand.Next(3)!=0) continue;
                // Role 0: prefer proximity to a queue mouth for QueueRevisitParking.
                if (index==0 && request.intent.pattern=="QueueRevisitParking" &&
                    cells.Min(c=>mouths.Min(m=>Math.Abs(c.x-m.x)+Math.Abs(c.y-m.y)))>3 && tries<130) continue;
                // RegionRelease: first role situated near wall to act as a possible blocker.
                if (index==0 && request.intent.pattern=="RegionRelease" &&
                    cells.Min(c=>Math.Abs(c.x-wallStart.x)+Math.Abs(c.y-wallStart.y))>2 && tries<130) continue;
                foreach(var c in cells) occupied.Add(c);
                var color=(index<10 ? index : rand.Next(0,10));
                result.holeInfos.Add(new HoleData { colorIds=new[]{color},numOfCats=new[]{shape.footprint.Length},holeType=shape.holeType,position=point });
                area+=shape.footprint.Length;
                fitted=true; break;
            }
            if (!fitted) {reason=$"Could not place shape {index} after {placementsTried} samples";return null;}
            index++;
        }
        if(area!=target) {reason=$"Footprint only {area}/{target}";return null;}
        // Allocate color supply: board + queue exactly equals initial footprint demand.
        var tokens = result.holeInfos.SelectMany(h=>Enumerable.Repeat(h.colorIds[0],h.numOfCats[0])).ToList();
        var free = Enumerable.Range(0,7).SelectMany(x=>Enumerable.Range(0,8).Select(y=>new Point(x,y)))
            .Where(c=>!occupied.Contains(c)).ToList();
        Shuffle(free,rand);
        // Leave some cells free for movement. Queue capacity is flexible (outside board).
        var boardCount = Math.Min(Math.Min(tokens.Count-request.targetPolicy.boxCount, free.Count-2),
            Math.Max(5,(int)Math.Round(tokens.Count*(0.50+rand.NextDouble()*0.12))));
        if (boardCount < 2) {reason="Insufficient free cells for board supply";return null;}
        var boxQueue = tokens.ToList();
        // For a queue revisit motif, an actor must have demand from both sources.
        var actorColor = result.holeInfos[0].colorIds[0];
        if (request.intent.pattern=="QueueRevisitParking")
        {
            if (!boxQueue.Remove(actorColor)) {reason="No actor token";return null;}
            result.catInfos.Add(new CatData { colorId=actorColor, position=free[result.catInfos.Count] });
        }
        Shuffle(boxQueue,rand);
        while (result.catInfos.Count<boardCount)
        {
            // Reserve actor color for queue when using QueueRevisitParking.
            var idx = boxQueue.FindIndex(c=>request.intent.pattern!="QueueRevisitParking" || c!=actorColor);
            if(idx<0) idx=0;
            var color=boxQueue[idx]; boxQueue.RemoveAt(idx);
            result.catInfos.Add(new CatData {colorId=color,position=free[result.catInfos.Count]});
        }
        // Group queue cats by role preference but permit repeated actor after a different color.
        Shuffle(boxQueue,rand);
        for(var i=0;i<result.catBoxInfos.Count;i++)
        {
            if(boxQueue.Count==0) {reason="Queue unexpectedly empty";return null;}
            var color=boxQueue[0]; boxQueue.RemoveAt(0);
            result.catBoxInfos[i].colorIds.Add(new CatData{colorId=color,position=result.catBoxInfos[i].position});
        }
        for(var i=0;i<boxQueue.Count;i++)
        {
            var b = result.catBoxInfos[i%result.catBoxInfos.Count];
            b.colorIds.Add(new CatData {colorId=boxQueue[i],position=b.position});
        }
        if(request.intent.pattern=="QueueRevisitParking")
        {
            // Ensure first actor also has queue supply, not just a board token.
            var queuedActor = result.catBoxInfos.Any(b=>b.colorIds.Any(c=>c.colorId==actorColor));
            if(!queuedActor) {reason="QueueRevisitParking actor lacks queued token";return null;}
        }
        var checkedLevel=validator.Check(result,request.targetPolicy.minHoleFootprint,request.targetPolicy.maxHoleFootprint,request.targetPolicy.targetHoleFootprint);
        if(!checkedLevel.valid) {reason="Constructed candidate failed validation: "+string.Join("; ",checkedLevel.errors); return null;}
        reason="Geometry/supply valid; motif not yet verified";
        return result;
    }
    private static Point Mouth(Point p,int d) {var delta = new[]{new Point(0,1),new Point(-1,0),new Point(0,-1),new Point(1,0)}[d];return new(p.x+delta.x,p.y+delta.y);}
    private static void Shuffle<T>(IList<T> list,Random random)
    { for(int i=list.Count-1;i>0;i--) {var j=random.Next(i+1);(list[i],list[j])=(list[j],list[i]);} }
}
