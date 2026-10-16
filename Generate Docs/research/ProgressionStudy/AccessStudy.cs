#nullable enable
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using CatDom.CoreSolver;
using CatDom.CoreSolver.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Research only: frozen settled phase, other groups fixed, stop at first eat.
internal static class AccessStudy
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Type T=typeof(CatLevelSolver).GetNestedType("Engine",BindingFlags.NonPublic)!;
    internal static object Field(object e,string n)=>T.GetField(n,F)!.GetValue(e)!;
    internal static object Call(object e,string n,params object[] a)=>T.GetMethod(n,F)!.Invoke(e,a)!;
    internal sealed record Target(string key,int hole,int steps);
    internal sealed record Measurement(BoardSnapshot snapshot,List<Target> targets,int reachableAnchors,int checkedFrontiers);
    internal static object Engine(BoardInput input,List<EngineMove> prefix)
    {
        var e=Activator.CreateInstance(T,F,null,new object[]{input.Copy(),new EngineContext(),CancellationToken.None,new EngineResult()},null)!;
        Call(e,"Replay",prefix,false);Call(e,"Prepare");return e;
    }
    internal static Measurement Measure(BoardInput input,List<EngineMove> prefix,HashSet<Cell>? shadow=null)
    {
        var e=Engine(input,prefix);var s=(BoardSnapshot)Call(e,"Snapshot");
        var board=(BoardInput)Field(e,"board");var groups=(int[][])Field(e,"linkedGroups");
        var valid=(bool[][])Field(e,"valid");var goals=(bool[][])Field(e,"goals");
        var seenGroups=new HashSet<int>();var targets=new Dictionary<string,Target>();int anchors=0,checks=0;
        foreach(var root in s.holes.Where(h=>!h.finished))
        {
            if(seenGroups.Contains(root.id))continue;
            var members=groups[root.id].Where(i=>!s.holes[i].finished).ToArray();foreach(var i in members)seenGroups.Add(i);
            if(members.Any(i=>!(bool)Call(e,"CanDrag",i,s.holes[i].position.y*board.width+s.holes[i].position.x)))continue;
            var fixedCells=board.obstacles.ToHashSet();
            if(shadow!=null)fixedCells.UnionWith(shadow);
            foreach(var h in s.holes.Where(h=>!h.finished && !members.Contains(h.id)))
                foreach(var c in board.holes[h.id].footprint)fixedCells.Add(new Cell(h.position.x+c.x,h.position.y+c.y));
            int start=root.position.y*board.width+root.position.x;
            var paths=new Dictionary<int,List<Cell>>{{start,new(){root.position}}};var q=new Queue<int>();q.Enqueue(start);
            while(q.Count>0)
            {
                int p=q.Dequeue();var from=new Cell(p%board.width,p/board.width);
                foreach(var to in new[]{new Cell(from.x+1,from.y),new Cell(from.x-1,from.y),new Cell(from.x,from.y+1),new Cell(from.x,from.y-1)})
                {
                    if(to.x<0||to.y<0||to.x>=board.width||to.y>=board.height)continue;
                    int dest=to.y*board.width+to.x;if(paths.ContainsKey(dest))continue;
                    int dx=to.x-root.position.x,dy=to.y-root.position.y,oldDx=from.x-root.position.x,oldDy=from.y-root.position.y;
                    int dir=to.y>from.y?0:to.x>from.x?1:to.y<from.y?2:3;
                    bool legal=true,eats=false;
                    foreach(int i in members)
                    {
                        var pos=s.holes[i].position;var z=new Cell(pos.x+dx,pos.y+dy);int next=z.y*board.width+z.x,old=(pos.y+oldDy)*board.width+pos.x+oldDx;
                        if(z.x<0||z.y<0||z.x>=board.width||z.y>=board.height||!valid[i][next]
                            ||!(bool)Call(e,"CanStep",i,old,next)||!(bool)Call(e,"CanEnterCat",i,next,dir)
                            ||board.holes[i].footprint.Any(c=>fixedCells.Contains(new Cell(z.x+c.x,z.y+c.y)))) {legal=false;break;}
                        eats|=goals[i][next];
                    }
                    if(!legal)continue;
                    var path=new List<Cell>(paths[p]){to};paths[dest]=path;anchors++;
                    if(!eats){q.Enqueue(dest);continue;}
                    // Replay each frontier to identify real consumed sources, not relaxed goals.
                    checks++;var probe=new List<EngineMove>(prefix){new(){holeId=root.id,start=root.position,path=path}};
                    var actual=CatLevelSolver.VerifyPlan(input,probe).finalState;
                    foreach(var id in s.remainingCatIds.Except(actual.remainingCatIds))
                    {
                        var cat=board.cats.Single(c=>c.id==id);
                        foreach(int i in members.Where(i=>s.holes[i].color==cat.color))
                        {
                            var pos=s.holes[i].position;
                            if(!board.holes[i].footprint.Any(c=>pos.x+dx+c.x==cat.position.x && pos.y+dy+c.y==cat.position.y))continue;
                            Add($"cat:{id}",i,path.Count-1);
                        }
                    }
                    foreach(var box in s.boxes)
                    {
                        var post=actual.boxes.Single(b=>b.id==box.id);
                        for(int ix=box.consumed;ix<post.consumed;ix++)
                            foreach(int i in members.Where(i=>s.holes[i].color==board.boxes[box.id].cats[ix].color))
                            {
                                var pos=s.holes[i].position;int anchor=(pos.y+dy)*board.width+pos.x+dx;
                                if((bool)Call(e,"BoxTouches",i,anchor,box.id))Add($"box:{box.id}:{ix}",i,path.Count-1);
                            }
                    }
                }
            }
        }
        return new(s,targets.Values.OrderBy(t=>t.key).ThenBy(t=>t.hole).ToList(),anchors,checks);
        void Add(string key,int hole,int steps) {string k=$"{key}@{hole}";if(!targets.TryGetValue(k,out var old)||steps<old.steps)targets[k]=new(key,hole,steps);}
    }
    static string Key(Target t)=>$"{t.key}@{t.hole}";
    internal static void Run(string root,bool improved=false)
    {
        var catalog=SolverJson.ReadCatalog(File.ReadAllText(Path.Combine(root,"IndependentSolver/IO/HoleShapes.json")));
        var output=new List<object>();
        var inputDirectory=Path.Combine(root,"Levels");
        var names=new[]{"Level00129","Level00098","Level00115"};
        foreach(var name in names)
        {
            var json=File.ReadAllText(Path.Combine(inputDirectory,name+".json"));var input=SolverJson.ReadLevel(json,catalog);
            var raw=JObject.Parse(json);var wallCells=new[]{"obstacleInfos","brokenObstacleInfos"}.SelectMany(k=>(raw[k] as JArray??new JArray()).Select(c=>new Cell((int)c["x"]!,(int)c["y"]!))).Where(c=>c.x>=0&&c.y>=0&&c.x<input.width&&c.y<input.height).ToHashSet();
            var withoutWall=input.Copy();withoutWall.obstacles.RemoveAll(c=>wallCells.Contains(c));
            var interiorWallCells=InteriorComponents(wallCells,input.width,input.height);
            var withoutInterior=input.Copy();withoutInterior.obstacles.RemoveAll(c=>interiorWallCells.Contains(c));
            using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var r=improved?CatLevelSolver.SolveLevelWithProgress(json,cancel.Token,null,0):CatLevelSolver.SolveLevel(json,cancel.Token);
            if(r.status!=SolverStatus.Solved)throw new Exception(name+" "+r.status);
            var prefix=new List<EngineMove>();var previous=Measure(input,prefix);var initial=previous;
            var events=new List<object>();var history=new List<Measurement>{initial};int probes=initial.checkedFrontiers;
            var wallEffects=new List<object>();
            RecordWallEffect(0,initial);
            for(int i=0;i<r.moves.Count;i++)
            {
                var m=r.moves[i];prefix.Add(new(){holeId=m.holeId,start=m.start,path=m.path,eaten=m.eaten});
                if(m.eaten.Count>0)
                {
                    var first=m.eaten[0];string source=first.catId>=0?$"cat:{first.catId}":$"box:{first.boxId}:{first.boxIndex}";
                    if(!previous.targets.Any(t=>t.key==source && t.hole==first.holeId))throw new Exception($"First consumption missing from access frontier: {name} drag {i+1} {source}");
                }
                var next=Measure(input,prefix);probes+=next.checkedFrontiers;
                var finished=next.snapshot.holes.Where(h=>h.finished && !previous.snapshot.holes[h.id].finished).Select(h=>h.id).ToArray();
                var newly=next.targets.Where(t=>!previous.targets.Any(b=>Key(b)==Key(t))).ToArray();
                var cheaper=next.targets.Where(t=>previous.targets.Any(b=>Key(b)==Key(t)&&b.steps>t.steps)).Select(t=>new{t.key,t.hole,before=previous.targets.Single(b=>Key(b)==Key(t)).steps,after=t.steps}).ToArray();
                var shadow=new HashSet<Cell>();var linked=Group(input,m.holeId);
                foreach(int id in finished)
                {
                    var pos=previous.snapshot.holes[id].position;
                    if(linked.Contains(id)){pos=new Cell(pos.x+m.path[^1].x-m.start.x,pos.y+m.path[^1].y-m.start.y);}
                    foreach(var c in input.holes[id].footprint)shadow.Add(new Cell(pos.x+c.x,pos.y+c.y));
                }
                var blocked=finished.Length>0?Measure(input,prefix,shadow):next;probes+=finished.Length>0?blocked.checkedFrontiers:0;
                var released=next.targets.Where(t=>!blocked.targets.Any(b=>Key(b)==Key(t))).ToArray();
                var releaseCheaper=next.targets.Where(t=>blocked.targets.Any(b=>Key(b)==Key(t)&&b.steps>t.steps)).Select(t=>new{t.key,t.hole,withShadow=blocked.targets.Single(b=>Key(b)==Key(t)).steps,actual=t.steps}).ToArray();
                var future=newly.Select(t=>new {t.key,t.hole,nextUse=NextUse(r.moves,i,t)}).ToArray();
                var releaseUse=released.Select(t=>new {t.key,t.hole,nextUse=NextUse(r.moves,i,t)}).ToArray();
                events.Add(new {drag=i+1,hole=m.holeId,eaten=m.eaten.Count,queueEaten=m.eaten.Count(x=>x.boxId>=0),finished,
                    beforeTargetPairs=previous.targets.Count,afterTargetPairs=next.targets.Count,newTargets=future,cheaper,
                    footprintReleaseTargets=releaseUse,footprintReleaseCheaper=releaseCheaper,shadowTargetPairs=blocked.targets.Count,
                    boxChanges=next.snapshot.boxes.Where(b=>b.consumed!=previous.snapshot.boxes[b.id].consumed).Select(b=>new{b.id,before=previous.snapshot.boxes[b.id].consumed,after=b.consumed}).ToArray(),
                    remainingCats=next.snapshot.remainingCatIds.Count+next.snapshot.boxes.Sum(b=>input.boxes[b.id].cats.Length-b.consumed)});
                previous=next;history.Add(next);RecordWallEffect(i+1,next);
            }
            var segments=new List<object>();
            for(int i=0;i<r.moves.Count;i++)
            {
                if(r.moves[i].eaten.Count>0)continue;
                int begin=i;while(i+1<r.moves.Count && r.moves[i+1].eaten.Count==0)i++;
                var before=history[begin];var after=history[i+1];
                var opened=after.targets.Where(t=>!before.targets.Any(b=>Key(b)==Key(t))).Select(t=>new {t.key,t.hole,nextUse=NextUse(r.moves,i,t)}).ToArray();
                var closer=after.targets.Where(t=>before.targets.Any(b=>Key(b)==Key(t)&&b.steps>t.steps)).Select(t=>new {t.key,t.hole,before=before.targets.Single(b=>Key(b)==Key(t)).steps,after=t.steps,nextUse=NextUse(r.moves,i,t)}).ToArray();
                int repeats=history.Skip(begin).Take(i-begin+2).Select(h=>JsonConvert.SerializeObject(h.snapshot)).GroupBy(x=>x).Sum(g=>g.Count()-1);
                segments.Add(new {firstDrag=begin+1,lastDrag=i+1,drags=i-begin+1,steps=r.moves.Skip(begin).Take(i-begin+1).Sum(m=>m.path.Count-1),beforeTargetPairs=before.targets.Count,afterTargetPairs=after.targets.Count,newTargets=opened,closer,repeatedSnapshotCount=repeats,evidence=opened.Any(t=>t.nextUse!=null)?"DirectAccessSupported":closer.Any(t=>t.nextUse!=null)?"ApproachCostSupported":"Unknown"});
            }
            output.Add(new {level=name,status=r.status.ToString(),inputHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),searchAlgorithm=r.searchAlgorithm,r.solveTimeMs,r.expanded,planHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(r.moves)))),drags=r.moves.Count,initialTargets=initial.targets,initialRemainingCats=initial.snapshot.remainingCatIds.Count+initial.snapshot.boxes.Sum(b=>input.boxes[b.id].cats.Length-b.consumed),events,preparationSegments=segments,wallCells,interiorWallCells,wallEffects,verifiedFrontiers=probes});
            Console.WriteLine($"{name}: {r.moves.Count} drags; {probes} frontier replay probes");
            void RecordWallEffect(int drag,Measurement actual)
            {
                var relaxed=Measure(withoutWall,prefix);probes+=relaxed.checkedFrontiers;
                if(JsonConvert.SerializeObject(actual.snapshot)!=JsonConvert.SerializeObject(relaxed.snapshot))throw new Exception("Wall diagnostic changed replay settlement: "+name+" drag "+drag);
                var relaxedInterior=interiorWallCells.SetEquals(wallCells)?relaxed:Measure(withoutInterior,prefix);
                if(!interiorWallCells.SetEquals(wallCells))probes+=relaxedInterior.checkedFrontiers;
                if(JsonConvert.SerializeObject(actual.snapshot)!=JsonConvert.SerializeObject(relaxedInterior.snapshot))throw new Exception("Interior wall diagnostic changed replay settlement: "+name+" drag "+drag);
                wallEffects.Add(new {drag,actualPairs=actual.targets.Count,withoutWallPairs=relaxed.targets.Count,
                    actualParkingAnchors=actual.reachableAnchors-actual.checkedFrontiers,
                    withoutWallParkingAnchors=relaxed.reachableAnchors-relaxed.checkedFrontiers,
                    withoutInteriorWallParkingAnchors=relaxedInterior.reachableAnchors-relaxedInterior.checkedFrontiers,
                    newlyReachableWithoutWall=relaxed.targets.Where(t=>!actual.targets.Any(a=>Key(a)==Key(t))).ToArray(),
                    cheaperWithoutWall=relaxed.targets.Where(t=>actual.targets.Any(a=>Key(a)==Key(t)&&a.steps>t.steps)).Select(t=>new{t.key,t.hole,actual=actual.targets.Single(a=>Key(a)==Key(t)).steps,withoutWall=t.steps}).ToArray(),
                    newlyReachableWithoutInteriorWall=relaxedInterior.targets.Where(t=>!actual.targets.Any(a=>Key(a)==Key(t))).ToArray(),
                    cheaperWithoutInteriorWall=relaxedInterior.targets.Where(t=>actual.targets.Any(a=>Key(a)==Key(t)&&a.steps>t.steps)).Select(t=>new{t.key,t.hole,actual=actual.targets.Single(a=>Key(a)==Key(t)).steps,withoutInteriorWall=t.steps}).ToArray()});
            }
        }
        string filename=improved?"access-focus-improved.json":"access-focus-hybrid.json";
        File.WriteAllText(Path.Combine(root,"Generate Docs/research",filename),JsonConvert.SerializeObject(new {version="3.0.0",profile="focus",mode=improved?"forced improved":"default hybrid",solverHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CatLevelSolver).Assembly.Location))),catalogHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"IndependentSolver/IO/HoleShapes.json")))),firstConsumptionCheck="Every eating drag's first consumed source/hole must appear in the previous state's access frontier; fail run on mismatch",method="Frozen phase BFS stops at first consumption; actual frontier prefix replay; footprint shadow and wall removal are research-only diagnostics, not exportable levels; same engine, not independent certification",levels=output},Formatting.Indented));
    }
    static HashSet<Cell> InteriorComponents(HashSet<Cell> walls,int width,int height)
    {
        var rest=walls.ToHashSet();var interior=new HashSet<Cell>();
        while(rest.Count>0)
        {
            var first=rest.First();rest.Remove(first);var component=new HashSet<Cell>{first};var todo=new Queue<Cell>();todo.Enqueue(first);
            while(todo.Count>0){var p=todo.Dequeue();foreach(var q in new[]{new Cell(p.x+1,p.y),new Cell(p.x-1,p.y),new Cell(p.x,p.y+1),new Cell(p.x,p.y-1)})if(rest.Remove(q)){component.Add(q);todo.Enqueue(q);}}
            if(component.All(c=>c.x>0&&c.y>0&&c.x<width-1&&c.y<height-1))interior.UnionWith(component);
        }
        return interior;
    }
    static HashSet<int> Group(BoardInput b,int root)
    {
        var ids=new HashSet<int>{root};bool changed;
        do {changed=false;foreach(var l in b.links)if(ids.Contains(l.holeId1)||ids.Contains(l.holeId2)){changed|=ids.Add(l.holeId1);changed|=ids.Add(l.holeId2);}}while(changed);return ids;
    }
    static int? NextUse(List<SolverMove> moves,int current,Target t)
    {
        for(int i=current+1;i<moves.Count;i++)foreach(var e in moves[i].eaten)
            if(e.holeId==t.hole && (e.catId>=0?$"cat:{e.catId}":$"box:{e.boxId}:{e.boxIndex}")==t.key)return i+1;
        return null;
    }
}
