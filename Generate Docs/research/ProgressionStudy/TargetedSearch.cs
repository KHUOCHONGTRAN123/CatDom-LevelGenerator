using System.Diagnostics;
using CatDom.CoreSolver;

// Classic single-layer research adapter. Every successor is replayed by the product engine.
// First-eat drag truncation, beam pruning and restricted starts prohibit global impossibility claims.
internal static class TargetedSearch
{
    internal sealed record Node(List<EngineMove> plan, BoardSnapshot state, int depth);
    internal sealed record Outcome(SearchResult result, List<EngineMove> plan);
    internal static Outcome EndpointSchedule(string id,BoardInput input,List<EngineMove> prefix,Dictionary<int,Cell> endpoints,
        Func<List<EngineMove>,bool> goal,Func<List<EngineMove>,string> independentReplay)
    {
        var initial=CatLevelSolver.VerifyPlan(input,prefix).finalState;
        var scope=new SearchScope(AnalysisContracts.StateHash(initial),AnalysisContracts.PlanHash(prefix),
            "live h4 parks at(0,3) and both h2 sources accessible with others fixed","Only declared actors, each moved at most once to its declared endpoint",
            "goal-state independently replayed prefix; no full-win obligation",
            "all ordered subsets of actor/endpoints: "+string.Join(";",endpoints.OrderBy(e=>e.Key).Select(e=>$"h{e.Key}:{e.Value}")),
            "full snapshot + remaining action set",endpoints.Count,10000,0,true);
        int expanded=0,generated=0;List<EngineMove> found=null;
        void Visit(List<EngineMove> plan,HashSet<int> used)
        {
            if(found!=null)return;
            var state=CatLevelSolver.VerifyPlan(input,plan).finalState;
            if(!state.holes[4].finished&&state.holes[4].position.Equals(new Cell(0,3))&&goal(plan)){found=plan;return;}
            expanded++;
            foreach(var actor in endpoints.Keys.Where(a=>!used.Contains(a)).Order())
            {
                var move=Moves(input,plan,new(){actor}).FirstOrDefault(m=>m.path[^1].Equals(endpoints[actor]));
                if(move==null)continue;
                generated++;var next=new List<EngineMove>(plan){move};var nextUsed=new HashSet<int>(used){actor};
                Visit(next,nextUsed);if(found!=null)return;
            }
        }
        Visit(new(prefix),new());
        var result=new SearchResult(id,found==null?SearchStatus.ProvedAbsentWithinCompleteScope:SearchStatus.Found,scope,expanded,generated,0,
            found==null?"all declared schedules exhausted":"goal reached",
            found==null?"Every ordered subset of the fixed endpoint actions was recursively enumerated; deterministic shortest first-event path per action; not all board moves":"Positive schedule witness only",
            found==null?null:AnalysisContracts.PlanHash(found),found==null?null:AnalysisContracts.StateHash(CatLevelSolver.VerifyPlan(input,found).finalState),
            found==null?"None":independentReplay(found),found?.Skip(prefix.Count).Select(m=>new{m.holeId,m.start,end=m.path[^1]}).ToArray());
        AnalysisContracts.Validate(result);return new(result,found);
    }
    internal static IEnumerable<EngineMove> Moves(BoardInput input,List<EngineMove> prefix,HashSet<int> allowed=null)
    {
        if(input.links.Count!=0)throw new NotSupportedException("Linked groups are outside this research adapter");
        var engine=AccessStudy.Engine(input,prefix);var state=(BoardSnapshot)AccessStudy.Call(engine,"Snapshot");
        var board=(BoardInput)AccessStudy.Field(engine,"board");
        var valid=(bool[][])AccessStudy.Field(engine,"valid");var goals=(bool[][])AccessStudy.Field(engine,"goals");
        foreach(var h in state.holes.Where(h=>!h.finished && (allowed==null||allowed.Contains(h.id))))
        {
            var start=h.position;int index=start.y*input.width+start.x;
            if(!(bool)AccessStudy.Call(engine,"CanDrag",h.id,index))continue;
            var occupied=board.obstacles.ToHashSet();
            foreach(var other in state.holes.Where(x=>!x.finished&&x.id!=h.id))
                foreach(var cell in board.holes[other.id].footprint)occupied.Add(new(other.position.x+cell.x,other.position.y+cell.y));
            var paths=new Dictionary<Cell,List<Cell>>{{start,new(){start}}};var queue=new Queue<Cell>();queue.Enqueue(start);
            while(queue.Count>0)
            {
                var from=queue.Dequeue();
                foreach(var to in new[]{new Cell(from.x+1,from.y),new Cell(from.x-1,from.y),new Cell(from.x,from.y+1),new Cell(from.x,from.y-1)})
                {
                    if(to.x<0||to.y<0||to.x>=input.width||to.y>=input.height||paths.ContainsKey(to))continue;
                    int next=to.y*input.width+to.x,old=from.y*input.width+from.x;
                    int direction=to.y>from.y?0:to.x>from.x?1:to.y<from.y?2:3;
                    if(!valid[h.id][next]||!(bool)AccessStudy.Call(engine,"CanStep",h.id,old,next)
                        ||!(bool)AccessStudy.Call(engine,"CanEnterCat",h.id,next,direction)
                        ||board.holes[h.id].footprint.Any(c=>occupied.Contains(new Cell(to.x+c.x,to.y+c.y))))continue;
                    var path=new List<Cell>(paths[from]){to};paths[to]=path;
                    yield return new(){holeId=h.id,start=start,path=path};
                    if(!goals[h.id][next])queue.Enqueue(to);
                }
            }
        }
    }

