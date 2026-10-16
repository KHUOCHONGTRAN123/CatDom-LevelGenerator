using CatDom.CoreSolver;

namespace CatDom.LevelGenerator;

/// <summary>
/// Witness-specific observations, NOT an optimality proof or a global necessity test.
/// Every observation refers to a full solution already independently certified by V42.
/// Local footprint shadow compares the same post-clear remaining tasks with / without
/// a removed actor's old footprint. It is explicitly a geometric diagnostic only.
/// </summary>
public sealed class AnalysisEvent
{
    public int drag;
    public int actor;
    public string type = "";
    public int consumed;
    public int? catId;
    public int? boxId;
    public string detail = "";
}
public sealed class LocalReliefObservation
{
    public int drag;
    public int clearedHole;
    public string sameRemainingTaskCheck = "Same post-state (shadow only adds removed footprint)";
    public int actualReachableSources;
    public int withShadowReachableSources;
    public List<string> newlyReachable = new();
    public List<string> usedInContinuation = new();
    public string evidenceClass = "LocalDiagnostic";
    public string limitation = "Static rigid-translation access diagnostic on one replayed witness; not global necessity, uniqueness or difficulty.";
}
public sealed class AnalysisReport
{
    public string schemaVersion = "levelgen-analysis/0.2";
    public string inputHash = "";
    public string certifiedPlanHash = "";
    public string provenance = "Full solution certified by independent V42 replay; observation extracted from production solver move events";
    public string pattern = "";
    public string bottleneckEvidence = "Unknown";
    public string progressionEvidence = "Unknown";
    public string difficultyRating = "Unrated";
    public string qualityRating = "Unrated";
    public int drags;
    public int actorSwitches;
    public int movesWithoutConsumption;
    public int clearEvents;
    public int queueConsumptionEvents;
    public int queueLiveVisits;
    public int queueRevisits;
    public List<AnalysisEvent> timeline = new();
    public List<LocalReliefObservation> localRelief = new();
    public List<string> unknowns = new();
}
public sealed class WitnessAnalyzer
{
    private readonly ShapeSet shapes;
    public WitnessAnalyzer(ShapeSet catalog) => shapes = catalog;

