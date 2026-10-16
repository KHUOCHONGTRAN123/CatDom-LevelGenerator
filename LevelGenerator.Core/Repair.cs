namespace CatDom.LevelGenerator;

/// <summary>
/// Small bounded, transactional mutations. Every accepted proposal is checked by
/// static validation. A proposal does NOT preserve solve/replay certification.
/// It must be independently re-certified before export.
/// </summary>
public sealed class RepairRecord
{
    public int attempt;
    public int step;
    public string operatorName="";
    public string parentHash="";
    public string? childHash;
    public string state="";
    public string reason="";
    public string solverStatus="NotRun";
    public string replayStatus="NotRun";
    public string solverFailureCategory="NotRun";
    public long solverExpandedStates;
    public double solveTimeMs;
    public double proposalTimeMs;
}
public sealed class CandidateRepair
{
    private readonly ShapeSet catalog;
    private readonly LevelValidator validator;
    public CandidateRepair(ShapeSet set){catalog=set;validator=new LevelValidator(set);}

    public LevelData? Propose(LevelData parent,GeneratorRequest request,int seed,int step,
        out string operatorName,out string reason,string failureHint="")
    {
        var rand=new Random(StableSeed(seed,step));
        var enabled=request.operatorPolicy.enabled;
        if(enabled.Length==0){operatorName="None";reason="No enabled repair operators";return null;}
        // Priority is selected from the observed failure, never from an invented difficulty score.
        // Preserve an explicit enabled-operator allowlist; rotate within the permitted priority.
        var priority=failureHint switch
        {
            "NoNextCatReachable" => new[]{"MoveCat","EditQueueSupply","MoveHole","MoveWall"},
            // Diagnosed from actual solver explanations, not inferred from layout.
            "NoMovableConsumptionTarget" => new[]{"MoveCat","EditQueueSupply","MoveHole","MoveWall"},
            "ParkingSearchExhausted" => new[]{"MoveWall","MoveHole","MoveCat","EditQueueSupply"},
            "AnalysisUnknown" when request.intent.pattern=="QueueRevisitParking" => new[]{"EditQueueSupply","MoveHole","MoveCat","MoveWall"},
            "AnalysisUnknown" => new[]{"MoveHole","MoveWall","MoveCat","EditQueueSupply"},
            "Cancelled" => new[]{"MoveHole","MoveWall","MoveCat","EditQueueSupply"},
            _ => new[]{"MoveCat","MoveHole","MoveWall","EditQueueSupply"}
        };
        var ops=priority.Where(x=>enabled.Contains(x)).ToArray();
        for(var k=0;k<ops.Length;k++)
        {
            var op=ops[(step+k)%ops.Length];
            // Rollback is implicit: parent is never mutated, only a JSON-deep-cloned candidate.
            var child=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(parent));
            bool changed=op switch
            {
                "MoveCat"=>MoveCat(child,rand,failureHint=="NoMovableConsumptionTarget"),
                "MoveHole"=>MoveHole(child,rand,request),
                "MoveWall"=>MoveWall(child,rand),
                "EditQueueSupply"=>EditQueueSupply(child,rand),
                _=>false
            };
            if(!changed) continue;
            var validation=validator.Check(child,request.targetPolicy.minHoleFootprint,request.targetPolicy.maxHoleFootprint,
                request.targetPolicy.targetHoleFootprint);
            if(!validation.valid) continue;
            if(!PreservesIntent(child,request)) continue;
            operatorName=op;reason=$"Validated transaction for {failureHint}; solver/replay certification invalidated";
            return child;
        }
        operatorName=ops[step%ops.Length];
        reason="No enabled operator produced a valid, intent-preserving mutation";
        return null;
    }

    public bool PreservesIntent(LevelData level,GeneratorRequest request)
    {
        if(!request.intent.protectedIntent) return true;
        if(level.holeInfos.Count<2 || level.obstacleInfos.Count<2) return false;
        if(request.intent.pattern=="QueueRevisitParking")
        {
            // Necessary structural precondition, not sufficient evidence for an actual revisit.
            var actor=level.holeInfos[0].colorIds[0];
            return level.catInfos.Any(c=>c.colorId==actor)
                && level.catBoxInfos.Any(b=>b.colorIds.Any(c=>c.colorId==actor));
        }
        var p=level.holeInfos[0].position;
        var shape=catalog.Footprints[level.holeInfos[0].holeType];
        return shape.Select(s=>new Point(p.x+s.x,p.y+s.y))
            .Any(cell=>level.obstacleInfos.Any(w=>Math.Abs(cell.x-w.x)+Math.Abs(cell.y-w.y)<=2));
    }

    private static int StableSeed(int seed,int step)
    {
        var bytes=System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"repair-v0.2|{seed}|{step}"));
        return BitConverter.ToInt32(bytes,0);
    }
    private static HashSet<Point> Occupied(LevelData level,ShapeSet catalog)
    {
        var occupied=level.obstacleInfos.Concat(level.brokenObstacleInfos).ToHashSet();
        foreach(var hole in level.holeInfos)
            foreach(var part in catalog.Footprints[hole.holeType])
                occupied.Add(new Point(hole.position.x+part.x,hole.position.y+part.y));
        foreach(var cat in level.catInfos) occupied.Add(cat.position);
        foreach(var b in level.catBoxInfos)
        {
            if(b.position.x is >=0 and <7 && b.position.y is >=0 and <8) occupied.Add(b.position);
        }
        return occupied;
    }
    private bool MoveCat(LevelData child,Random rand,bool favorConsumptionAccess)
    {
        if(child.catInfos.Count==0) return false;
        // Optional conservative repair hint: when solver says there is no movable
        // consumption target, try placing a cat in a FREE cell adjacent to a hole
        // requiring its color. This is only a proposal; solver/replay still decide.
        // No board-to-board similarity or deduplication is performed.
        if(favorConsumptionAccess)
        {
            var indexed=child.catInfos.Select((cat,index)=>(cat,index)).OrderBy(_=>rand.Next()).ToList();
            foreach(var (cat,index) in indexed)
            {
                var used=Occupied(child,catalog);used.Remove(cat.position);
                var targets=child.holeInfos.Where(h=>h.colorIds.Contains(cat.colorId)).ToList();
                var spots=new HashSet<Point>();
                foreach(var hole in targets)
                {
                    foreach(var local in catalog.Footprints[hole.holeType])
                    {
                        var cell=new Point(hole.position.x+local.x,hole.position.y+local.y);
                        foreach(var dir in new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)})
                        {
                            var p=new Point(cell.x+dir.x,cell.y+dir.y);
                            if(p.x is >=0 and <7 && p.y is >=0 and <8 && p!=cat.position && !used.Contains(p))
                                spots.Add(p);
                        }
                    }
                }
                if(spots.Count==0)continue;
                var ordered=spots.OrderBy(p=>p.x).ThenBy(p=>p.y).ToList();
                child.catInfos[index].position=ordered[rand.Next(ordered.Count)];
                return true;
            }
        }
        var randomCat=child.catInfos[rand.Next(child.catInfos.Count)];
        var occupied=Occupied(child,catalog);occupied.Remove(randomCat.position);
        var free=Enumerable.Range(0,7).SelectMany(x=>Enumerable.Range(0,8).Select(y=>new Point(x,y)))
            .Where(p=>!occupied.Contains(p) && p!=randomCat.position).ToList();
        if(free.Count==0)return false;
        randomCat.position=free[rand.Next(free.Count)];return true;
    }
    private bool MoveHole(LevelData child,Random rand,GeneratorRequest request)
    {
        // Protect the first two explicit role placements; relocate support hole(s).
        if(child.holeInfos.Count<3) return false;
        var hole=child.holeInfos[rand.Next(2,child.holeInfos.Count)];
        var occupied=Occupied(child,catalog);
        var shape=catalog.Footprints[hole.holeType];
        foreach(var p in shape)occupied.Remove(new Point(hole.position.x+p.x,hole.position.y+p.y));
        var spots=Enumerable.Range(0,7).SelectMany(x=>Enumerable.Range(0,8).Select(y=>new Point(x,y)))
            .Where(p=>p!=hole.position && shape.All(part=>p.x+part.x<7 && p.y+part.y<8 && !occupied.Contains(new Point(p.x+part.x,p.y+part.y))))
            .ToList();
        if(spots.Count==0)return false;
        hole.position=spots[rand.Next(spots.Count)];return true;
    }
    private bool MoveWall(LevelData child,Random rand)
    {
        // Translate all ordinary wall cells as one connected component; don't split it.
        var walls=child.obstacleInfos;
        if(walls.Count==0) return false;
        var used=Occupied(child,catalog);
        foreach(var w in walls)used.Remove(w);
        var dirs=new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)};
        foreach(var dir in dirs.OrderBy(_=>rand.Next()))
        {
            var shifted=walls.Select(w=>new Point(w.x+dir.x,w.y+dir.y)).ToList();
            if(shifted.All(p=>p.x>0 && p.x<6 && p.y>0 && p.y<7 && !used.Contains(p)))
            {child.obstacleInfos=shifted;return true;}
        }
        return false;
    }
    private static bool EditQueueSupply(LevelData child,Random rand)
    {
        var queues=child.catBoxInfos.SelectMany(b=>b.colorIds).ToList();
        if(queues.Count==0)return false;
        // Swap colors only; preserve total demand/supply and all queue positions.
        var first=queues[rand.Next(queues.Count)];
        var second=child.catInfos.Concat(queues).Where(x=>!ReferenceEquals(x,first) && x.colorId!=first.colorId).ToList();
        if(second.Count==0)return false;
        var other=second[rand.Next(second.Count)];
        (first.colorId,other.colorId)=(other.colorId,first.colorId);
        return true;
    }
}