    internal static Outcome Beam(string id,BoardInput input,List<EngineMove> prefix, Func<BoardSnapshot,bool> goal,
        Func<BoardSnapshot,bool> forbidden,Func<BoardSnapshot,double> rank,
        Func<List<EngineMove>,string> independentReplay,string goalDescription,string forbiddenDescription,
        int depthLimit=10,int nodeLimit=2000,int seconds=15,int beamWidth=35,HashSet<int> allowed=null)
    {
        BoardSnapshot initial;
        try {initial=CatLevelSolver.VerifyPlan(input,prefix).finalState;}
        catch(ArgumentException ex) {
            var invalidScope=new SearchScope("invalid",AnalysisContracts.PlanHash(prefix),goalDescription,forbiddenDescription,"goal-state prefix replay",
                "shortest path to each legal anchor; stop at first consume","full snapshot canonical-v1",depthLimit,nodeLimit,seconds,false);
            return new(new(id,SearchStatus.InvalidStartState,invalidScope,0,0,0,ex.Message,"",null,null,"None",null),null);
        }
        var scope=new SearchScope(AnalysisContracts.StateHash(initial),AnalysisContracts.PlanHash(prefix),goalDescription,forbiddenDescription,
            "goal-state prefix must independently replay; full-win continuation not required",
            $"shortest-path anchors, stop at first consume; beamWidth={beamWidth}; actors={(allowed==null?"all":string.Join(",",allowed.Order()))}",
            "full snapshot canonical-v1; best depth dedup",depthLimit,nodeLimit,seconds,false);
        var timer=Stopwatch.StartNew();int expanded=0,generated=0,frontierRemaining=1;string stop="depth limit";
        var frontier=new List<Node>{new(new(prefix),initial,0)};var visited=new Dictionary<string,int>{{scope.startStateHash,0}};
        for(int depth=0;depth<=depthLimit;depth++)
        {
            var nextFrontier=new List<Node>();
            foreach(var node in frontier)
            {
                if(forbidden(node.state))continue;
                if(goal(node.state))
                {
                    string replay=independentReplay(node.plan);
                    var summary=node.plan.Skip(prefix.Count).Select(m=>new{m.holeId,m.start,end=m.path[^1]}).ToArray();
                    var result=new SearchResult(id,SearchStatus.Found,scope,expanded,generated,frontier.Count,"goal reached",
                        "Positive witness only; no necessity/optimality claim",AnalysisContracts.PlanHash(node.plan),AnalysisContracts.StateHash(node.state),replay,summary);
                    AnalysisContracts.Validate(result);return new(result,node.plan);
                }
                if(depth==depthLimit)continue;
                if(expanded>=nodeLimit||timer.Elapsed.TotalSeconds>=seconds){stop=expanded>=nodeLimit?"node budget":"time budget";goto Finished;}
                expanded++;
                foreach(var move in Moves(input,node.plan,allowed))
                {
                    if(timer.Elapsed.TotalSeconds>=seconds){stop="time budget during successor generation";goto Finished;}
                    generated++;var plan=new List<EngineMove>(node.plan){move};
                    var state=CatLevelSolver.VerifyPlan(input,plan).finalState;
                    if(forbidden(state))continue;
                    var hash=AnalysisContracts.StateHash(state);
                    if(visited.TryGetValue(hash,out int oldDepth)&&oldDepth<=depth+1)continue;
                    visited[hash]=depth+1;nextFrontier.Add(new(plan,state,depth+1));
                }
            }
            frontierRemaining=nextFrontier.Count;
            frontier=nextFrontier.OrderBy(n=>rank(n.state)).ThenBy(n=>AnalysisContracts.StateHash(n.state),StringComparer.Ordinal).Take(beamWidth).ToList();
            if(frontier.Count==0){stop="restricted frontier exhausted (beam-pruned scope)";break;}
        }
        Finished:
        var notFound=new SearchResult(id,SearchStatus.NotFoundWithinBudget,scope,expanded,generated,frontierRemaining,stop,
            "Beam/first-event/depth/start restrictions; not exhaustive global search",null,null,"None",null);
        AnalysisContracts.Validate(notFound);return new(notFound,null);
    }
}