    public AnalysisReport Analyze(LevelData input, IReadOnlyList<SolverMove> plan, string certifiedInputHash, string pattern)
    {
        var analysis = new AnalysisReport {inputHash=certifiedInputHash, pattern=pattern, drags=plan.Count};
        analysis.certifiedPlanHash=HashUtil.Text(JsonIO.Serialize(plan.Select(m=>new {m.holeId,m.start,m.path,m.eaten})));
        analysis.unknowns.Add("No exhaustive alternative-strategy search was conducted: local observation is not global necessity.");
        analysis.unknowns.Add("Reasoning depth, forcedness, cognitive difficulty and experience quality are not inferred from drag counts.");
        var positions = input.holeInfos.Select(h=>h.position).ToArray();
        var remaining = input.holeInfos.Select(h=>h.numOfCats[0]).ToArray();
        var aliveCats = Enumerable.Range(0,input.catInfos.Count).ToHashSet();
        var queueHeads = new int[input.catBoxInfos.Count];
        var queueConsumedByActor = new Dictionary<(int hole,int box),int>();
        var exitedAfterQueue = new HashSet<(int hole,int box)>();
        var walls = input.obstacleInfos.Concat(input.brokenObstacleInfos).ToHashSet();
        var footprints = input.holeInfos.Select(h=>shapes.Footprints[h.holeType]).ToArray();
        var precedingActor=-1;
        for (var step=0;step<plan.Count;step++)
        {
            var move=plan[step];
            if(move.holeId<0 || move.holeId>=positions.Length || move.path==null || move.path.Count==0)
                throw new InvalidDataException("Unsupported certified plan event: missing drag endpoint/actor");
            if(precedingActor!=-1 && move.holeId!=precedingActor) analysis.actorSwitches++;
            precedingActor=move.holeId;
            var oldPositions=(Point[])positions.Clone();
            var oldRemaining=(int[])remaining.Clone();
            var oldPos=positions[move.holeId];
            var end=move.path[^1];
            positions[move.holeId]=new Point(end.x,end.y);
            var eaten = move.eaten ?? new List<EatEvent>();
            if(eaten.Count==0) analysis.movesWithoutConsumption++;
            foreach(var ev in eaten)
            {
                if(ev.holeId<0 || ev.holeId>=remaining.Length) throw new InvalidDataException("Unknown consumer in solver event");
                remaining[ev.holeId]--;
                if(remaining[ev.holeId]<0) throw new InvalidDataException("Solver consumed more than expected demand");
                if(ev.catId>=0) aliveCats.Remove(ev.catId);
                if(ev.boxId>=0)
                {
                    if(ev.boxId>=queueHeads.Length) throw new InvalidDataException("Unknown queue in solver event");
                    queueHeads[ev.boxId]++;
                    analysis.queueConsumptionEvents++;
                    var key=(ev.holeId,ev.boxId);
                    if(queueConsumedByActor.ContainsKey(key) && exitedAfterQueue.Contains(key))
                    {
                        analysis.queueRevisits++;
                        analysis.timeline.Add(new AnalysisEvent{drag=step+1,actor=ev.holeId,type="QueueRevisit",boxId=ev.boxId,detail="Actor returned to consume queue after leaving mouth"});
                        exitedAfterQueue.Remove(key);
                    }
                    queueConsumedByActor[key]=step+1;
                    if(remaining[ev.holeId]>0) analysis.queueLiveVisits++;
                }
                analysis.timeline.Add(new AnalysisEvent{drag=step+1,actor=ev.holeId,type=ev.boxId>=0?"QueueConsume":"BoardConsume",consumed=1,catId=ev.catId>=0?ev.catId:null,boxId=ev.boxId>=0?ev.boxId:null});
            }
            foreach(var key in queueConsumedByActor.Keys.ToArray())
            {
                if(key.hole!=move.holeId || remaining[key.hole]<=0) continue;
                var mouth=Mouth(input.catBoxInfos[key.box]);
                if(!footprints[key.hole].Any(p=>positions[key.hole].x+p.x==mouth.x && positions[key.hole].y+p.y==mouth.y))
                    exitedAfterQueue.Add(key);
            }
            foreach(var hole in Enumerable.Range(0,remaining.Length).Where(h=>oldRemaining[h]>0 && remaining[h]==0))
            {
                analysis.clearEvents++;
                analysis.timeline.Add(new AnalysisEvent{drag=step+1,actor=hole,type="Clear",detail="Actor removed after its final consumption"});
                var oldFootprint=footprints[hole].Select(p=>new Point(positions[hole].x+p.x,positions[hole].y+p.y)).ToHashSet();
                var actual=ReachableSources(input,positions,remaining,aliveCats,queueHeads,footprints,walls,null);
                var shadow=ReachableSources(input,positions,remaining,aliveCats,queueHeads,footprints,walls,oldFootprint);
                var gained=actual.Except(shadow).OrderBy(x=>x,StringComparer.Ordinal).ToList();
                var observation=new LocalReliefObservation{
                    drag=step+1,clearedHole=hole,actualReachableSources=actual.Count,withShadowReachableSources=shadow.Count,
                    newlyReachable=gained
                };
                foreach(var source in gained)
                {
                    var parts=source.Split(':');
                    var actor=int.Parse(parts[0]);
                    var kind=parts[1];var index=int.Parse(parts[2]);
                    if(plan.Skip(step+1).Any(m=>m.eaten!=null && m.eaten.Any(e=>e.holeId==actor && (kind=="cat"?e.catId==index:e.boxId==index))))
                        observation.usedInContinuation.Add(source);
                }
                if(gained.Count!=0) analysis.localRelief.Add(observation);
            }
            if(oldPos!=positions[move.holeId] && eaten.Count==0)
                analysis.timeline.Add(new AnalysisEvent{drag=step+1,actor=move.holeId,type="Reposition",detail=$"{oldPos} -> {positions[move.holeId]}"});
        }
        // A replayed plan may contain initial auto-consumption not represented by per-drag events.
        // In that case do NOT publish a misleading causal timeline.
        if(plan.Count>0 && (remaining.Any(n=>n!=0) || aliveCats.Count!=0 ||
            input.catBoxInfos.Select((b,i)=>queueHeads[i]!=b.colorIds.Count).Any(x=>x)))
            throw new InvalidDataException("Solver move event stream is incomplete or inconsistent with complete solution (initial consumption is not modeled by this analyzer)");
        // An observed relief with subsequent use is a positive, local witness only.
        if (analysis.localRelief.Any(r=>r.usedInContinuation.Count>0))
            analysis.bottleneckEvidence="ObservedLocalRelief";
        else if(pattern=="QueueRevisitParking" && analysis.queueRevisits>0)
            analysis.bottleneckEvidence="ObservedQueueRevisit";
        analysis.progressionEvidence=analysis.timeline.Count>0?"ObservedWitnessTimeline":"Unknown";
        if(pattern=="RegionRelease" && !analysis.localRelief.Any(r=>r.usedInContinuation.Count>0))
            analysis.unknowns.Add("No locally relieved source subsequently consumed in this particular witness; RegionRelease motif remains unproven.");
        if(pattern=="QueueRevisitParking" && analysis.queueRevisits==0)
            analysis.unknowns.Add("No same-actor queue revisit observed in this particular witness; structural placement does not prove required parking.");
        return analysis;
    }

