using CatDom.LevelGenerator;
using Newtonsoft.Json.Linq;

var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
var catalogPath=Path.Combine(root,"IndependentSolver","IO","HoleShapes.json");
var catalog=ShapeSet.Load(catalogPath);
var validator=new LevelValidator(catalog);
var constructor=new CandidateConstructor(catalog);
var pass=0;
void Expect(bool cond,string message) { if(!cond) throw new Exception("FAIL: "+message);Console.WriteLine("PASS "+message);pass++; }
var requestJson=File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples","RegionRelease.request.json"));
var request=JsonIO.Deserialize<GeneratorRequest>(requestJson);
Expect(RequestValidator.Validate(JObject.Parse(requestJson),request,catalog).Count==0,"Example request passes config validation");
var noBudgets=JObject.Parse(requestJson);noBudgets.Remove("budgets");
Expect(RequestValidator.Validate(noBudgets,request,catalog).Any(x=>x.Contains("Missing request.budgets")),"Missing budget is an explicit InvalidConfig");
var repairRequestJson=File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples","RegionRelease.repair.request.json"));
Expect(RequestValidator.Validate(JObject.Parse(repairRequestJson),JsonIO.Deserialize<GeneratorRequest>(repairRequestJson),catalog).Count==0,
    "Repair evidence preference accepted as a versioned config policy");
