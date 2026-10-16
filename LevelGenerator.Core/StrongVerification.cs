using System.Diagnostics;
using System.Globalization;
using CatDom.CoreSolver;
using Newtonsoft.Json;
using Ref = CatDom.Reference.V42;
using RefIO = CatDom.Reference.V42.IO;

namespace CatDom.LevelGenerator;

/// <summary>
/// A deliberately scoped ALTERNATIVE-PLAN search, not a global necessity/uniqueness proof.
/// Reorders the drags of an independently certified plan (preserving each actor's drag order),
/// and requires a second complete V42 replay for every positive bypass witness.
/// Unlike a superficial geometric similarity test, this changes play order on the SAME board.
/// Search misses alternatives with new paths, new stops, or different numbers of drags.
/// </summary>
public sealed class StrongVerificationCheck
{
    public string id = "";
    public string pattern = "";
    public int blockingHole = -1;
    public int consumingHole = -1;
    public string target = "";
    public int originalBreakDrag;
    public int originalConsumeDrag;
    public string verdict = "Unknown";
    public string explanation = "";
    public string? alternatePlanHash;
    public string? alternatePlanFile;
    public int alternateDrags;
    public int sourceBoxIndex = -1;
    public List<AlternativeDrag> alternatePlan = new();
}
public sealed class AlternativeDrag
{
    public int holeId;
    public Point start;
    public List<Point> path = new();
}
public sealed class StrongVerificationReport
{
    public string schemaVersion = "levelgen-strong-verification/0.1";
    public string scope = "Bounded reorder of already-certified drags; per-hole drag order preserved; same paths, endpoints and level. Not all possible gameplay plans.";
    public string inputHash = "";
    public string catalogHash = "";
    public string pattern = "";
    public string originalPlanHash = "";
    public string originalCertification = "NotRun";
    public string originalWitnessEvidence = "Unknown";
    public string verdict = "Unknown";
    public string difficultyRating = "Unrated";
    public string qualityRating = "Unrated";
    public string stopReason = "NotRun";
    public string limitations = "No global necessity/uniqueness/optimality inference. A proven bypass applies to the specified ordering constraint, NOT the whole level's difficulty.";
    public int searchedPlans;
    public int independentReplayAttempts;
    public int validAlternativeWins;
    public int maxPlans;
    public int maxSearchMs;
    public double searchElapsedMs;
    public double solveElapsedMs;
    public List<StrongVerificationCheck> checks = new();
    public List<string> notes = new();
}

public static class BoundedPlanReorder
{
    /// <summary>Explore reorders by adjacent swaps of DIFFERENT actors. No mutable state is shared.
    /// Limits count on visited plans, even those invalid in the real game, to avoid false exhaustiveness.</summary>
    internal static (int visited,string stop) Explore(IReadOnlyList<int> actors,int maxStates,int maxMs,
        Func<int[],bool> probe,CancellationToken token=default,IEnumerable<int[]>? preferredOrders=null)
    {
        if(maxStates<1 || maxMs<1)throw new ArgumentOutOfRangeException(nameof(maxStates));
        var original=Enumerable.Range(0,actors.Count).ToArray();
        var seen=new HashSet<string>(StringComparer.Ordinal) { Key(original) };
        var frontier=new Queue<int[]>();frontier.Enqueue(original);
        if(preferredOrders!=null)
            foreach(var choice in preferredOrders)
                if(choice.Length==actors.Count && choice.Distinct().Count()==actors.Count &&
                    choice.All(i=>i>=0 && i<actors.Count) &&
                    PreservesActorOrder(actors,choice) && seen.Add(Key(choice)))
                    frontier.Enqueue((int[])choice.Clone());
        var clock=Stopwatch.StartNew();
        var probed=0;
        while(frontier.Count>0)
        {
            if(token.IsCancellationRequested)return (probed,"Cancelled");
            if(clock.ElapsedMilliseconds>=maxMs)return (probed,"TimeBudget");
            if(probed>=maxStates)return (probed,"StateBudget");
            var order=frontier.Dequeue();
            // The original plan is known, but it is NOT a new alternative.
            if(probed>0 && probe(order))return (probed,"BypassFound");
            probed++;
            for(int i=0;i+1<order.Length;i++)
            {
                if(actors[order[i]]==actors[order[i+1]])continue;
                var child=(int[])order.Clone();
                (child[i],child[i+1])=(child[i+1],child[i]);
                if(seen.Add(Key(child)))frontier.Enqueue(child);
            }
        }
        return (probed,"ReorderScopeExhausted");
    }
    private static bool PreservesActorOrder(IReadOnlyList<int> actors,int[] order)
    {
        var last=new Dictionary<int,int>();
        foreach(var index in order)
        {
            var actor=actors[index];
            if(last.TryGetValue(actor,out var prior) && index<=prior)return false;
            last[actor]=index;
        }
        return true;
    }
    private static string Key(int[] x)=>string.Join(',',x);
}

