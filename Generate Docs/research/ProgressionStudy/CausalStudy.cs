using CatDom.CoreSolver;
using CatDom.CoreSolver.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ref=CatDom.Reference.V42;

internal static class CausalStudy
{
    internal sealed record Witness(string mode,List<EngineMove> plan,List<BoardSnapshot> states,string replayStatus);
    internal static void Run(string root)
    {
        var catalogText=File.ReadAllText(Path.Combine(root,"IndependentSolver/IO/HoleShapes.json"));
        var catalog=SolverJson.ReadCatalog(catalogText);var referenceCatalog=Ref.IO.SolverJson.ReadCatalog(catalogText);
        var cases=new List<object>();var records=new List<BottleneckRecord>();var searches=new List<SearchResult>();
        foreach(string level in new[]{"Level00115","Level00098","Level00129"})
        {
            string json=File.ReadAllText(Path.Combine(root,"Levels",level+".json"));var input=SolverJson.ReadLevel(json,catalog);
            if(input.links.Count!=0||input.holes.Any(h=>h.color<0||h.color>9||h.layerColors.Length>1))throw new Exception("Outside classic analysis scope");
            var referenceInput=Ref.IO.SolverJson.ReadLevel(json,referenceCatalog);
            var completeContinuations=new List<object>();
            string Replay(List<EngineMove> moves)
            {
                var actual=CatLevelSolver.VerifyPlan(input,moves).finalState;
                var refMoves=JsonConvert.DeserializeObject<List<Ref.SolverMove>>(JsonConvert.SerializeObject(moves.Select(m=>new{m.holeId,m.start,m.path})));
                var replay=Ref.CatLevelSolver.VerifyPlan(referenceInput,refMoves,0,false);
                if(ClassicState(JToken.FromObject(actual))!=ClassicState(JToken.FromObject(replay.finalState)))throw new Exception("Independent prefix settlement differs");
                bool complete=actual.remainingCatIds.Count==0&&actual.holes.All(h=>h.finished)&&actual.boxes.All(b=>b.consumed==input.boxes[b.id].cats.Length);
                if(complete&&replay.status!=Ref.SolveStatus.Solved)throw new Exception("Independent full replay failed");
                return complete?"IndependentSolved":"IndependentPrefixMatched";
            }
            var witnesses=new List<Witness>();
            foreach(bool improved in new[]{false,true})
            {
                using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(25));
                var solved=improved?CatLevelSolver.SolveLevelWithProgress(json,cts.Token,null,0):CatLevelSolver.SolveLevel(json,cts.Token);
                if(solved.status!=SolverStatus.Solved)throw new Exception(level+" witness "+solved.status);
                var plan=solved.moves.Select(m=>new EngineMove{holeId=m.holeId,start=m.start,path=m.path,eaten=m.eaten}).ToList();
                string replay=Replay(plan);var states=new List<BoardSnapshot>();var prefix=new List<EngineMove>();
                states.Add(CatLevelSolver.VerifyPlan(input,prefix).finalState);
                foreach(var m in plan){prefix.Add(m);states.Add(CatLevelSolver.VerifyPlan(input,prefix).finalState);}
                witnesses.Add(new(improved?"improved":"hybrid",plan,states,replay));
                Console.WriteLine($"{level} {witnesses[^1].mode}: {plan.Count} drags; {replay}");
            }
            if(level=="Level00115")
            {
                AnalysisContracts.Tests(witnesses[0].states[0]);
                var improved=witnesses[1];
                // Use actual witnessed state/event predicates to locate the relocation, never a hardcoded drag as identity.
                int relocation=Enumerable.Range(1,improved.plan.Count).First(i=>
                    improved.plan[i-1].holeId==4 && improved.states[i-1].holes[4].position.Equals(new Cell(4,0))
                    && improved.states[i].holes[4].position.Equals(new Cell(0,3)) && !improved.states[i].holes[4].finished);
                int firstH6=improved.states.FindIndex(s=>s.holes[6].finished);
                var desired=improved.states[relocation];
                double Rank(BoardSnapshot s)
                {
                    double rank=0;
                    foreach(var h in s.holes.Where(h=>!h.finished&&h.id!=6))
                    {
                        var target=desired.holes[h.id];rank+=Math.Abs(h.position.x-target.position.x)+Math.Abs(h.position.y-target.position.y);
                    }
                    return rank+80*(s.remainingCatIds.Contains(6)?1:0)+80*(s.remainingCatIds.Contains(13)?1:0);
                }
                var a=TargetedSearch.Beam("Q115-A-local",input,improved.plan.Take(firstH6-1).ToList(),
                    s=>!s.remainingCatIds.Contains(6)&&!s.remainingCatIds.Contains(13)&&!s.holes[6].finished,
                    s=>s.holes[6].finished,Rank,Replay,"h2 consumes cat6 and cat13 while h6 remains live","clear(h6)",12,2500,18,35);
                searches.Add(a.result);Console.WriteLine($"Q115-A-local: {a.result.status}; {a.result.expanded} expanded; {a.result.stopReason}");
                bool OpenTargets(List<EngineMove> prefix)
                {
                    var access=AccessStudy.Measure(input,prefix);
                    return new[]{"cat:6","cat:13"}.All(key=>access.targets.Any(t=>t.hole==2&&t.key==key));
                }
                var start=improved.plan.Take(relocation-1).ToList();
                if(OpenTargets(start))throw new Exception("Relocation test starts after unlock");
                var b=TargetedSearch.Beam("Q115-B-final-reposition",input,start,
                    s=>!s.holes[4].finished&&s.holes[4].position.Equals(new Cell(0,3)),s=>s.holes[4].finished,
                    s=>Math.Abs(s.holes[4].position.x)+Math.Abs(s.holes[4].position.y-3),Replay,
                    "live h4 parks at(0,3), then verify both h2 first-consumption targets","clear(h4)",1,100,5,100,new(){4});
                if(b.plan!=null&&!OpenTargets(b.plan))throw new Exception("Parking did not unlock expected targets");
                searches.Add(b.result);
                void CheckWinContinuation(string question,List<EngineMove> prefix)
                {
                    if(AnalysisContracts.StateHash(CatLevelSolver.VerifyPlan(input,prefix).finalState)!=AnalysisContracts.StateHash(desired))throw new Exception("Alternative did not reach the same settled continuation state");
                    var full=new List<EngineMove>(prefix);full.AddRange(improved.plan.Skip(relocation));
                    var status=Replay(full);if(status!="IndependentSolved")throw new Exception("Alternative continuation did not win");
                    completeContinuations.Add(new{question,joinedStateHash=AnalysisContracts.StateHash(desired),fullPlanHash=AnalysisContracts.PlanHash(full),status});
                }
                if(b.plan!=null)CheckWinContinuation(b.result.id,b.plan);
                // A direct support segment is concrete; its individual actor necessity is a separate bounded question.
                int begin=firstH6+1;var basePrefix=improved.plan.Take(begin).ToList();
                var declaredEndpoints=new[]{0,7,2,3,5,4}.ToDictionary(id=>id,id=>desired.holes[id].position);
                foreach(int frozen in new[]{-1,0,7,2,3,5})
                {
                    var result=TargetedSearch.EndpointSchedule(frozen<0?"Q115-B-endpoint-baseline":$"Q115-B-endpoint-freeze-h{frozen}",input,basePrefix,
                        declaredEndpoints.Where(kv=>kv.Key!=frozen).ToDictionary(kv=>kv.Key,kv=>kv.Value),OpenTargets,Replay);
                    searches.Add(result.result);Console.WriteLine($"{result.result.id}: {result.result.status}; {result.result.expanded} expanded");
                    if(result.plan!=null)CheckWinContinuation(result.result.id,result.plan);
                }
                // Search before the witnessed board-source completion, not immediately before an inevitably finishing box visit.
                foreach(int actor in new[]{7,3})
                {
                    int boardCat=actor==7?9:12;
                    int firstBoardEat=Enumerable.Range(0,improved.plan.Count).First(i=>improved.plan[i].eaten.Any(e=>e.holeId==actor&&e.catId==boardCat));
                    int queueIndex=actor==7?0:1;
                    bool LiveConsumed(BoardSnapshot s)=>s.boxes[2].consumed>queueIndex&&!s.holes[actor].finished;
                    var result=TargetedSearch.Beam($"Q115-C-h{actor}",input,improved.plan.Take(firstBoardEat).ToList(),
                        s=>LiveConsumed(s)&&!input.holes[actor].footprint.Any(c=>s.holes[actor].position.x+c.x==input.boxes[2].mouth.x&&s.holes[actor].position.y+c.y==input.boxes[2].mouth.y),
                        s=>s.holes[actor].finished||!s.remainingCatIds.Contains(boardCat),
                        s=>100*(s.boxes[2].consumed<=queueIndex?1:0)+Math.Abs(s.holes[actor].position.x-4)+Math.Abs(s.holes[actor].position.y),Replay,
                        $"b2 index{queueIndex} consumed; h{actor} live and footprint parked off mouth; board cat{boardCat} preserved",
                        $"clear(h{actor}); consume(cat{boardCat})",10,1500,12,25);
                    searches.Add(result.result);Console.WriteLine($"{result.result.id}: {result.result.status}; {result.result.expanded} expanded");
                }
                // Scope-independent supply fact: the witnessed final queue unit cannot leave an already fully pre-fed hole live.
            }
            var queueEvents=new List<object>();var reliefChecks=new List<object>();
            foreach(var w in witnesses)
            {
                var oldData=JObject.Parse(File.ReadAllText(Path.Combine(root,$"Generate Docs/research/access-focus-{w.mode}.json")));
                var oldRow=oldData["levels"].Single(x=>(string)x["level"]==level);
                string legacyPlanHash=AnalysisContracts.Hash(JsonConvert.SerializeObject(w.plan.Select(m=>new{m.holeId,m.start,m.path,m.eaten})));
                if((string)oldRow["inputHash"]!=AnalysisContracts.Hash(json)||(string)oldRow["planHash"]!=legacyPlanHash)throw new Exception("Access witness provenance differs");
                var releaseEvent=oldRow["events"].First(e=>e["footprintReleaseTargets"].Any(t=>t["nextUse"].Type!=JTokenType.Null)
                    &&(level!="Level00115"||w.mode!="hybrid"||e["finished"].Any(h=>(int)h==4)));
                int drag=(int)releaseEvent["drag"];var prefix=w.plan.Take(drag).ToList();
                var actual=AccessStudy.Measure(input,prefix);var shadow=new HashSet<Cell>();
                foreach(var id in releaseEvent["finished"].Values<int>())
                {
                    var position=w.plan[drag-1].path[^1];
                    foreach(var cell in input.holes[id].footprint)shadow.Add(new(position.x+cell.x,position.y+cell.y));
                }
                var retained=AccessStudy.Measure(input,prefix,shadow);
                if(AnalysisContracts.StateHash(actual.snapshot)!=AnalysisContracts.StateHash(retained.snapshot))throw new Exception("Shadow changed remaining task set");
                var released=actual.targets.Where(t=>!retained.targets.Any(b=>b.key==t.key&&b.hole==t.hole)).ToArray();
                if(released.Length==0)throw new Exception("Recorded local relief did not reproduce");
                reliefChecks.Add(new{w.mode,drag,postStateHash=AnalysisContracts.StateHash(actual.snapshot),sameRemainingTaskCheck=true,
                    actualPairs=actual.targets.Count,withRetainedFootprintPairs=retained.targets.Count,
                    released=released.Select(t=>new{t.key,t.hole,nextUse=NextUse(w.plan,drag,t)}),independentFullWitness=w.replayStatus});
                for(int i=0;i<w.plan.Count;i++)
                    foreach(var e in w.plan[i].eaten.Where(e=>e.boxId>=0))
                    {
                        var after=w.states[i+1];int? parked=null;
                        if(!after.holes[e.holeId].finished)
                            for(int j=i+1;j<w.plan.Count;j++)
                            {
                                if(w.states[j+1].holes[e.holeId].finished)break;
                                var h=w.states[j+1].holes[e.holeId];var mouth=input.boxes[e.boxId].mouth;
                                if(w.plan[j].holeId==e.holeId&&!input.holes[h.id].footprint.Any(c=>h.position.x+c.x==mouth.x&&h.position.y+c.y==mouth.y)){parked=j+1;break;}
                            }
                        queueEvents.Add(new{w.mode,drag=i+1,e.holeId,e.boxId,e.boxIndex,
                            remaining=after.holes[e.holeId].remaining,live=!after.holes[e.holeId].finished,
                            nextLiveMoveOffMouth=parked,
                            stateHash=AnalysisContracts.StateHash(after),
                            unconsumedBoardSources=input.cats.Where(c=>c.color==input.holes[e.holeId].color&&after.remainingCatIds.Contains(c.id)).Select(c=>c.id).ToArray()});
                    }
            }
            string recordId=level+"-"+(level=="Level00115"?"lower-right":"queue-parking");
            var alternatives=witnesses.Select(w=>
            {
                int index=level=="Level00115"?Enumerable.Range(1,w.plan.Count).First(i=>w.mode=="hybrid"
                    ?w.states[i].holes[4].finished&&!w.states[i-1].holes[4].finished
                    :!w.states[i].holes[4].finished&&w.plan[i-1].holeId==4&&w.states[i-1].holes[4].position.Equals(new Cell(4,0))&&w.states[i].holes[4].position.Equals(new Cell(0,3))):w.plan.Count;
                return new BreakAlternative(level=="Level00115"?(w.mode=="hybrid"?"clear-h4":"park-live-h4"):"witness-"+w.mode,
                    level=="Level00115"?(w.mode=="hybrid"?"Clear":"Reposition"):"Composite",
                    AnalysisContracts.PlanHash(w.plan),AnalysisContracts.StateHash(w.states[0]),AnalysisContracts.StateHash(w.states[index]),"existing-witness",w.replayStatus);
            }).ToArray();
            var evidence=new[]{new Evidence("queue-rule",EvidenceClass.RuleNecessary,"Consume queue entries in order; completion not required for queue advance",
                "Levels/"+level+".json","single-layer ordinary queue","Does not establish completion dependencies"),
                new Evidence("replayed-witnesses",EvidenceClass.ObservedWitness,"Two complete plans independently replay to win; live queue visits recorded",
                "causal-study.json#cases","two solver modes of this input","No optimum, necessity or uniqueness claim"),
                new Evidence("geometry",level=="Level00115"?EvidenceClass.LocalDiagnostic:EvidenceClass.Hypothesis,
                    level=="Level00115"?"Clear or reposition h4 unlocks h2 lower-right targets":"Queue revisit may require parking coordination",
                    level=="Level00115"?"access-focus-*.json":"Levels/"+level+".json","state-local or structural only","Actor graph cycles do not prove cognitive difficulty")};
            var record=new BottleneckRecord(AnalysisContracts.Version,recordId,AnalysisContracts.Hash(json),AnalysisContracts.Hash(catalogText),"causal-study/0.1",
                AnalysisContracts.StateHash(witnesses[0].states[0]),"classic-goals/0.1",
                new[]{level=="Level00115"?"h2 lower-right board sources then b2 mouth":"partial supply across queue consumers and useful parking"},
                input.holes.Select(h=>h.id).ToArray(),new object[]{new{id="queue-order",type="QueueOrder",statement="Ordered consumption",evidenceRefs=new[]{"queue-rule"}},new{id="parking",type="Parking",statement="Useful footprint routing and staging",evidenceRefs=new[]{"geometry"}}},
                new object[]{new{taskId="support-attribution",necessityStatus="Unknown",reason="Bounded alternative study; no global minimal-support proof"}},
                alternatives,new{diagnostic=level=="Level00115"?"footprint-shadow and relocation plus continuation":"queue live-visit trace; no independent causal relief isolation yet",
                    continuation=level=="Level00115"?"h2 consumes remaining sources; h7/h3/h1 use b2":"Local clear unlocks a task consumed in same independently replayed plan; global queue knot remains Proposed",sameRemainingTaskCheck="Fresh state-local footprint-shadow checks in cases[].reliefChecks",
                    residualTasks=new[]{"Unknown global residual bottleneck attribution; inspect other branch/queue and parking tasks"},cleanupTasks=new[]{"Unknown global cleanup boundary; direct tail is witness-specific"}},
                evidence,new[]{"Minimal reasoning/lookahead Unknown","Global main bottleneck Unknown","DifficultyScore Unrated","Human fairness and queue visibility unverified"});
            AnalysisContracts.Validate(record);records.Add(record);
            if(level=="Level00115")
            {
                var scopedSupports=searches.Where(s=>s.id.StartsWith("Q115-B-endpoint-freeze-h")).Select(s=>(object)new{
                    taskId=s.id,actor=int.Parse(s.id.Split('h').Last()),necessityStatus="ScopedSearchNecessary",
                    reason="Removing this actor's one fixed endpoint action eliminates all declared schedules; not global necessity",evidenceRef=s.id}).ToArray();
                var supportEvidence=searches.Where(s=>s.id.StartsWith("Q115-B-endpoint-freeze-h")).Select(s=>new Evidence(s.id,EvidenceClass.ScopedSearchNecessary,
                    "Fixed endpoint schedule has no solution after excluding this actor", "causal-study.json#searches",s.scope.successorGeneration,
                    "Only this start snapshot and one move per declared actor to fixed anchors; other parking or repeated moves untested")).ToArray();
                record=record with {supportTasks=scopedSupports,evidence=record.evidence.Concat(supportEvidence).ToArray()};
                AnalysisContracts.Validate(record);records[^1]=record;
                var branch=record with {recordId="Level00115-b3-release",blockedTasks=new[]{"Expose b3 color6 and make mouth usable by h0"},affectedActors=new[]{6,0},
                    alternatives=witnesses.Select(w=>{int end=w.states.FindIndex(st=>st.holes[6].finished);return new BreakAlternative("consume-b3-head-and-clear-h6","Composite",AnalysisContracts.PlanHash(w.plan),AnalysisContracts.StateHash(w.states[end-1]),AnalysisContracts.StateHash(w.states[end]),"existing-witness",w.replayStatus);}).ToArray(),
                    relief=new{diagnostic="b3 head advances 0→6; h0 consumes next in both witnesses; footprint shadow supports mouth release",continuation="h0 follows h6",sameRemainingTaskCheck="Previous access-focus diagnostics",
                        residualTasks=new[]{"Lower-right h2/b2 still needs preparation in both witnesses; global dependency Unknown"},cleanupTasks=new[]{"Not established immediately after b3 release"}},
                    supportTasks=new object[]{new{taskId="b3-earlier-support",necessityStatus="Unknown",reason="No global attribution of prefix to this branch"}},
                    evidence=evidence.Select(e=>e.id=="geometry"?e with {statement="h6 disappearance releases next h0 queue access at b3",scope="post-h6-clear snapshot only"}:e).ToArray(),
                    unknowns=new[]{"Whether b3/h6 is a global prerequisite of lower-right Unknown","Contribution of earlier unrelated work Unknown"}};
                AnalysisContracts.Validate(branch);records.Add(branch);
            }
            cases.Add(new{level,inputHash=AnalysisContracts.Hash(json),witnesses=witnesses.Select(w=>new{w.mode,drags=w.plan.Count,planHash=AnalysisContracts.PlanHash(w.plan),w.replayStatus}),queueEvents,reliefChecks,completeContinuations});
            Save();
        }
        Save();
        void Save()=>File.WriteAllText(Path.Combine(root,"Generate Docs/research/causal-study.json"),JsonConvert.SerializeObject(new{
            schemaVersion=AnalysisContracts.Version,analysisVersion="causal-study/0.1",stateHashVersion="snapshot-canonical-v1",
            solverHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(CatLevelSolver).Assembly.Location))),
            referenceHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Ref.CatLevelSolver).Assembly.Location))),
            analysisAssemblyHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(CausalStudy).Assembly.Location))),
            records,searches,cases,difficultyScore=(double?)null,difficultyLabel="Unrated"},Formatting.Indented));
    }
    static int? NextUse(List<EngineMove> moves,int start,AccessStudy.Target target)
    {
        for(int i=start;i<moves.Count;i++)if(moves[i].eaten.Any(e=>e.holeId==target.hole&&(e.catId>=0?$"cat:{e.catId}":$"box:{e.boxId}:{e.boxIndex}")==target.key))return i+1;
        return null;
    }
    static string ClassicState(JToken state)=>AnalysisContracts.Canonical(new JObject {
        ["holes"]=new JArray(state["holes"].Select(h=>new JObject{["id"]=h["id"],["color"]=h["color"],["remaining"]=h["remaining"],["position"]=h["position"],["finished"]=h["finished"]})),
        ["remainingCatIds"]=state["remainingCatIds"],
        ["boxes"]=new JArray(state["boxes"].Select(b=>new JObject{["id"]=b["id"],["consumed"]=b["consumed"]}))
    });
}