var wrongRange=JsonIO.Deserialize<GeneratorRequest>(requestJson);wrongRange.targetPolicy.maxHoleFootprint=33;
Expect(RequestValidator.Validate(JObject.Parse(JsonIO.Serialize(wrongRange)),wrongRange,catalog).Any(x=>x.Contains("25..32")),"33 exceeds locked footprint range");
foreach(var pattern in new[]{"RegionRelease","QueueRevisitParking"})
{
    var source=JsonIO.Deserialize<GeneratorRequest>(File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples",pattern+".request.json")));
    int found=0,minimum=int.MaxValue,maximum=int.MinValue;
    for(int seed=1;seed<=80;seed++)
    {
        var level=constructor.TryBuild(source,seed,out _);
        if(level==null) continue;
        found++;
        var validation=validator.Check(level);
        Expect(validation.valid,$"{pattern} seed {seed}: geometry and supply invariants");
        Expect(validation.footprint is >=25 and <=32,$"{pattern} seed {seed}: footprint within 25..32");
        minimum=Math.Min(minimum,validation.footprint);maximum=Math.Max(maximum,validation.footprint);
        if(found==1)
        {
            var second=constructor.TryBuild(source,seed,out _);
            Expect(second!=null && JsonIO.Serialize(second)==JsonIO.Serialize(level),pattern+" deterministic sampling");
            var tampered=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(level));
            var h0=tampered.holeInfos[0]; var off=catalog.Footprints[h0.holeType][0];
            tampered.catInfos[0].position=new Point(h0.position.x+off.x,h0.position.y+off.y);
            Expect(!validator.Check(tampered).valid,pattern+" rejects cat-on-hole collision");
            tampered=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(level));
            tampered.catInfos[0].colorId=200;
            Expect(!validator.Check(tampered).valid,pattern+" rejects out-of-range color/supply");
            tampered=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(level));
            tampered.holeInfos[0].numOfCats[0]++;
            Expect(!validator.Check(tampered).valid,pattern+" rejects bad per-hole demand");
            tampered=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(level));
            tampered.catBoxInfos[0].colorIds[0].position=new Point(10,10);
            Expect(!validator.Check(tampered).valid,pattern+" rejects box queue position mismatch");
            var dynamic=JObject.Parse(JsonIO.Serialize(level));dynamic["someUnrecognizedMechanic"]=new JArray();
            Expect(!validator.CheckJson(dynamic.ToString()).valid,pattern+" refuses unknown fields");
            var strict=validator.Check(level,31,32);
            if(validation.footprint<31) Expect(!strict.valid,pattern+" rejects footprint below configured min");
        }
    }
    Expect(found>=1,pattern+" constructs at least one fresh candidate in 80 seeds");
    Console.WriteLine($"{pattern} constructed {found}/80; footprint {minimum}..{maximum}");
}
foreach(var id in new[]{"00115","00098","00129"})
{
    var file=Path.Combine(root,"Levels","Level"+id+".json");
    var original=validator.CheckJson(File.ReadAllText(file));
    Expect(original.valid,$"Original Level{id} passes static preset after 25..32 revision: {string.Join(";",original.errors)}");
}
// Milestone 2 smoke tests: repairs must preserve the parent and all static invariants.
foreach(var pattern in new[]{"RegionRelease","QueueRevisitParking"})
{
    var source=JsonIO.Deserialize<GeneratorRequest>(File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples",pattern+".request.json")));
    var mutator=new CandidateRepair(catalog);
    var witnessAnalyzer=new WitnessAnalyzer(catalog);
    int proposals=0;
    for(int seed=1;seed<=80 && proposals<12;seed++)
    {
        var level=constructor.TryBuild(source,seed,out _);
        if(level==null)continue;
        var original=JsonIO.Serialize(level);
        for(int step=0;step<source.budgets.maxRepairsPerAttempt;step++)
        {
            var next=mutator.Propose(level,source,seed,step,out var op,out _);
            Expect(JsonIO.Serialize(level)==original,pattern+" repair never mutates parent");
            if(next==null)continue;
            proposals++;
            var check=validator.Check(next);
            Expect(check.valid,pattern+" "+op+" repairs pass validator");
            Expect(mutator.PreservesIntent(next,source),pattern+" "+op+" preserves protected intent");
            Expect(HashUtil.Text(JsonIO.Serialize(next))!=HashUtil.Text(original),pattern+" "+op+" creates a new candidate hash");
            var repeat=mutator.Propose(level,source,seed,step,out var opAgain,out _);
            Expect(repeat!=null && op==opAgain && JsonIO.Serialize(repeat)==JsonIO.Serialize(next),pattern+" repair deterministic by seed+step");
            if(proposals>=12)break;
        }
    }
    Expect(proposals>0,pattern+" produced valid bounded repair proposals");
    var sample=constructor.TryBuild(source,Enumerable.Range(1,80).First(x=>constructor.TryBuild(source,x,out _)!=null),out _)!;
    var empty=witnessAnalyzer.Analyze(sample,Array.Empty<CatDom.CoreSolver.SolverMove>(),"synthetic-test-only",pattern);
    Expect(empty.bottleneckEvidence=="Unknown" && empty.difficultyRating=="Unrated" && empty.qualityRating=="Unrated",
        pattern+" empty witness cannot invent causal/difficulty/quality proof");
    Expect(empty.progressionEvidence=="Unknown",pattern+" empty witness cannot imply progression");
}
// Milestone 3: independent, fast checks for preview, batch reporting and no-overwrite behavior.
var reference=JsonIO.Deserialize<LevelData>(File.ReadAllText(Path.Combine(root,"Levels","Level00115.json")));
var standalone=LevelPreview.Render(reference,catalog);
Expect(standalone.StartsWith("<svg ") && standalone.Contains("NOT CERTIFIED"),
    "Static preview is SVG and never claims certification by default");
Expect(standalone.Contains("Hole footprint") && standalone.Contains("H0") && standalone.Contains("Board summary"),
    "Preview renders catalog holes and footprint summary");
var certifiedPreview=LevelPreview.Render(reference,catalog,true,"Seed & <test>");
Expect(certifiedPreview.Contains("CERTIFIED:") && certifiedPreview.Contains("Seed &amp; &lt;test&gt;"),
    "Preview escapes custom text and only labels certified when explicitly requested");
var invalidPreview=JsonIO.Deserialize<LevelData>(JsonIO.Serialize(reference));
invalidPreview.sizeX=6;
var rejected=false;
try{LevelPreview.Render(invalidPreview,catalog);}catch(InvalidDataException){rejected=true;}
Expect(rejected,"Preview rejects level that fails static gameplay validation");
var sampleBatch=new BatchSummary {startSeed=11,count=2,certifiedCount=1,witnessedCount=1,rows=new()
{
    new BatchRow {seed=11,status="ReviewRequired",pattern="RegionRelease",independentlyReplayed=true,
        bottleneckEvidence="ObservedLocalRelief",reportFile="test",draftFile="test",previewFile="test"},
    new BatchRow {seed=12,status="Error",pattern="QueueRevisitParking",error="quoted \"test\" & <test>"}
}};
var table=BatchRunner.ToCsv(sampleBatch.rows);
Expect(table.Contains("\"quoted \"\"test\"\" & <test>\""),"Batch CSV correctly escapes quoted error strings");
var gallery=BatchRunner.ToHtml(sampleBatch);
Expect(gallery.Contains("seed-11/preview.svg") && gallery.Contains("Certified") &&
    gallery.Contains("&lt;test&gt;") && !gallery.Contains("& <test>"),
    "Batch HTML links local previews and escapes untrusted errors");
var tmp=Path.Combine(Path.GetTempPath(),"catdom-batch-test-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
try
{
    File.WriteAllText(Path.Combine(tmp,"existing.txt"),"keep me");
    rejected=false;
    try{new BatchRunner(catalogPath).Run(requestJson,tmp,1);}catch(IOException){rejected=true;}
    Expect(rejected && File.Exists(Path.Combine(tmp,"existing.txt")),
        "Batch refuses to overwrite a non-empty experiment directory");
    rejected=false;
    try{new BatchRunner(catalogPath).Run(requestJson,Path.Combine(tmp,"new"),21);}catch(ArgumentOutOfRangeException){rejected=true;}
    Expect(rejected,"Batch rejects more than 20 seeds per invocation");
}
finally {Directory.Delete(tmp,true);}
// Milestone 3.1 parallel batch tests using a deterministic stub worker (no solver cost).
var parallelTmp=Path.Combine(Path.GetTempPath(),"catdom-batch-parallel-"+Guid.NewGuid().ToString("N"));
try
{
    ThreadPool.GetMinThreads(out var oldMinWorkers,out var oldMinIo);
    ThreadPool.SetMinThreads(Math.Max(4,oldMinWorkers),oldMinIo);
    try
    {
    var active=0;
    var peak=0;
    var seen=new System.Collections.Concurrent.ConcurrentDictionary<int,int>();
    GenerationReport FakeRun(string json,string directory,CancellationToken token)
    {
        var seed=JObject.Parse(json).Value<int>("seed");
        var concurrent=Interlocked.Increment(ref active);
        int observed;
        do
        {
            observed=Volatile.Read(ref peak);
            if(observed>=concurrent)break;
        } while(Interlocked.CompareExchange(ref peak,concurrent,observed)!=observed);
        try
        {
            seen.AddOrUpdate(seed,1,(_,n)=>n+1);
            // Uneven delays deliberately produce out-of-order completions.
            Thread.Sleep(seed%2==0?70:110);
            return new GenerationReport {status="BudgetExhausted",seed=seed,pattern="RegionRelease"};
        }
        finally {Interlocked.Decrement(ref active);}
    }
    var testBatch=new BatchRunner(catalogPath,FakeRun);
    var result=testBatch.Run(requestJson,Path.Combine(parallelTmp,"multi"),5,maxWorkers:3);
    Expect(result.count==5 && result.rows.Count==5 && result.maxWorkers==3,
        "Parallel batch honors requested count and max worker setting");
    Expect(peak>=2 && peak<=3,"Parallel batch really overlaps workers and respects concurrency cap");
    Expect(result.rows.Select(r=>r.seed).SequenceEqual(Enumerable.Range(request.seed,5)),
        "Completed rows export in ascending seed order despite concurrent completion");
    Expect(result.rows.All(r=>seen[r.seed]==1) && seen.Count==5,
        "Exactly one independent worker executes each seed");
    var snapshot=JsonIO.Deserialize<BatchSummary>(File.ReadAllText(Path.Combine(parallelTmp,"multi","batch-summary.json")));
    Expect(snapshot.rows.Select(r=>r.seed).SequenceEqual(result.rows.Select(r=>r.seed)) &&
        snapshot.maxWorkers==3,"Persistent summary is ordered and records concurrency setting");
    peak=0;
    var serial=testBatch.Run(requestJson,Path.Combine(parallelTmp,"serial"),3,maxWorkers:1);
    Expect(peak==1 && serial.rows.Count==3 && serial.maxWorkers==1,
        "One-worker mode preserves sequential execution");
    rejected=false;
    try{testBatch.Run(requestJson,Path.Combine(parallelTmp,"invalid"),3,maxWorkers:4);}catch(ArgumentOutOfRangeException){rejected=true;}
    Expect(rejected,"Cannot request more workers than seeds");
    using var stopped=new CancellationTokenSource();
    stopped.Cancel();
    var cancelled=testBatch.Run(requestJson,Path.Combine(parallelTmp,"stopped"),5,stopped.Token,maxWorkers:3);
    Expect(cancelled.rows.Count==0 && File.Exists(Path.Combine(parallelTmp,"stopped","batch-summary.json")),
        "Cancellation before scheduling leaves a truthful partial summary");
    }
    finally {ThreadPool.SetMinThreads(oldMinWorkers,oldMinIo);}
}
finally {if(Directory.Exists(parallelTmp))Directory.Delete(parallelTmp,true);}
// Milestone 4.1b playtest export: exported files are game-format JSON only.
var exportTmp=Path.Combine(Path.GetTempPath(),"catdom-batch-export-"+Guid.NewGuid().ToString("N"));
try
{
    var referenceJson=File.ReadAllText(Path.Combine(root,"Levels","Level00115.json"));
    GenerationReport FakeCertified(string json,string directory,CancellationToken token)
    {
        var seed=JObject.Parse(json).Value<int>("seed");
        if(seed!=request.seed)return new GenerationReport{status="BudgetExhausted",seed=seed};
        var draft=Path.GetFullPath(Path.Combine(directory,"certified-draft.json"));
        File.WriteAllText(draft,referenceJson);
        return new GenerationReport {status="ReviewRequired",seed=seed,independentlyReplayed=true,
            inputHash=HashUtil.Text(referenceJson),catalogHash=catalog.Sha256,draftFile=draft};
    }
    var run=new BatchRunner(catalogPath,FakeCertified).Run(requestJson,exportTmp,2,maxWorkers:2);
    var exported=Path.Combine(exportTmp,"levels",$"Level{request.seed:D5}.json");
    Expect(run.certifiedCount==1 && run.exportedCount==1 && File.Exists(exported),
        "Only certified seed is exported in a dedicated levels folder");
    Expect(Directory.GetFiles(Path.Combine(exportTmp,"levels")).Length==1 &&
        File.ReadAllText(exported)==referenceJson,
        "Playtest level preserves exact game-format JSON; folder has no metadata files");
    Expect(!File.Exists(Path.Combine(exportTmp,"levels",$"Level{request.seed+1:D5}.json")),
        "Unverified seed never appears in the exported levels folder");
    Expect(run.rows.Single(r=>r.seed==request.seed).exportedLevelFile==exported,
        "Exported level path is reported per seed");
}
finally {if(Directory.Exists(exportTmp))Directory.Delete(exportTmp,true);}

// Milestone 4.1b: historic requests remain valid, guided mode is opt-in.
Expect(!request.operatorPolicy.failureAware,"Existing request stays baseline / failure-aware disabled");
var guidedJson=File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples","RegionRelease.guided.request.json"));
var guided=JsonIO.Deserialize<GeneratorRequest>(guidedJson);
Expect(guided.operatorPolicy.failureAware && RequestValidator.Validate(JObject.Parse(guidedJson),guided,catalog).Count==0,
    "Guided request enables failure-aware repair without changing v0.1 config contract");
var originalPolicy=JsonIO.Deserialize<GeneratorRequest>(repairRequestJson);
Expect(GeneratorSeeding.SamplingConfigHash(originalPolicy)==GeneratorSeeding.SamplingConfigHash(guided) &&
    GeneratorSeeding.AttemptSeed(115,GeneratorSeeding.SamplingConfigHash(originalPolicy),0)==
    GeneratorSeeding.AttemptSeed(115,GeneratorSeeding.SamplingConfigHash(guided),0),
    "Baseline and guided A/B share identical constructor sampling for same seed");
var invalidFlag=JObject.Parse(guidedJson);
invalidFlag["operatorPolicy"]!["failureAware"]="sure";
Expect(RequestValidator.Validate(invalidFlag,JsonIO.Deserialize<GeneratorRequest>(guidedJson),catalog).Any(e=>e.Contains("failureAware")),
    "Guided flag rejects non-boolean configuration");
Expect(SolverFailureClassifier.Classify("NoNextCatReachable","No movable component has a consumption target.")=="NoMovableConsumptionTarget",
    "Diagnostic categorizes missing consumption targets");
Expect(SolverFailureClassifier.Classify("NoNextCatReachable","All reachable parking states exhausted without a consumption event.")=="ParkingSearchExhausted",
    "Diagnostic distinguishes parking exhaustion");
Expect(SolverFailureClassifier.Classify("Cancelled","timeout") == "TimeoutOrCancelled",
    "Timeout is never labeled unsolvable");
var guidedMutator=new CandidateRepair(catalog);
for(int testSeed=1;testSeed<=40;testSeed++)
{
    var candidate=constructor.TryBuild(guided,testSeed,out _);
    if(candidate==null)continue;
    var parentSnapshot=JsonIO.Serialize(candidate);
    var proposal=guidedMutator.Propose(candidate,guided,testSeed,0,out _,out _,"NoMovableConsumptionTarget");
    Expect(parentSnapshot==JsonIO.Serialize(candidate),"Guided repair never mutates the parent");
    if(proposal!=null)
    {
        Expect(validator.Check(proposal).valid && guidedMutator.PreservesIntent(proposal,guided),
            "Guided repair remains statically valid and protected-intent preserving");
        var repeat=guidedMutator.Propose(candidate,guided,testSeed,0,out _,out _,"NoMovableConsumptionTarget");
        Expect(repeat!=null && JsonIO.Serialize(proposal)==JsonIO.Serialize(repeat),
            "Guided proposal deterministic by seed and step");
        break;
    }
}
var diagnosisTmp=Path.Combine(Path.GetTempPath(),"catdom-diagnose-"+Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(diagnosisTmp);
    Directory.CreateDirectory(Path.Combine(diagnosisTmp,"seed-1"));
    var exampleReport=new GenerationReport{
        seed=1,status="ReviewRequired",attempts=2,repairsProposed=2,repairsCertified=1,
        diagnostics=new GenerationDiagnostics{
            solverCalls=3,solverReplayMs=123,constructorMs=7,repairProposalMs=4,
            solverStatuses=new(){{"Solved",1},{"NoNextCatReachable",2}},
            solverFailureCategories=new(){{"Solved",1},{"ParkingSearchExhausted",2}},
            repairOperatorsProposed=new(){{"MoveWall",2}},
            repairOperatorsCertified=new(){{"MoveWall",1}}
        }
    };
    File.WriteAllText(Path.Combine(diagnosisTmp,"seed-1","report.json"),JsonIO.Serialize(exampleReport));
    File.WriteAllText(Path.Combine(diagnosisTmp,"batch-summary.json"),JsonIO.Serialize(new BatchSummary{
        count=1,certifiedCount=1,elapsedWallMs=200,rows=new(){new BatchRow{seed=1,status="ReviewRequired",elapsedMs=195}}
    }));
    var diagnosed=BenchmarkDiagnostics.Read(diagnosisTmp);
    Expect(diagnosed.reportsWithPhaseTiming==1 && diagnosed.totalSolverCalls==3 && diagnosed.solverReplayMs==123,
        "Offline diagnostics uses instrumented timing and solver outcomes");
    Expect(diagnosed.repairOperatorsCertified.GetValueOrDefault("MoveWall")==1 &&
        BenchmarkDiagnostics.ToMarkdown(diagnosed).Contains("ParkingSearchExhausted"),
        "Offline Markdown includes per-operator evidence and diagnosed reasons");
}
finally {if(Directory.Exists(diagnosisTmp))Directory.Delete(diagnosisTmp,true);}
// Milestone 4.2: bounded alternative search is scoped, deterministic and never
// labels an unsearched global strategy as impossible.
var reorderActors=new[]{0,1,0};
var reorderOrders=new List<string>();
var reorderResult=BoundedPlanReorder.Explore(reorderActors,20,1000,order=>
{
    var key=string.Join(",",order);
    reorderOrders.Add(key);
    return key=="1,0,2";
});
Expect(reorderResult.stop=="BypassFound" && reorderOrders.Contains("1,0,2"),
    "Bounded reorder explores a different-actor ordering without changing same-actor order");
Expect(!reorderOrders.Contains("2,1,0"),
    "Adjacent swaps cannot reverse the relative order of two drags of the same hole");
var exhausted=BoundedPlanReorder.Explore(new[]{0,1,2},1,1000,_=>false);
Expect(exhausted.stop=="StateBudget","Alternative-search budget is explicit, not a proof of no solutions");
using(var cancelledSearch=new CancellationTokenSource())
{
    cancelledSearch.Cancel();
    var cancelledResult=BoundedPlanReorder.Explore(new[]{0,1},100,1000,_=>false,cancelledSearch.Token);
    Expect(cancelledResult.stop=="Cancelled","Alternative-search cancellation preserves truthful status");
}
var suggested=new List<CatDom.CoreSolver.SolverMove>
{
    new(){holeId=0},new(){holeId=1},new(){holeId=2},new(){holeId=0}
};
var reorderingCheck=new StrongVerificationCheck {pattern="RegionRelease",originalBreakDrag=2,originalConsumeDrag=3};
Expect(StrongBottleneckVerifier.PreferredReorders(suggested,new[]{reorderingCheck}).Any(
    x=>Array.IndexOf(x,2)<Array.IndexOf(x,1)),
    "Focused alternative search seeds source-before-breaker schedules");
var sameActor=new StrongVerificationCheck {pattern="RegionRelease",originalBreakDrag=1,originalConsumeDrag=4};
Expect(!StrongBottleneckVerifier.PreferredReorders(suggested,new[]{sameActor}).Any(),
    "Focused seeds preserve per-hole drag chronology");
var sameHoleOnly=new List<CatDom.CoreSolver.SolverMove>{new(){holeId=7},new(){holeId=7}};
Expect(StrongBottleneckVerifier.PreferredReorders(sameHoleOnly,new[]{
    new StrongVerificationCheck{pattern="RegionRelease",originalBreakDrag=1,originalConsumeDrag=2}}).ToArray().Length==0,
    "Same-hole breaker and consumer yield no reorder even when removal would empty the schedule");
var interleaved=new List<CatDom.CoreSolver.SolverMove>
{
    new(){holeId=0},new(){holeId=1},new(){holeId=2},new(){holeId=3},
    new(){holeId=1},new(){holeId=2},new(){holeId=0}
};
var interleavedChecks=new[]{
    new StrongVerificationCheck{pattern="RegionRelease",originalBreakDrag=1,originalConsumeDrag=7},
    new StrongVerificationCheck{pattern="RegionRelease",originalBreakDrag=2,originalConsumeDrag=6}};
var focusedOrders=StrongBottleneckVerifier.PreferredReorders(interleaved,interleavedChecks).ToArray();
Expect(focusedOrders.Length==2 && focusedOrders[0].SequenceEqual(new[]{0,2,5,1,3,4,6}) &&
    focusedOrders[1].SequenceEqual(new[]{0,2,3,5,1,4,6}),
    "Skipping same-hole checks still permits consumer promotion and breaker delay across different holes");
Expect(focusedOrders.All(order=>order.OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,interleaved.Count)) &&
    interleaved.Select(m=>m.holeId).Distinct().All(actor=>
        order.Where(i=>interleaved[i].holeId==actor).SequenceEqual(
            Enumerable.Range(0,interleaved.Count).Where(i=>interleaved[i].holeId==actor)))),
    "Both focused permutations retain every drag exactly once and preserve each hole's action order");
