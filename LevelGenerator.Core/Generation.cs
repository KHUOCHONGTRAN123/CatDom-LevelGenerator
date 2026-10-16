using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace CatDom.LevelGenerator;

/// <summary>Bounded candidate generation, independent certification, witness analysis and transactional repair.</summary>
public sealed class GenerationRunner
{
    private readonly ShapeSet shapes;
    private readonly LevelValidator validator;
    private readonly CandidateConstructor constructor;
    private readonly Certifier certifier;
    private readonly WitnessAnalyzer analyzer;
    private readonly CandidateRepair repair;
    public GenerationRunner(string catalogPath)
    {
        shapes=ShapeSet.Load(catalogPath);
        validator=new LevelValidator(shapes);
        constructor=new CandidateConstructor(shapes);
        certifier=new Certifier(catalogPath);
        analyzer=new WitnessAnalyzer(shapes);
        repair=new CandidateRepair(shapes);
    }
    public ShapeSet Catalog=>shapes;

    public GenerationReport Run(string requestJson,string outputDirectory,CancellationToken token=default)
    {
        Directory.CreateDirectory(outputDirectory);
        var root=JObject.Parse(requestJson);
        GeneratorRequest request;
        try {request=JsonIO.Deserialize<GeneratorRequest>(requestJson);}
        catch(Exception ex){throw new ArgumentException("InvalidConfig: cannot parse request: "+ex.Message,ex);}
        var errors=RequestValidator.Validate(root,request,shapes);
        if(errors.Count>0)throw new ArgumentException("InvalidConfig: "+string.Join("; ",errors));
        var normalized=JsonIO.Serialize(request);
        var samplingConfigHash=GeneratorSeeding.SamplingConfigHash(request);
        var report=new GenerationReport{
            seed=request.seed,pattern=request.intent.pattern,catalogHash=shapes.Sha256,
            resolvedConfigHash=HashUtil.Text(normalized),samplingConfigHash=samplingConfigHash,status="BudgetExhausted",
            warning="Only independent solver/replay certifies a level. Local witness evidence does not prove global bottleneck necessity."
        };
        // Avoid stale success artifacts being mistaken for output of a subsequent exhausted run.
        foreach(var f in new[]{"certified-draft.json","analysis.json","unverified-draft.json"})
        {
            var path=Path.Combine(outputDirectory,f);
            if(File.Exists(path))File.Delete(path);
        }
        var stop=Stopwatch.StartNew();
        bool firstDraft=false;
        var metrics=report.diagnostics;
        var qualityPolicy=request.qualitySelection;
        var qualityReport=report.qualitySelection;
        qualityReport.enabled=qualityPolicy.enabled;
        qualityReport.maxCertifiedCandidates=qualityPolicy.enabled?qualityPolicy.maxCertifiedCandidates:0;
        qualityReport.maxAdditionalAttemptsAfterFirst=qualityPolicy.enabled?qualityPolicy.maxAdditionalAttemptsAfterFirst:0;
        int firstCertifiedAttempt=-1;
        (string json,string hash,ValidationResult validation,AnalysisReport evidence,string solverVersion,bool repaired)? fallback=null;
        (string json,string hash,ValidationResult validation,AnalysisReport evidence,string solverVersion,bool repaired)? selected=null;
        QualityCandidateSummary? bestEvidence=null;
        bool WithinBudget()=>!token.IsCancellationRequested && stop.ElapsedMilliseconds<request.budgets.totalRunMs;
        void Publish(string json,string hash,ValidationResult validation,AnalysisReport evidence,string solverVersion,bool wasRepaired)
        {
            report.inputHash=hash;
            report.footprint=validation.footprint;
            report.coverageStatus=validation.coverageStatus;
            report.solverVersion=solverVersion;
            report.independentlyReplayed=true;
            report.bottleneckEvidence=evidence.bottleneckEvidence;
            report.progressionEvidence=evidence.progressionEvidence;
            report.difficultyRating="Unrated";
            report.qualityRating="Unrated";
            report.status="ReviewRequired";
            report.draftFile=Path.GetFullPath(Path.Combine(outputDirectory,"certified-draft.json"));
            File.WriteAllText(report.draftFile,json);
            report.analysisFile=Path.GetFullPath(Path.Combine(outputDirectory,"analysis.json"));
            var analysisText=JsonIO.Serialize(evidence);
            File.WriteAllText(report.analysisFile,analysisText);
            report.analysisHash=HashUtil.Text(analysisText);
            if(wasRepaired)report.repairsCertified++;
        }
        bool Evaluate(LevelData level,AttemptRecord record,RepairRecord? repairRecord,Dictionary<string,string> seenThisAttempt)
        {
            var phase=Stopwatch.StartNew();
            // This LevelData is constructed in-process; Check(level) avoids an unnecessary
            // JSON parse/deserialization. The JSON boundary is still independently checked
            // by the solver/replay, and approve validates the exported JSON again.
            var validation=validator.Check(level,request.targetPolicy.minHoleFootprint,
                request.targetPolicy.maxHoleFootprint,request.targetPolicy.targetHoleFootprint);
            record.validationTimeMs=phase.Elapsed.TotalMilliseconds;
            metrics.staticValidationMs+=record.validationTimeMs;
            record.footprint=validation.footprint;
            if(!validation.valid)
            {
                metrics.invalidCandidates++;
                record.state="RuleFailure";record.detail=string.Join("; ",validation.errors);return false;
            }
            metrics.validCandidates++;
            var json=JsonIO.Serialize(level);
            if(validation.coverageStatus=="WithinRangeTargetDeviation" && request.targetPolicy.deviationPolicy=="Reject")
            {record.state="TargetDeviation";record.detail="Target mismatch; rejected by policy";return false;}
            record.inputHash=HashUtil.Text(json);
            // Rechecking the exact same candidate after a conclusive solver/replay
            // result adds no information. Timeouts/cancellations are INCONCLUSIVE
            // and are deliberately NOT cached. This is not a cross-level filter.
            if(seenThisAttempt.ContainsKey(record.inputHash))
            {
                metrics.duplicateCandidatesSkipped++;
                record.state="DuplicateCandidateSkipped";
                record.detail="Exact candidate already evaluated in this attempt";
                if(repairRecord!=null)repairRecord.state=record.state;
                return false;
            }
            if(!firstDraft)
            {
                // An unverified draft is never named Levelxxxxx.json or exported by Approve.
                File.WriteAllText(Path.Combine(outputDirectory,"unverified-draft.json"),json);
                report.draftFile=Path.GetFullPath(Path.Combine(outputDirectory,"unverified-draft.json"));
                firstDraft=true;
            }
            var remaining=(int)Math.Max(0,request.budgets.totalRunMs-stop.ElapsedMilliseconds);
            if(remaining<100){record.state="BudgetExhausted";return false;}
            var certified=certifier.Check(json,Math.Min(remaining,request.budgets.perSolveMs),token);
            metrics.solverCalls++;
            metrics.solverReplayMs+=certified.elapsedMs;
            GenerationDiagnostics.Increment(metrics.solverStatuses,certified.solverStatus);
            record.solverFailureCategory=SolverFailureClassifier.Classify(certified.solverStatus,certified.explanation);
            GenerationDiagnostics.Increment(metrics.solverFailureCategories,record.solverFailureCategory);
            record.solverExpandedStates=certified.expandedStates;
            record.solverStatus=certified.solverStatus;
            record.replayStatus=certified.replayStatus;
            record.solveTimeMs=certified.elapsedMs;
            record.detail=certified.explanation;
            if(repairRecord!=null)
            {
                repairRecord.solverStatus=certified.solverStatus;
                repairRecord.replayStatus=certified.replayStatus;
                repairRecord.solverFailureCategory=record.solverFailureCategory;
                repairRecord.solverExpandedStates=certified.expandedStates;
                repairRecord.solveTimeMs=certified.elapsedMs;
            }
            if(!certified.certified)
            {
                if(certified.solverStatus=="NoNextCatReachable")
                    seenThisAttempt[record.inputHash]="NoNextCatReachable";
                record.state=certified.replayStatus=="ReplayFailed"||certified.replayStatus=="InvalidInput"?"ReplayFailed":
                    certified.replayStatus=="ToolFailure"?"ToolFailure":"Uncertified";
                if(repairRecord!=null){repairRecord.state=record.state;repairRecord.reason=certified.explanation;}
                return false;
            }
            seenThisAttempt[record.inputHash]="Solved";
            record.state="Certified";
            AnalysisReport evidence;
            var analysisClock=Stopwatch.StartNew();
            try {evidence=analyzer.Analyze(level,certified.certifiedMoves,record.inputHash,request.intent.pattern);}
            catch(Exception ex)
            {
                // Do not change the independent certification verdict when analytics fail.
                evidence=new AnalysisReport {inputHash=record.inputHash,pattern=request.intent.pattern,
                    unknowns=new(){"Analyzer failed: "+ex.GetType().Name+": "+ex.Message}};
                record.detail+="; analyzer unsupported: "+ex.Message;
            }
            finally
            {
                record.analysisTimeMs=analysisClock.Elapsed.TotalMilliseconds;
                metrics.analysisMs+=record.analysisTimeMs;
            }
            if(repairRecord!=null)repairRecord.state="Certified";
            var expectedObservation=request.intent.pattern=="RegionRelease" ? "ObservedLocalRelief" : "ObservedQueueRevisit";
            if(qualityPolicy.enabled)
            {
                // Preserve the existing Reject contract: unknown global necessity is not a pass.
                // The opt-in quality mode cannot be combined with Reject (config validation).
                var entry=QualityCandidateSelector.Describe(evidence,request.intent.pattern,
                    record.inputHash,record.index,repairRecord?.step??0,qualityReport.certifiedCandidatesConsidered+1);
                qualityReport.certifiedCandidatesConsidered++;
                firstCertifiedAttempt=firstCertifiedAttempt<0?record.index:firstCertifiedAttempt;
                if(bestEvidence==null || QualityCandidateSelector.Compare(entry,bestEvidence)>0)
                {
                    if(bestEvidence!=null)
                    {
                        bestEvidence.selected=false;
                        bestEvidence.decision="Superseded by stronger observed pattern/progression evidence";
                    }
                    entry.selected=true;
                    entry.decision="Selected: strongest observed witness evidence so far";
                    bestEvidence=entry;
                    selected=(json,record.inputHash,validation,evidence,certified.solverAlgorithmVersion,repairRecord!=null);
                    qualityReport.selectedInputHash=record.inputHash;
                }
                else entry.decision="Not selected: no improvement over existing certified candidate";
                qualityReport.candidates.Add(entry);
                // If a witness is absent, preserve the certified candidate and allow
                // the original Repair policy to try another candidate with the same intent.
                if(request.intent.unknownPolicy=="Repair" && !entry.intendedPatternObserved &&
                   qualityReport.certifiedCandidatesConsidered<qualityPolicy.maxCertifiedCandidates)
                {
                    record.state="AnalysisUnknown";
                    record.detail+="; certified fallback retained by quality selector; exploring repair";
                    if(repairRecord!=null)repairRecord.state="AnalysisUnknown";
                    return false;
                }
                // Keep the existing diagnostics event names for backwards-compatible reports.
                record.state="Certified";
                if(repairRecord!=null)repairRecord.state="Certified";
                return true; // stop this attempt; outer loop may continue to improve selection
            }
            if(request.intent.unknownPolicy=="Repair" && evidence.bottleneckEvidence!=expectedObservation)
            {
                // Prefer an observed pattern, but keep the FIRST certified result as a review fallback.
                // This is NOT a claim that the pattern is necessary in all solutions.
                fallback??=(json,record.inputHash,validation,evidence,certified.solverAlgorithmVersion,repairRecord!=null);
                record.state="AnalysisUnknown";
                record.detail+="; pattern witness not observed; trying bounded repair while preserving certified fallback";
                if(repairRecord!=null)repairRecord.state="AnalysisUnknown";
                return false;
            }
            var evidenceEstablished = evidence.bottleneckEvidence is "Supported";
            if(request.intent.unknownPolicy=="Reject" && !evidenceEstablished)
            {
                record.state="AnalysisUnknown";
                record.detail+="; global bottleneck necessity not verified";
                report.status="AnalysisUnknown";
                if(repairRecord!=null)repairRecord.state="AnalysisUnknown";
                return false;
            }
            Publish(json,record.inputHash,validation,evidence,certified.solverAlgorithmVersion,repairRecord!=null);
            return true;
        }
        for(int i=0;i<request.budgets.maxAttempts && WithinBudget();i++)
        {
            if(qualityPolicy.enabled)
            {
                if(qualityReport.certifiedCandidatesConsidered>=qualityPolicy.maxCertifiedCandidates)
                {qualityReport.stopReason="CertifiedCandidateLimit";break;}
                if(firstCertifiedAttempt>=0 && i>firstCertifiedAttempt+qualityPolicy.maxAdditionalAttemptsAfterFirst)
                {qualityReport.stopReason="AdditionalAttemptLimit";break;}
            }
            var attemptSeed=GeneratorSeeding.AttemptSeed(request.seed,samplingConfigHash,i);
            var record=new AttemptRecord{index=i,attemptSeed=attemptSeed};
            report.trace.Add(record);report.attempts++;
            var buildClock=Stopwatch.StartNew();
            var candidate=constructor.TryBuild(request,attemptSeed,out var reason);
            record.constructTimeMs=buildClock.Elapsed.TotalMilliseconds;
            metrics.constructorMs+=record.constructTimeMs;
            if(candidate==null)
            {
                record.state="ConstructionFailed";record.detail=reason;
                GenerationDiagnostics.Increment(metrics.constructionFailures,reason);
                continue;
            }
            var intentClock=Stopwatch.StartNew();
            var intentOk=repair.PreservesIntent(candidate,request);
            metrics.intentCheckMs+=intentClock.Elapsed.TotalMilliseconds;
            if(!intentOk)
            {record.state="IntentMismatch";record.detail="Constructor did not preserve intended structural preconditions";continue;}
            var seenThisAttempt=new Dictionary<string,string>(StringComparer.Ordinal);
            if(Evaluate(candidate,record,null,seenThisAttempt))
            {
                if(!qualityPolicy.enabled)break; // legacy stop-on-first behavior
                continue; // quality mode: later attempts may improve the certified winner
            }
            // Repair explores variants in sequence, each proposal validated and independently re-solved.
            // A validated but unsolved proposal can be the parent of another repair;
            // NONE inherits certification, input hash, or causal analysis from its parent.
            if(record.state is "ReplayFailed" or "ToolFailure")continue;
            var current=candidate;
            var hint=record.state=="AnalysisUnknown"?"AnalysisUnknown":
                request.operatorPolicy.failureAware?record.solverFailureCategory:record.solverStatus;
            for(int step=0;step<request.budgets.maxRepairsPerAttempt && WithinBudget() &&
                (!qualityPolicy.enabled || qualityReport.certifiedCandidatesConsidered<qualityPolicy.maxCertifiedCandidates);step++)
            {
                var parentHash=HashUtil.Text(JsonIO.Serialize(current));
                var proposalClock=Stopwatch.StartNew();
                var next=repair.Propose(current,request,attemptSeed,step,out var op,out var explanation,hint);
                var proposalMs=proposalClock.Elapsed.TotalMilliseconds;
                metrics.repairProposalMs+=proposalMs;
                var row=new RepairRecord{attempt=i,step=step+1,operatorName=op,parentHash=parentHash,
                    proposalTimeMs=proposalMs,state=next==null?"RolledBack":"Proposed",reason=explanation};
                report.repairTrace.Add(row);
                if(next==null){metrics.repairProposalsSkipped++;continue;}
                row.childHash=HashUtil.Text(JsonIO.Serialize(next));
                report.repairsProposed++;
                GenerationDiagnostics.Increment(metrics.repairOperatorsProposed,op);
                var evaluation=new AttemptRecord{index=i,attemptSeed=attemptSeed};
                if(Evaluate(next,evaluation,row,seenThisAttempt))
                {
                    record.state="RepairedCertified";
                    GenerationDiagnostics.Increment(metrics.repairOperatorsCertified,op);
                    record.inputHash=evaluation.inputHash;
                    record.footprint=evaluation.footprint;
                    record.solverStatus=evaluation.solverStatus;
                    record.replayStatus=evaluation.replayStatus;
                    record.detail=$"Repair {step+1}/{request.budgets.maxRepairsPerAttempt} ({op}): "+evaluation.detail;
                    break;
                }
                // Static transaction committed to a working candidate; failed certification is not a proof of impossibility.
                if(evaluation.state is "Uncertified" or "AnalysisUnknown")
                {
                    current=next;
                    hint=evaluation.state=="AnalysisUnknown"?"AnalysisUnknown":
                        request.operatorPolicy.failureAware?evaluation.solverFailureCategory:evaluation.solverStatus;
                }
                if(evaluation.state is "ReplayFailed" or "ToolFailure")break;
            }
            if(report.status=="ReviewRequired")break;
        }
        if(qualityPolicy.enabled)
        {
            if(string.IsNullOrEmpty(qualityReport.stopReason) || qualityReport.stopReason=="Disabled")
                qualityReport.stopReason=token.IsCancellationRequested?"Cancelled":
                    !WithinBudget()?"TotalRunBudget": "AttemptBudget";
            if(selected.HasValue && !token.IsCancellationRequested)
            {
                var winner=selected.Value;
                // Every eligible candidate was certified/replayed separately. Export only
                // the selected immutable JSON/hash; never inherit another plan's evidence.
                Publish(winner.json,winner.hash,winner.validation,winner.evidence,winner.solverVersion,winner.repaired);
                report.warning+=" Selection is based only on evidence observed in certified plans, not human quality ratings or proof of bottleneck necessity.";
            }
        }
        if(!qualityPolicy.enabled && fallback.HasValue && report.status!="ReviewRequired" && !token.IsCancellationRequested)
        {
            var saved=fallback.Value;
            Publish(saved.json,saved.hash,saved.validation,saved.evidence,saved.solverVersion,saved.repaired);
            report.warning+=" Preferred pattern observation was not found within budget; returning independently certified fallback for HUMAN review only.";
        }
        if(report.status=="BudgetExhausted" && report.trace.Count>0 && report.trace.All(a=>a.state=="ConstructionFailed"))
            report.warning+=" All attempts failed construction.";
        if(token.IsCancellationRequested)report.status="Cancelled";
        metrics.elapsedMs=stop.Elapsed.TotalMilliseconds;
        foreach(var attempt in report.trace)
            GenerationDiagnostics.Increment(metrics.attemptOutcomes,attempt.state);
        foreach(var row in report.repairTrace)
            GenerationDiagnostics.Increment(metrics.repairOutcomes,row.state);
        report.reviewFile=Path.GetFullPath(Path.Combine(outputDirectory,"report.json"));
        File.WriteAllText(report.reviewFile,JsonIO.Serialize(report));
        return report;
    }
}

