using System.Globalization;
using System.Text;

namespace CatDom.LevelGenerator;

/// <summary>Offline benchmark analysis; reads saved reports and never re-runs solver.</summary>
public sealed class SeedDiagnosticRow
{
    public int seed;
    public string status="";
    public int attempts;
    public int repairsProposed;
    public int repairsCertified;
    public double elapsedMs;
    public int solverCalls;
    public int duplicateCandidatesSkipped;
    public bool hasPhaseTiming;
}
public sealed class BatchDiagnosticReport
{
    public string schemaVersion="levelgen-benchmark-diagnostics/0.1";
    public int requestedSeeds;
    public int completedSeeds;
    public int reportsRead;
    public int certifiedCount;
    public int budgetExhaustedCount;
    public int totalAttempts;
    public int totalRepairsProposed;
    public int totalRepairsCertified;
    public int totalSolverCalls;
    public int duplicateCandidatesSkipped;
    public double batchElapsedWallMs;
    public double? constructorMs;
    public double? staticValidationMs;
    public double? solverReplayMs;
    public double? analysisMs;
    public double? repairProposalMs;
    public int reportsWithPhaseTiming;
    public Dictionary<string,int> solverOutcomes=new(StringComparer.Ordinal);
    public Dictionary<string,int> solverFailureCategories=new(StringComparer.Ordinal);
    public Dictionary<string,int> attemptOutcomes=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOutcomes=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOperatorsProposed=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOperatorsCertified=new(StringComparer.Ordinal);
    public List<SeedDiagnosticRow> seeds=new();
    public List<string> caveats=new();
}
public static class BenchmarkDiagnostics
{
    private static void Add(Dictionary<string,int> target,string key,int count)
    {
        if(count<=0)return;
        target[key]=target.GetValueOrDefault(key)+count;
    }
    private static void Merge(Dictionary<string,int> target,Dictionary<string,int> source)
    {
        foreach(var (key,value) in source)Add(target,key,value);
    }
    public static BatchDiagnosticReport Read(string batchDirectory)
    {
        var directory=Path.GetFullPath(batchDirectory);
        var summaryPath=Path.Combine(directory,"batch-summary.json");
        if(!File.Exists(summaryPath))throw new FileNotFoundException("Missing batch-summary.json",summaryPath);
        var summary=JsonIO.Deserialize<BatchSummary>(File.ReadAllText(summaryPath));
        var output=new BatchDiagnosticReport{
            requestedSeeds=summary.count,completedSeeds=summary.rows.Count,
            batchElapsedWallMs=summary.elapsedWallMs,certifiedCount=summary.certifiedCount,
            budgetExhaustedCount=summary.rows.Count(r=>r.status=="BudgetExhausted")
        };
        foreach(var batchRow in summary.rows.OrderBy(r=>r.seed))
        {
            // Use directory + numeric seed, NEVER historical absolute reportFile from another machine.
            var reportPath=Path.Combine(directory,$"seed-{batchRow.seed.ToString(CultureInfo.InvariantCulture)}","report.json");
            if(!File.Exists(reportPath))
            {
                output.caveats.Add($"Seed {batchRow.seed}: report.json missing; row is present in batch summary only.");
                continue;
            }
            var reportJson=File.ReadAllText(reportPath);
            var report=JsonIO.Deserialize<GenerationReport>(reportJson);
            output.reportsRead++;
            output.totalAttempts+=report.attempts;
            output.totalRepairsProposed+=report.repairsProposed;
            output.totalRepairsCertified+=report.repairsCertified;
            var metrics=report.diagnostics;
            bool hasTelemetry=metrics!=null && Newtonsoft.Json.Linq.JObject.Parse(reportJson)["diagnostics"] is Newtonsoft.Json.Linq.JObject;
            if(hasTelemetry)
            {
                output.reportsWithPhaseTiming++;
                output.constructorMs=(output.constructorMs??0)+metrics!.constructorMs;
                output.staticValidationMs=(output.staticValidationMs??0)+metrics.staticValidationMs;
                output.solverReplayMs=(output.solverReplayMs??0)+metrics.solverReplayMs;
                output.analysisMs=(output.analysisMs??0)+metrics.analysisMs;
                output.repairProposalMs=(output.repairProposalMs??0)+metrics.repairProposalMs;
                output.totalSolverCalls+=metrics.solverCalls;
                output.duplicateCandidatesSkipped+=metrics.duplicateCandidatesSkipped;
                Merge(output.solverOutcomes,metrics.solverStatuses);
                Merge(output.solverFailureCategories,metrics.solverFailureCategories);
                Merge(output.attemptOutcomes,metrics.attemptOutcomes);
                Merge(output.repairOutcomes,metrics.repairOutcomes);
                Merge(output.repairOperatorsProposed,metrics.repairOperatorsProposed);
                Merge(output.repairOperatorsCertified,metrics.repairOperatorsCertified);
            }
            else
            {
                // Milestone 3.1 reports do not retain repair wall time nor all initial
                // solver outcomes after a repaired attempt is promoted to success.
                foreach(var attempt in report.trace)
                {
                    Add(output.attemptOutcomes,attempt.state,1);
                    if(attempt.state=="RepairedCertified")continue; // original failure overwritten by old code
                    if(attempt.solverStatus!="NotRun")
                    {
                        Add(output.solverOutcomes,attempt.solverStatus,1);
                        Add(output.solverFailureCategories,SolverFailureClassifier.Classify(attempt.solverStatus,attempt.detail),1);
                    }
                }
                foreach(var repair in report.repairTrace)
                {
                    Add(output.repairOutcomes,repair.state,1);
                    if(repair.childHash!=null)Add(output.repairOperatorsProposed,repair.operatorName,1);
                    if(repair.state=="Certified")Add(output.repairOperatorsCertified,repair.operatorName,1);
                    if(repair.solverStatus!="NotRun")
                    {
                        Add(output.solverOutcomes,repair.solverStatus,1);
                        Add(output.solverFailureCategories,SolverFailureClassifier.Classify(repair.solverStatus,repair.reason),1);
                    }
                }
            }
            output.seeds.Add(new SeedDiagnosticRow{seed=report.seed,status=report.status,
                attempts=report.attempts,repairsProposed=report.repairsProposed,
                repairsCertified=report.repairsCertified,elapsedMs=batchRow.elapsedMs,
                solverCalls=hasTelemetry?metrics!.solverCalls:0,
                duplicateCandidatesSkipped=hasTelemetry?metrics!.duplicateCandidatesSkipped:0,
                hasPhaseTiming=hasTelemetry});
        }
        if(output.reportsWithPhaseTiming!=output.reportsRead)
            output.caveats.Add("Legacy reports lack phase timing and repair solver durations. Historic solver categories are reconstructed approximately; re-run with milestone 4.1b for exact measurements.");
        output.caveats.Add("NoNextCatReachable is a solver search result, not a sound pre-solver proof of unsolvability. Timeouts are inconclusive.");
        output.caveats.Add("Stage milliseconds are summed over proposals and seeds; with parallel workers they do not add up to batch wall time.");
        output.caveats.Add("Local pattern witness is not proof of a globally necessary bottleneck, and no quality/difficulty score is inferred.");
        return output;
    }
    public static string ToMarkdown(BatchDiagnosticReport report)
    {
        var sb=new StringBuilder();
        sb.AppendLine("# CatDom — Benchmark Diagnostics 4.1b");
        sb.AppendLine();
        sb.AppendLine($"- Batch: **{report.completedSeeds}/{report.requestedSeeds}** completed; **{report.certifiedCount}** certified; **{report.budgetExhaustedCount}** budget exhausted.");
        sb.AppendLine($"- Wall time: **{(report.batchElapsedWallMs/1000).ToString("0.0",CultureInfo.InvariantCulture)} s**; candidate attempts: **{report.totalAttempts}**; repair proposals: **{report.totalRepairsProposed}**.");
        sb.AppendLine($"- Repair certification count: **{report.totalRepairsCertified}** (these are not level quality ratings).");
        sb.AppendLine($"- Accurate phase timing available for **{report.reportsWithPhaseTiming}/{report.reportsRead}** seed reports.");
        sb.AppendLine();
        void Section(string title,Dictionary<string,int> values)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            sb.AppendLine("| Outcome | Count |");sb.AppendLine("|---|---:|");
            foreach(var kv in values.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key))
                sb.Append("| ").Append(kv.Key.Replace("|","\\|")).Append(" | ").Append(kv.Value).AppendLine(" |");
            sb.AppendLine();
        }
        Section("Solver outcomes (observed)",report.solverOutcomes);
        Section("Failure categories (observed)",report.solverFailureCategories);
        Section("Repair proposals per operator",report.repairOperatorsProposed);
        Section("Certified repair per operator",report.repairOperatorsCertified);
        if(report.reportsWithPhaseTiming>0)
        {
            sb.AppendLine("## Phase durations, sum across available seed reports");sb.AppendLine();
            sb.AppendLine("| Phase | Milliseconds |");sb.AppendLine("|---|---:|");
            foreach(var (name,ms) in new[]{("Constructor",report.constructorMs),
                ("Validation",report.staticValidationMs),("Solver + replay",report.solverReplayMs),
                ("Analyzer",report.analysisMs),("Repair proposals",report.repairProposalMs)})
                sb.Append("| ").Append(name).Append(" | ").Append(ms?.ToString("0.0",CultureInfo.InvariantCulture)??"N/A").AppendLine(" |");
            sb.AppendLine();
        }
        sb.AppendLine("## Caveats");sb.AppendLine();
        foreach(var caveat in report.caveats)sb.Append("- ").AppendLine(caveat);
        return sb.ToString();
    }
}