var strongVerifier=new StrongBottleneckVerifier(catalogPath);
var noWitness=strongVerifier.VerifyCertified(File.ReadAllText(Path.Combine(root,"Levels","Level00115.json")),
    "RegionRelease",Array.Empty<CatDom.CoreSolver.SolverMove>(),100,5);
Expect(noWitness.verdict=="UnknownNoWitness" && noWitness.difficultyRating=="Unrated" && noWitness.qualityRating=="Unrated",
    "No certified plan cannot yield strong bottleneck or quality claims");
var invalidLevel=JsonIO.Deserialize<LevelData>(File.ReadAllText(Path.Combine(root,"Levels","Level00115.json")));
invalidLevel.sizeX=6;
var invalidResult=strongVerifier.Verify(JsonIO.Serialize(invalidLevel),"RegionRelease",100,100,3);
Expect(invalidResult.verdict=="InvalidLevel" && invalidResult.originalCertification=="NotRun",
    "Invalid level is rejected before costly solving");
var verifyTmp=Path.Combine(Path.GetTempPath(),"catdom-m42-batch-"+Guid.NewGuid().ToString("N"));
try
{
    var sourceDir=Path.Combine(verifyTmp,"old");
    var targetDir=Path.Combine(verifyTmp,"verification");
    Directory.CreateDirectory(sourceDir);
    File.WriteAllText(Path.Combine(sourceDir,"batch-summary.json"),JsonIO.Serialize(new BatchSummary{
        count=1,rows=new(){new BatchRow{seed=1,status="BudgetExhausted"}}
    }));
    // An unrelated file must not prevent verification in a new output folder.
    Directory.CreateDirectory(targetDir);
    File.WriteAllText(Path.Combine(targetDir,"unrelated-note.txt"),"This is not a verification artifact.");
    Directory.CreateDirectory(Path.Combine(targetDir,"metadata"));
    var skippedBatch=StrongVerificationBatchRunner.Run(sourceDir,targetDir,catalogPath,workers:2,solveMs:100,searchMs:100,maxPlans:5);
    Expect(skippedBatch.skipped==1 && skippedBatch.eligible==0 && skippedBatch.rows[0].status=="SkippedUncertified",
        "Offline alternative verification does not process uncertified candidates");
    Expect(File.Exists(Path.Combine(targetDir,"verification-summary.json")) &&
        !Directory.Exists(Path.Combine(sourceDir,"levels")),
        "Verification writes separate reports without changing level/game files");
    bool refusedExistingVerification=false;
    try {StrongVerificationBatchRunner.Run(sourceDir,targetDir,catalogPath,workers:2,solveMs:100,searchMs:100,maxPlans:5);}
    catch(IOException) {refusedExistingVerification=true;}
    Expect(refusedExistingVerification,"Existing verification report cannot be overwritten");
}
finally {if(Directory.Exists(verifyTmp))Directory.Delete(verifyTmp,true);}
// M4.3: opt-in quality-guided candidate selection; no fake numeric difficulty rating.
var qualityJson=File.ReadAllText(Path.Combine(root,"LevelGenerator.Examples","RegionRelease.quality.request.json"));
var qualityRequest=JsonIO.Deserialize<GeneratorRequest>(qualityJson);
Expect(qualityRequest.qualitySelection.enabled &&
    RequestValidator.Validate(JObject.Parse(qualityJson),qualityRequest,catalog).Count==0,
    "Quality-guided request passes explicit bounded config validation");
