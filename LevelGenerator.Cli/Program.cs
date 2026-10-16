using CatDom.LevelGenerator;

static int Usage()
{
    Console.Error.WriteLine("CatDom generator v0.1 milestone 4.3 quality-guided candidate selection\n"+
      "  dotnet run --project LevelGenerator.Cli -- catalog <HoleShapes.json>\n"+
      "  dotnet run --project LevelGenerator.Cli -- validate <Level.json> <HoleShapes.json>\n"+
      "  dotnet run --project LevelGenerator.Cli -- generate <request.json> <outDir> <HoleShapes.json>\n"+
      "  dotnet run --project LevelGenerator.Cli -- analyze <Level.json> <analysis.json> <HoleShapes.json> <RegionRelease|QueueRevisitParking> [solveMs]\n"+
      "  dotnet run --project LevelGenerator.Cli -- approve <report.json> <Levelxxxxx.json> <HoleShapes.json> <rationale...>\n"+
      "  dotnet run --project LevelGenerator.Cli -- preview <Level.json> <out.svg> <HoleShapes.json> [certified-report.json]\n"+
      "  dotnet run --project LevelGenerator.Cli -- batch <request.json> <outDir> <HoleShapes.json> <count:1..20> [--workers <1..count>]\n"+
      "  dotnet run --project LevelGenerator.Cli -- diagnose <batchDirectory> [outputDirectory]\n"+
      "  dotnet run --project LevelGenerator.Cli -- verify <Level.json> <verification.json> <HoleShapes.json> <RegionRelease|QueueRevisitParking> [solveMs] [searchMs] [maxPlans]\n"+
      "  dotnet run --project LevelGenerator.Cli -- verify-batch <batchDir> <outDir> <HoleShapes.json> [workers] [solveMs] [searchMs] [maxPlans]");
    return 2;
}
try
{
    if(args.Length>=5 && args.Length<=8 && args[0]=="verify")
    {
        int Optional(int idx,int fallback)=>args.Length>idx?int.Parse(args[idx]):fallback;
        var verifier=new StrongBottleneckVerifier(args[3]);
        using var cancel=new CancellationTokenSource();
        Console.CancelKeyPress+=(sender,e)=>{e.Cancel=true;cancel.Cancel();};
        var result=verifier.Verify(File.ReadAllText(args[1]),args[4],
            Optional(5,15000),Optional(6,5000),Optional(7,250),cancel.Token);
        var output=Path.GetFullPath(args[2]);
        if(File.Exists(output))throw new IOException("Refusing to overwrite prior verification: "+output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output,JsonIO.Serialize(result));
        Console.WriteLine(JsonIO.Serialize(new {result.verdict,result.stopReason,
            result.searchedPlans,result.validAlternativeWins,result.checks,result.notes,output}));
        return result.verdict is "InvalidLevel" or "Uncertified"?1:0;
    }
    if(args.Length>=4 && args.Length<=8 && args[0]=="verify-batch")
    {
        int Optional(int idx,int fallback)=>args.Length>idx?int.Parse(args[idx]):fallback;
        using var cancel=new CancellationTokenSource();
        Console.CancelKeyPress+=(sender,e)=>{e.Cancel=true;cancel.Cancel();};
        var result=StrongVerificationBatchRunner.Run(args[1],args[2],args[3],
            Optional(4,4),Optional(5,15000),Optional(6,5000),Optional(7,250),cancel.Token);
        Console.WriteLine(JsonIO.Serialize(new{result.totalSeeds,result.eligible,result.verified,
            result.bypassFound,result.unknown,result.skipped,result.elapsedWallMs,
            summary=Path.GetFullPath(Path.Combine(args[2],"verification-summary.json"))}));
        return cancel.IsCancellationRequested?130:result.rows.Any(r=>r.status=="Error")?1:0;
    }
    if(args.Length is 2 or 3 && args[0]=="diagnose")
    {
        var input=Path.GetFullPath(args[1]);
        var output=Path.GetFullPath(args.Length==3?args[2]:args[1]);
        // Existing batch artifacts are read-only; diagnostics get their own names.
        var diagnostics=BenchmarkDiagnostics.Read(input);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"diagnostics.json"),JsonIO.Serialize(diagnostics));
        File.WriteAllText(Path.Combine(output,"diagnostics.md"),BenchmarkDiagnostics.ToMarkdown(diagnostics));
        Console.WriteLine(JsonIO.Serialize(new{diagnostics.completedSeeds,diagnostics.certifiedCount,
            diagnostics.totalAttempts,diagnostics.totalRepairsProposed,diagnostics.reportsWithPhaseTiming,
            json=Path.Combine(output,"diagnostics.json"),markdown=Path.Combine(output,"diagnostics.md")}));
        return 0;
    }
    if(args.Length==2 && args[0]=="catalog")
    {
        Console.WriteLine(ShapeSet.Load(args[1]).Sha256);return 0;
    }
    if(args.Length==3 && args[0]=="validate")
    {
        var result=new LevelValidator(ShapeSet.Load(args[2])).CheckJson(File.ReadAllText(args[1]));
        Console.WriteLine(JsonIO.Serialize(result));
        return result.valid ? 0 : 1;
    }
    if(args.Length==4 && args[0]=="generate")
    {
        var runner=new GenerationRunner(args[3]);
        var result=runner.Run(File.ReadAllText(args[1]),args[2]);
        Console.WriteLine(JsonIO.Serialize(new {
            result.status,result.attempts,result.footprint,result.draftFile,
            result.reviewFile,result.analysisFile,result.repairsProposed,
            result.repairsCertified,result.bottleneckEvidence,
            qualitySelection=result.qualitySelection.enabled?new {
                result.qualitySelection.certifiedCandidatesConsidered,
                result.qualitySelection.selectedInputHash,
                result.qualitySelection.stopReason
            }:null,result.warning}));
        return result.status=="ReviewRequired"?0:1;
    }
    if(args.Length is 5 or 6 && args[0]=="analyze")
    {
        if(args[4] is not ("RegionRelease" or "QueueRevisitParking"))
            throw new ArgumentException("Pattern must be RegionRelease or QueueRevisitParking");
        var ms=args.Length==6?int.Parse(args[5]):30000;
        if(ms is <100 or >600000)throw new ArgumentOutOfRangeException(nameof(ms),"solveMs must be 100..600000");
        var shapes=ShapeSet.Load(args[3]);
        var json=File.ReadAllText(args[1]);
        var valid=new LevelValidator(shapes).CheckJson(json);
        if(!valid.valid)throw new InvalidDataException("Validation failed: "+string.Join("; ",valid.errors));
        var certificate=new Certifier(args[3]).Check(json,ms,CancellationToken.None);
        if(!certificate.certified)
        {
            Console.Error.WriteLine($"Not certified: solver={certificate.solverStatus}; replay={certificate.replayStatus}; {certificate.explanation}");
            return 1;
        }
        var input=JsonIO.Deserialize<LevelData>(json);
        var analysis=new WitnessAnalyzer(shapes).Analyze(input,certificate.certifiedMoves,HashUtil.Text(json),args[4]);
        var output=Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output,JsonIO.Serialize(analysis));
        Console.WriteLine(JsonIO.Serialize(new {analysis.pattern,analysis.bottleneckEvidence,analysis.progressionEvidence,
            analysis.clearEvents,analysis.queueRevisits,analysis.drags,analysis.unknowns,analysisFile=output}));
        return 0;
    }
    if(args.Length is 4 or 5 && args[0]=="preview")
    {
        var json=File.ReadAllText(args[1]);
        var catalog=ShapeSet.Load(args[3]);
        var validation=new LevelValidator(catalog).CheckJson(json);
        if(!validation.valid)throw new InvalidDataException("Invalid level: "+string.Join("; ",validation.errors));
        bool certified=false;
        if(args.Length==5)
        {
            var report=JsonIO.Deserialize<GenerationReport>(File.ReadAllText(args[4]));
            // A report is NOT sufficient unless the unchanged JSON and catalog match.
            certified=report.status=="ReviewRequired" && report.independentlyReplayed &&
                report.inputHash==HashUtil.Text(json) && report.catalogHash==catalog.Sha256;
            if(!certified)throw new InvalidDataException("Provided report does not certify the unchanged level and catalog");
        }
        var svg=LevelPreview.Render(JsonIO.Deserialize<LevelData>(json),catalog,certified,
            "CatDom board preview");
        var path=Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,svg);
        Console.WriteLine($"Preview written: {path} (certified={certified})");
        return 0;
    }
    if(args.Length is 5 or 6 or 7 && args[0]=="batch")
    {
        if(!int.TryParse(args[4],out var count) || count<1 || count>20)
            throw new ArgumentOutOfRangeException("count","Batch count must be an integer from 1..20");
        // Backwards compatible: a bare numeric sixth argument is also accepted.
        if(args.Length==7 && args[5]!="--workers")throw new ArgumentException("Expected --workers <number>");
        if(args.Length==6 && args[5]=="--workers")throw new ArgumentException("Missing worker count after --workers");
        var workerArg=args.Length==7?args[6]:args.Length==6?args[5]:null;
        if(workerArg!=null && (!int.TryParse(workerArg,out var parsedWorkers) || parsedWorkers<1 || parsedWorkers>count))
            throw new ArgumentOutOfRangeException("workers","Workers must be an integer from 1 to count");
        int workers=workerArg==null?0:int.Parse(workerArg);
        using var batchCancellation=new CancellationTokenSource();
        Console.CancelKeyPress+=(sender,e)=>{e.Cancel=true;batchCancellation.Cancel();};
        var result=new BatchRunner(args[3]).Run(File.ReadAllText(args[1]),args[2],count,batchCancellation.Token,workers,
            (row,completed,total)=>Console.Error.WriteLine($"[{completed}/{total}] seed={row.seed} status={row.status} elapsed={row.elapsedMs/1000:0.0}s certified={row.independentlyReplayed}"));
        Console.WriteLine(JsonIO.Serialize(new {
            result.count, completed=result.rows.Count,result.maxWorkers,result.elapsedWallMs,result.certifiedCount,result.witnessedCount,
            result.unknownEvidenceCount,result.errorCount,result.exportedCount,
            levelsFolder=Path.GetFullPath(Path.Combine(args[2],"levels")),
            summaryFile=Path.GetFullPath(Path.Combine(args[2],"batch-summary.json")),
            galleryFile=Path.GetFullPath(Path.Combine(args[2],"index.html"))
        }));
        return batchCancellation.IsCancellationRequested ? 130 : result.errorCount==0 ? 0 : 1;
    }
    if(args.Length>=5 && args[0]=="approve")
    {
        var path=DesignerReview.Approve(args[1],args[2],string.Join(' ',args.Skip(4)),args[3]);
        Console.WriteLine("Approved: "+path);return 0;
    }
    return Usage();
}
catch(Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);
    return 2;
}