public sealed class StrongBottleneckVerifier
{
    private readonly ShapeSet catalog;
    private readonly string catalogText;
    private readonly string catalogPath;
    public StrongBottleneckVerifier(string shapeCatalogPath)
    {
        catalogPath=shapeCatalogPath;
        catalog=ShapeSet.Load(catalogPath);
        catalogText=File.ReadAllText(catalogPath);
    }

    public StrongVerificationReport Verify(string levelJson,string pattern,int solveMs=15000,int searchMs=5000,int maxPlans=250,
        CancellationToken token=default)
    {
        CheckArgs(pattern,solveMs,searchMs,maxPlans);
        var report=NewReport(levelJson,pattern,searchMs,maxPlans);
        var validation=new LevelValidator(catalog).CheckJson(levelJson,25,32);
        if(!validation.valid)
        {
            report.verdict="InvalidLevel";
            report.notes.Add("Static validation: "+string.Join("; ",validation.errors));
            return report;
        }
        var certificate=new Certifier(catalogPath).Check(levelJson,solveMs,token);
        report.solveElapsedMs=certificate.elapsedMs;
        report.originalCertification=certificate.certified?"IndependentlyReplayed":certificate.solverStatus+"/"+certificate.replayStatus;
        if(!certificate.certified)
        {
            report.verdict="Uncertified";
            report.notes.Add("The source plan was not independently certified; alternative-search results are not available.");
            return report;
        }
        return VerifyCertified(levelJson,pattern,certificate.certifiedMoves,searchMs,maxPlans,token,report);
    }