Expect(GeneratorSeeding.SamplingConfigHash(qualityRequest)==GeneratorSeeding.SamplingConfigHash(guided) &&
    GeneratorSeeding.AttemptSeed(115,GeneratorSeeding.SamplingConfigHash(qualityRequest),0)==
    GeneratorSeeding.AttemptSeed(115,GeneratorSeeding.SamplingConfigHash(guided),0),
    "Quality mode does not change initial constructor layout sampling seed");
var noQuality=JsonIO.Deserialize<GeneratorRequest>(guidedJson);
Expect(!noQuality.qualitySelection.enabled,"Legacy requests retain first-certified behavior by default");
var badQuality=JObject.Parse(qualityJson);
badQuality["qualitySelection"]!["maxCertifiedCandidates"]=21;
Expect(RequestValidator.Validate(badQuality,JsonIO.Deserialize<GeneratorRequest>(badQuality.ToString()),catalog)
    .Any(x=>x.Contains("qualitySelection bounds")),"Quality selection rejects unbounded certified count");
badQuality=JObject.Parse(qualityJson);
badQuality["qualitySelection"]!["enabled"]="yes";
Expect(RequestValidator.Validate(badQuality,qualityRequest,catalog)
    .Any(x=>x.Contains("qualitySelection.enabled")),"Quality selection rejects non-boolean enabled field");