public static class DesignerReview
{
    /// <summary>Approve exactly one certified, unchanged draft. Never accept a solver timeout.</summary>
    public static string Approve(string reportPath,string destinationPath,string rationale,string catalogPath)
    {
        if(string.IsNullOrWhiteSpace(rationale))throw new ArgumentException("Designer rationale is required");
        var report=JsonIO.Deserialize<GenerationReport>(File.ReadAllText(reportPath));
        if(report.status!="ReviewRequired" || !report.independentlyReplayed || string.IsNullOrEmpty(report.inputHash) || string.IsNullOrEmpty(report.draftFile))
            throw new InvalidOperationException("Only certified ReviewRequired drafts may be approved");
        if(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(destinationPath),@"^Level\d{5}\.json$"))
            throw new ArgumentException("Export filename must be Levelxxxxx.json");
        var draftJson=File.ReadAllText(report.draftFile);
        if(HashUtil.Text(draftJson)!=report.inputHash)throw new InvalidOperationException("Draft has changed after certification");
        var catalog=ShapeSet.Load(catalogPath);
        if(catalog.Sha256!=report.catalogHash)throw new InvalidOperationException("Catalog has changed after certification");
        var levelResult=new LevelValidator(catalog).CheckJson(draftJson,25,32);
        if(!levelResult.valid)throw new InvalidOperationException("Export static validation failed: "+string.Join("; ",levelResult.errors));
        if(!string.IsNullOrEmpty(report.analysisFile))
        {
            if(!File.Exists(report.analysisFile) || HashUtil.Text(File.ReadAllText(report.analysisFile))!=report.analysisHash)
                throw new InvalidOperationException("Analysis report has changed since certification");
        }
        if(File.Exists(destinationPath))throw new IOException("Refusing to overwrite existing level: "+destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        File.WriteAllText(destinationPath,draftJson);
        var review=new {decision="Approved",rationale,reportHash=HashUtil.Text(File.ReadAllText(reportPath)),
            inputHash=report.inputHash,analysisHash=report.analysisHash,exportPath=Path.GetFullPath(destinationPath),utc=DateTimeOffset.UtcNow};
        File.WriteAllText(destinationPath+".review.json",JsonIO.Serialize(review));
        return destinationPath;
    }
}