    internal StrongVerificationReport VerifyCertified(string json,string pattern,IReadOnlyList<SolverMove> certifiedMoves,
        int searchMs,int maxPlans,CancellationToken token=default,StrongVerificationReport? initialized=null)
    {
        CheckArgs(pattern,1,searchMs,maxPlans);
        var report=initialized??NewReport(json,pattern,searchMs,maxPlans);
        // Caller-provided plans must be replay-certified by the caller (normally Certifier).
        // The report only records this status if supplied by the trusted generation pipeline.
        report.originalCertification="IndependentlyReplayed";
        report.originalPlanHash=PlanHash(certifiedMoves);
        if(certifiedMoves.Count==0)
        {
            report.verdict="UnknownNoWitness";
            report.notes.Add("No certified drags for alternative-ordering analysis.");return report;
        }
        AnalysisReport witness;
        try { witness=new WitnessAnalyzer(catalog).Analyze(JsonIO.Deserialize<LevelData>(json),certifiedMoves,report.inputHash,pattern); }
        catch(Exception ex)
        {
            report.verdict="UnknownAnalyzerFailure";
            report.notes.Add("Could not read certified event stream: "+ex.GetType().Name+": "+ex.Message);
            return report;
        }
        report.originalWitnessEvidence=witness.bottleneckEvidence;
        if(pattern=="RegionRelease")
        {
            // Each concrete task means: consumer source becomes reachable after blocker clears.
            // A full winning plan that actually consumes the SAME source while blocker is
            // still alive refutes the *ordering* claimed by this observation.
            foreach(var observation in witness.localRelief)
            {
                foreach(var source in observation.usedInContinuation)
                {
                    var split=source.Split(':');
                    if(split.Length!=3 || !int.TryParse(split[0],out var consumer) || !int.TryParse(split[2],out var srcId))continue;
                    var consumeIndex=FindConsumption(certifiedMoves,consumer,split[1],srcId,observation.drag);
                    if(consumeIndex<0)continue;
                    var check=new StrongVerificationCheck{
                        sourceBoxIndex=split[1]=="box"?FindBoxIndex(certifiedMoves,consumer,srcId,observation.drag): -1,
                        id=$"clear-h{observation.clearedHole}-before-{source}",pattern=pattern,
                        blockingHole=observation.clearedHole,consumingHole=consumer,
                        target=source,originalBreakDrag=observation.drag,originalConsumeDrag=consumeIndex+1,
                        explanation="Testing whether this particular source can be consumed while the cleared blocker remains alive."};
                    if(!report.checks.Any(x=>x.id==check.id))report.checks.Add(check);
                    if(report.checks.Count>=6)break;
                }
                if(report.checks.Count>=6)break;
            }
        }
        else if(witness.queueRevisits>0)
        {
            report.checks.Add(new StrongVerificationCheck{id="queue-revisit-not-needed",pattern=pattern,
                target="Full-win alternative with zero same-actor queue revisits",
                explanation="Check whether a complete certified reordering wins without a same-actor queue revisit."});
        }
        if(report.checks.Count==0)
        {
            report.verdict="UnknownNoMotifWitness";
            report.notes.Add("The original certified plan did not exhibit an analyzable instance of the intended pattern.");
            return report;
        }
        var refInput=RefIO.SolverJson.ReadLevel(json,RefIO.SolverJson.ReadCatalog(catalogText));
        var originalLevel=JsonIO.Deserialize<LevelData>(json);
        var timer=Stopwatch.StartNew();
        int wins=0,independent=0;
        // A budgeted bound applies across ALL checks, not one budget per check.
        bool Probe(int[] order)
        {
            if(token.IsCancellationRequested || timer.ElapsedMilliseconds>=searchMs)return false;
            var reordered=order.Select(i=>certifiedMoves[i]).ToArray();
            if(report.checks.All(c=>c.verdict=="BypassWitnessFound"))return true;
            // Avoid expensive full replays when a necessary move-order reversal is absent.
            var interesting=report.checks.Any(c=>c.verdict!="BypassWitnessFound" &&
                (c.pattern=="QueueRevisitParking" ||
                 Position(order,c.originalConsumeDrag-1)<Position(order,c.originalBreakDrag-1)));
            if(!interesting)return false;
            var toReplay=Convert(reordered);
            Ref.SolveResult played;
            try {played=Ref.CatLevelSolver.VerifyPlan(refInput,toReplay,
                Math.Max(0.005,(searchMs-timer.Elapsed.TotalMilliseconds)/1000.0));}
            catch(ArgumentException){return false;}
            independent++;
            if(played.status!=Ref.SolveStatus.Solved)return false;
            wins++;
            bool anyBypass=false;
            foreach(var check in report.checks.Where(c=>c.verdict!="BypassWitnessFound"))
            {
                if(check.pattern=="RegionRelease")
                {
                    int idx=Position(order,check.originalConsumeDrag-1);
                    if(idx>=Position(order,check.originalBreakDrag-1))continue;
                    if(!TestEarlyConsumption(refInput,originalLevel,toReplay,check,idx,timer,searchMs))continue;
                }
                else if(HasQueueRevisit(refInput,originalLevel,toReplay,timer,searchMs)!=false)continue;
                check.verdict="BypassWitnessFound";
                check.alternatePlanHash=PlanHash(reordered);
                check.alternateDrags=reordered.Length;
                check.alternatePlan=reordered.Select(m=>new AlternativeDrag{holeId=m.holeId,
                    start=new Point(m.start.x,m.start.y),path=m.path.Select(c=>new Point(c.x,c.y)).ToList()}).ToList();
                check.explanation="A full solution was independently replayed on the unchanged board, and this specific ordering requirement was bypassed. Other bottlenecks may remain.";
                anyBypass=true;
            }
            if(anyBypass)report.notes.Add("Positive full-win alternative: "+PlanHash(reordered));
            return report.checks.All(c=>c.verdict=="BypassWitnessFound");
        }
        var result=BoundedPlanReorder.Explore(certifiedMoves.Select(m=>m.holeId).ToArray(),
            maxPlans,searchMs,Probe,token,PreferredReorders(certifiedMoves,report.checks));
        report.searchedPlans=result.visited;
        report.independentReplayAttempts=independent;
        report.validAlternativeWins=wins;
        report.searchElapsedMs=timer.Elapsed.TotalMilliseconds;
        report.stopReason=result.stop;
        foreach(var check in report.checks.Where(c=>c.verdict!="BypassWitnessFound"))
        {
            check.verdict=result.stop switch{
                "Cancelled"=>"UnknownCancelled",
                "StateBudget" or "TimeBudget"=>"UnknownBudget",
                _=>"NoBypassInTestedReorders"};
            check.explanation="No full-win ordering bypass established in the tested finite reorder scope; other paths/actions and solutions were NOT exhaustively searched.";
        }
        report.verdict=report.checks.All(c=>c.verdict=="BypassWitnessFound")?"OrderingBypassFound":
            report.checks.Any(c=>c.verdict=="BypassWitnessFound")?"PartialOrderingBypassFound":
            "Unknown";
        if(result.stop=="ReorderScopeExhausted")
            report.notes.Add("All reachable adjacent reorders of the fixed move list were visited within this scope only; this is NOT proof of global necessity.");
        report.notes.Add("Reordering keeps the original path and endpoint of every drag and preserves each actor's drag order; different paths/extra moves may bypass undetected.");
        return report;
    }