badQuality=JObject.Parse(qualityJson);
badQuality["qualitySelection"]!["maxAdditionalAttemptsAfterFirst"]=-1;
Expect(RequestValidator.Validate(badQuality,JsonIO.Deserialize<GeneratorRequest>(badQuality.ToString()),catalog)
    .Any(x=>x.Contains("qualitySelection bounds")),"Quality mode requires a finite additional-attempt bound");
var noObserved=new AnalysisReport{pattern="RegionRelease",bottleneckEvidence="Unknown",progressionEvidence="ObservedWitnessTimeline",drags=3};
var observed1=new AnalysisReport{pattern="RegionRelease",bottleneckEvidence="ObservedLocalRelief",progressionEvidence="ObservedWitnessTimeline",drags=100,
    localRelief=new(){new LocalReliefObservation{usedInContinuation=new(){"2:cat:1"}}}};
var observed2=new AnalysisReport{pattern="RegionRelease",bottleneckEvidence="ObservedLocalRelief",progressionEvidence="ObservedWitnessTimeline",drags=2,
    localRelief=new(){new LocalReliefObservation{usedInContinuation=new(){"2:cat:1","1:box:0"}}}};
var unknownChoice=QualityCandidateSelector.Describe(noObserved,"RegionRelease","unknown",0,0,1);
var oneChoice=QualityCandidateSelector.Describe(observed1,"RegionRelease","one",1,0,2);
var twoChoice=QualityCandidateSelector.Describe(observed2,"RegionRelease","two",2,1,3);
Expect(QualityCandidateSelector.Compare(oneChoice,unknownChoice)>0 &&
    QualityCandidateSelector.Compare(twoChoice,oneChoice)>0,
    "Real post-break continuation evidence wins over unknown, capped at 2 observed sources");