    private static Point Mouth(BoxData b)
    {
        var delta = new[]{new Point(0,1),new Point(-1,0),new Point(0,-1),new Point(1,0)}[b.direction];
        return new(b.position.x+delta.x,b.position.y+delta.y);
    }

    // Static movement access only. Cats are consumable targets, NOT hard walls.
    // Hold the same remaining tasks constant when adding the old cleared footprint.
    private static HashSet<string> ReachableSources(LevelData level,Point[] positions,int[] remaining,HashSet<int> aliveCats,
        int[] queueHeads,Point[][] footprints,HashSet<Point> walls,HashSet<Point>? shadow)
    {
        var accessible = new HashSet<string>();
        for(int actor=0;actor<remaining.Length;actor++)
        {
            if(remaining[actor]<=0) continue;
            var blocked=new HashSet<Point>(walls);
            foreach(var other in Enumerable.Range(0,remaining.Length).Where(h=>h!=actor && remaining[h]>0))
                foreach(var offset in footprints[other]) blocked.Add(new Point(positions[other].x+offset.x,positions[other].y+offset.y));
            if(shadow!=null) blocked.UnionWith(shadow);
            var seen=new HashSet<Point>();
            var queue=new Queue<Point>();
            var start=positions[actor];
            if(CanOccupy(start,footprints[actor],blocked,level.sizeX,level.sizeY)) {seen.Add(start);queue.Enqueue(start);}
            while(queue.Count>0)
            {
                var pos=queue.Dequeue();
                var occupied=footprints[actor].Select(p=>new Point(pos.x+p.x,pos.y+p.y)).ToHashSet();
                var color=level.holeInfos[actor].colorIds[0];
                foreach(var cat in aliveCats)
                    if(level.catInfos[cat].colorId==color && occupied.Contains(level.catInfos[cat].position)) accessible.Add($"{actor}:cat:{cat}");
                for(var b=0;b<level.catBoxInfos.Count;b++)
                    if(queueHeads[b]<level.catBoxInfos[b].colorIds.Count && level.catBoxInfos[b].colorIds[queueHeads[b]].colorId==color && occupied.Contains(Mouth(level.catBoxInfos[b])))
                        accessible.Add($"{actor}:box:{b}");
                foreach(var dir in new[]{new Point(0,1),new Point(0,-1),new Point(1,0),new Point(-1,0)})
                {
                    var next=new Point(pos.x+dir.x,pos.y+dir.y);
                    if(!seen.Contains(next) && CanOccupy(next,footprints[actor],blocked,level.sizeX,level.sizeY))
                    {seen.Add(next);queue.Enqueue(next);}
                }
            }
        }
        return accessible;
    }
    private static bool CanOccupy(Point p,Point[] footprint,HashSet<Point> blocked,int w,int h)
        => footprint.All(c=>p.x+c.x>=0 && p.y+c.y>=0 && p.x+c.x<w && p.y+c.y<h && !blocked.Contains(new Point(p.x+c.x,p.y+c.y)));
}