    private static bool TestEarlyConsumption(Ref.BoardInput input,LevelData raw,List<Ref.SolverMove> plan,
        StrongVerificationCheck check,int endIndex,Stopwatch timer,int searchMs)
    {
        if(timer.ElapsedMilliseconds>=searchMs)return false;
        var prefix=plan.Take(endIndex+1).ToList();
        var state=Ref.CatLevelSolver.VerifyPlan(input,prefix,Math.Max(0.005,(searchMs-timer.Elapsed.TotalMilliseconds)/1000.0));
        if(state.status is Ref.SolveStatus.TimedOut or Ref.SolveStatus.InvalidInput || state.finalState==null)return false;
        var blocker=state.finalState.holes.FirstOrDefault(h=>h.id==check.blockingHole);
        if(blocker==null || blocker.finished || blocker.remaining<=0)return false;
        var parts=check.target.Split(':');int target=int.Parse(parts[2],CultureInfo.InvariantCulture);
        if(parts[1]=="cat")return !state.finalState.remainingCatIds.Contains(target);
        // The exact queue index must be consumed, not just an earlier item.
        if(target<0 || target>=raw.catBoxInfos.Count || check.sourceBoxIndex<0)return false;
        return state.finalState.boxes.Any(b=>b.id==target && b.consumed>check.sourceBoxIndex);
    }