observed1.drags=1;
Expect(QualityCandidateSelector.Compare(oneChoice,unknownChoice)>0 &&
    QualityCandidateSelector.Compare(oneChoice,oneChoice)==0,
    "Move count is not a quality or difficulty score; exact evidence ties retain earlier candidate");
var queueObservation=new AnalysisReport{pattern="QueueRevisitParking",bottleneckEvidence="ObservedLocalRelief",
    progressionEvidence="ObservedWitnessTimeline",queueRevisits=1};
var queueChoice=QualityCandidateSelector.Describe(queueObservation,"QueueRevisitParking","queue",0,0,1);
Expect(queueChoice.intendedPatternObserved,"Queue revisit recognized even when analysis summary prioritized local relief");
var selectionReport=new GenerationReport{qualitySelection=new QualitySelectionReport{enabled=true,
    selectedInputHash="testhash",certifiedCandidatesConsidered=2,stopReason="CertifiedCandidateLimit",
    candidates=new(){unknownChoice,oneChoice}}};
var roundTrip=JsonIO.Deserialize<GenerationReport>(JsonIO.Serialize(selectionReport));
Expect(roundTrip.qualitySelection.candidates.Count==2 && roundTrip.qualitySelection.selectedInputHash=="testhash",
    "Quality selection provenance survives report JSON roundtrip");

Console.WriteLine($"All {pass} assertions passed. This suite does not certify solver/replay without running .NET.");
return 0;