    private static bool? HasQueueRevisit(Ref.BoardInput input,LevelData raw,List<Ref.SolverMove> plan,
        Stopwatch timer,int searchMs)
    {
        var consumed=new HashSet<(int actor,int box)>();
        var left=new HashSet<(int actor,int box)>();
        var prior=new int[raw.catBoxInfos.Count];
        // Check the initial auto-consumption state; it may advance queue heads even before drag 1.
        var first=Ref.CatLevelSolver.VerifyPlan(input,new List<Ref.SolverMove>(),Math.Max(0.005,(searchMs-timer.Elapsed.TotalMilliseconds)/1000.0));
        if(first.finalState==null)return null;
        foreach(var b in first.finalState.boxes)prior[b.id]=b.consumed;
        for(int i=0;i<plan.Count;i++)
        {
            if(timer.ElapsedMilliseconds>=searchMs)return null;
            var state=Ref.CatLevelSolver.VerifyPlan(input,plan.Take(i+1).ToList(),
                Math.Max(0.005,(searchMs-timer.Elapsed.TotalMilliseconds)/1000.0));
            if(state.finalState==null || state.status is Ref.SolveStatus.TimedOut or Ref.SolveStatus.InvalidInput)return null;
            var actor=plan[i].holeId;
            foreach(var b in state.finalState.boxes)
            {
                var key=(actor,b.id);
                if(b.consumed>prior[b.id])
                {
                    if(consumed.Contains(key) && left.Contains(key))return true;
                    consumed.Add(key);
                    left.Remove(key);
                }
                prior[b.id]=b.consumed;
            }
            foreach(var key in consumed.Where(k=>k.actor==actor).ToArray())
            {
                var hole=state.finalState.holes.FirstOrDefault(h=>h.id==actor);
                if(hole==null || hole.finished)continue;
                // Original geometry; a mouth is covered iff any footprint cell overlaps.
                var mouth=Mouth(raw.catBoxInfos[key.box]);
                // Use the source catalog? Here hole footprint is provided by input geometry.
                if(!input.holes[actor].footprint.Any(p=>hole.position.x+p.x==mouth.x && hole.position.y+p.y==mouth.y))left.Add(key);
            }
        }
        return false;
    }

    private static Point Mouth(BoxData box)
    {
        var delta=new[]{new Point(0,1),new Point(-1,0),new Point(0,-1),new Point(1,0)}[box.direction];
        return new Point(box.position.x+delta.x,box.position.y+delta.y);
    }
    // Place the original consuming drag immediately BEFORE the original break drag,
    // or delay the break drag until just AFTER consumption. Never transpose two drags
    // of the SAME actor: per-actor drag order is a hard constraint of this scope.
    internal static IEnumerable<int[]> PreferredReorders(IReadOnlyList<SolverMove> moves,
        IEnumerable<StrongVerificationCheck> checks)
    {
        int n=moves.Count;
        foreach(var check in checks.Where(c=>c.pattern=="RegionRelease"))
        {
            int breaker=check.originalBreakDrag-1, consumer=check.originalConsumeDrag-1;
            if(breaker<0 || consumer<0 || breaker>=n || consumer>=n || consumer<=breaker)continue;
            // Move the entire relevant sequence of the consumer actor before the
            // blocker clear, NOT merely one drag (which may violate actor order).
            var consumerActor=moves[consumer].holeId;
            var promoted=Enumerable.Range(breaker,consumer-breaker+1)
                .Where(i=>moves[i].holeId==consumerActor).ToArray();
            var moveBefore=Enumerable.Range(0,n).ToList();
            moveBefore.RemoveAll(i=>promoted.Contains(i));
            var start=moveBefore.IndexOf(breaker);
            moveBefore.InsertRange(start,promoted);
            if(PreservesActorOrder(moves,moveBefore))yield return moveBefore.ToArray();
            // Symmetrically defer blocker actions until after the consumer has
            // accessed the target; no same-actor order is ever reversed.
            var blockerActor=moves[breaker].holeId;
            var postponed=Enumerable.Range(breaker,consumer-breaker+1)
                .Where(i=>moves[i].holeId==blockerActor).ToArray();
            var delay=Enumerable.Range(0,n).ToList();
            delay.RemoveAll(i=>postponed.Contains(i));
            var after=delay.IndexOf(consumer)+1;
            delay.InsertRange(after,postponed);
            if(PreservesActorOrder(moves,delay))yield return delay.ToArray();
        }
    }
    private static bool PreservesActorOrder(IReadOnlyList<SolverMove> moves,List<int> permutation)
    {
        var last=new Dictionary<int,int>();
        foreach(var idx in permutation)
        {
            var actor=moves[idx].holeId;
            if(last.TryGetValue(actor,out var prev) && idx<=prev)return false;
            last[actor]=idx;
        }
        return true;
    }
    private static int FindBoxIndex(IReadOnlyList<SolverMove> moves,int actor,int box,int afterDrag)
    {
        for(int i=Math.Max(0,afterDrag);i<moves.Count;i++)
        {
            var ev=moves[i].eaten?.FirstOrDefault(e=>e.holeId==actor && e.boxId==box);
            if(ev!=null)return ev.boxIndex;
        }
        return -1;
    }
    private static int FindConsumption(IReadOnlyList<SolverMove> moves,int actor,string kind,int source,int afterDrag)
    {
        for(int i=Math.Max(0,afterDrag);i<moves.Count;i++)
            if(moves[i].eaten!=null && moves[i].eaten.Any(e=>e.holeId==actor &&
                (kind=="cat"?e.catId==source:e.boxId==source)))return i;
        return -1;
    }
    private static int Position(int[] permutation,int originalIndex)=>Array.IndexOf(permutation,originalIndex);
    private static List<Ref.SolverMove> Convert(IEnumerable<SolverMove> moves)=>moves.Select(m=>new Ref.SolverMove{
        holeId=m.holeId,start=new Ref.Cell(m.start.x,m.start.y),
        path=m.path.Select(p=>new Ref.Cell(p.x,p.y)).ToList(),
        eaten=new List<Ref.EatEvent>()}).ToList();
    private static string PlanHash(IEnumerable<SolverMove> moves)=>HashUtil.Text(JsonIO.Serialize(
        moves.Select(m=>new{m.holeId,m.start,m.path})));
    private StrongVerificationReport NewReport(string json,string pattern,int searchMs,int maxPlans)=>new(){
        inputHash=HashUtil.Text(json),catalogHash=catalog.Sha256,pattern=pattern,
        maxSearchMs=searchMs,maxPlans=maxPlans};
    private static void CheckArgs(string pattern,int solveMs,int searchMs,int maxPlans)
    {
        if(pattern != "RegionRelease" && pattern != "QueueRevisitParking")throw new ArgumentException("Unsupported pattern: "+pattern);
        if(solveMs<1 || searchMs<1 || maxPlans<1 || maxPlans>5000)
            throw new ArgumentOutOfRangeException(nameof(searchMs),"Budgets must be positive and maxPlans <= 5000");
    }
}

/// <summary>Offline verifier for previously certified batches. Does not change the generation algorithm,
/// gameplay JSON, or the levels/ directory; writes new verification artifacts only.</summary>
public sealed class StrongVerificationBatch
{
    public string schemaVersion="levelgen-strong-batch/0.1";
    public int totalSeeds;
    public int eligible;
    public int verified;
    public int bypassFound;
    public int unknown;
    public int skipped;
    public double elapsedWallMs;
    public List<StrongVerificationBatchRow> rows=new();
}
public sealed class StrongVerificationBatchRow
{
    public int seed;
    public string status="";
    public string evidence="Unknown";
    public string? resultFile;
    public string? message;
    public double elapsedMs;
}
public static class StrongVerificationBatchRunner
{
    public static StrongVerificationBatch Run(string batchPath,string destination,string catalogPath,
        int workers=4,int solveMs=15000,int searchMs=5000,int maxPlans=250,CancellationToken token=default)
    {
        if(workers<1 || workers>32)throw new ArgumentOutOfRangeException(nameof(workers));
        var batch=JsonIO.Deserialize<BatchSummary>(File.ReadAllText(Path.Combine(batchPath,"batch-summary.json")));
        // Only our own verification artifacts are overwrite hazards. An output directory
        // can already exist with unrelated files (e.g., desktop.ini or other metadata).
        // Reject known verification artifacts, never overwrite results from an earlier run.
        if(Directory.Exists(destination))
        {
            var existingReports=new[]{"verification-summary.json","verification-summary.csv"}
                .Select(name=>Path.Combine(destination,name))
                .Where(File.Exists)
                .Concat(Directory.EnumerateFiles(destination,"*.verification.json",SearchOption.TopDirectoryOnly))
                .Take(3).ToArray();
            if(existingReports.Length>0)
                throw new IOException("Refusing to overwrite existing verification reports: "+
                    string.Join(", ",existingReports));
        }
        Directory.CreateDirectory(destination);
        var catalog=ShapeSet.Load(catalogPath);
        var summary=new StrongVerificationBatch{totalSeeds=batch.rows.Count};
        var sync=new object();var clock=Stopwatch.StartNew();
        void Write()
        {
            summary.rows.Sort((a,b)=>a.seed.CompareTo(b.seed));
            summary.elapsedWallMs=clock.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(destination,"verification-summary.json"),JsonIO.Serialize(summary));
            File.WriteAllText(Path.Combine(destination,"verification-summary.csv"),
                "seed,status,evidence,elapsedMs,message\n"+string.Join("\n",summary.rows.Select(r=>
                    string.Join(",",new[]{r.seed.ToString(CultureInfo.InvariantCulture),r.status,r.evidence,
                        r.elapsedMs.ToString("F0",CultureInfo.InvariantCulture),r.message??""}.Select(Csv)))));
        }
        lock(sync)Write();
        try
        {
            Parallel.ForEach(batch.rows,new ParallelOptions{MaxDegreeOfParallelism=workers,CancellationToken=token},row=>
            {
                var watch=Stopwatch.StartNew();var result=new StrongVerificationBatchRow{seed=row.seed};
                try
                {
                    if(row.status!="ReviewRequired" || !row.independentlyReplayed)
                    {
                        result.status="SkippedUncertified";
                        result.message="Seed was not independently certified in source batch";
                    }
                    else
                    {
                        var seedDir=Path.Combine(batchPath,"seed-"+row.seed.ToString(CultureInfo.InvariantCulture));
                        var report=JsonIO.Deserialize<GenerationReport>(File.ReadAllText(Path.Combine(seedDir,"report.json")));
                        var draft=File.ReadAllText(Path.Combine(seedDir,"certified-draft.json"));
                        if(report.status!="ReviewRequired" || !report.independentlyReplayed ||
                            report.inputHash!=HashUtil.Text(draft) || report.catalogHash!=catalog.Sha256)
                            throw new InvalidDataException("Certified draft/report/catalog hash mismatch");
                        var detailed=new StrongBottleneckVerifier(catalogPath).Verify(draft,report.pattern,solveMs,searchMs,maxPlans,token);
                        var resultPath=Path.Combine(destination,$"Level{row.seed:D5}.verification.json");
                        File.WriteAllText(resultPath,JsonIO.Serialize(detailed));
                        result.resultFile=Path.GetFullPath(resultPath);
                        result.status=detailed.verdict;
                        result.evidence=detailed.originalWitnessEvidence;
                        result.message=$"{detailed.stopReason}; plans={detailed.searchedPlans}; independentReplay={detailed.independentReplayAttempts}";
                    }
                }
                catch(OperationCanceledException)when(token.IsCancellationRequested){result.status="Cancelled";}
                catch(Exception ex){result.status="Error";result.message=ex.GetType().Name+": "+ex.Message;}
                result.elapsedMs=watch.Elapsed.TotalMilliseconds;
                lock(sync)
                {
                    summary.rows.Add(result);
                    if(result.status=="SkippedUncertified")summary.skipped++;
                    else {summary.eligible++;if(result.status!="Error" && result.status!="Cancelled")summary.verified++;}
                    if(result.status.Contains("BypassFound",StringComparison.Ordinal))summary.bypassFound++;
                    else if(result.status is "Unknown" or "UnknownNoMotifWitness" or "UnknownNoWitness")summary.unknown++;
                    Write();
                }
            });
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){ /* preserve partial summary */ }
        lock(sync)Write();
        return summary;
    }
    private static string Csv(string text)=>"\""+text.Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";
}
