using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CatDom.LevelGenerator;

/// <summary>
/// Reproducible parallel experiment across consecutive request seeds.
/// Each seed gets its own directory so draft, analysis and report can be audited.
/// A solved+V42-replayed level is counted as certified; witness observations are
/// reported separately and NEVER reinterpreted as difficulty or quality scores.
/// </summary>
public sealed class BatchRow
{
    public int seed;
    public string status = "";
    public string pattern = "";
    public int attempts;
    public double elapsedMs;
    public int footprint;
    public int repairsProposed;
    public int repairsCertified;
    public bool independentlyReplayed;
    public string bottleneckEvidence = "Unknown";
    public string progressionEvidence = "Unknown";
    public string difficultyRating = "Unrated";
    public string qualityRating = "Unrated";
    public string reportFile = "";
    public string? draftFile;
    public string? analysisFile;
    public string? previewFile;
    // Convenience copy for gameplay testing; NOT designer approval.
    public string? exportedLevelFile;
    public string? error;
    public int qualityCertifiedCandidates;
    public string qualitySelectionStopReason = "Disabled";
    public string selectedInputHash = "";
    public int solverCalls;
    public int duplicateCandidatesSkipped;
    public double constructorMs;
    public double staticValidationMs;
    public double solverReplayMs;
    public double analysisMs;
    public double repairProposalMs;
}
public sealed class BatchSummary
{
    public string schemaVersion = "levelgen-batch/0.1";
    public string requestTemplateHash = "";
    public string catalogHash = "";
    public int startSeed;
    public int count;
    public int maxWorkers = 1;
    public double elapsedWallMs;
    public int certifiedCount;
    public int exportedCount;
    public int witnessedCount;
    public int unknownEvidenceCount;
    public int errorCount;
    public Dictionary<string,int> statuses = new(StringComparer.Ordinal);
    public Dictionary<string,int> evidenceCounts = new(StringComparer.Ordinal);
    public List<BatchRow> rows = new();
}
public sealed class BatchRunner
{
    private readonly string catalogPath;
    private readonly ShapeSet catalog;
    // Test seam: lets concurrency tests simulate slow seeds without invoking the real solver.
    // Production always uses one fresh GenerationRunner (and Certifier) per seed.
    private readonly Func<string,string,CancellationToken,GenerationReport> execute;
    public BatchRunner(string shapeCatalogPath) : this(shapeCatalogPath,null) { }
    internal BatchRunner(string shapeCatalogPath,Func<string,string,CancellationToken,GenerationReport>? executor)
    {
        catalogPath = shapeCatalogPath;
        catalog = ShapeSet.Load(shapeCatalogPath);
        execute = executor ?? ((json,directory,token) => new GenerationRunner(catalogPath).Run(json,directory,token));
    }

    /// <summary>
    /// Runs independent seeds concurrently. A worker owns only its seed directory and
    /// GenerationRunner instance; the coordinator owns the aggregate summary files.
    /// Defaults to at most four workers to avoid aggressive solver-memory pressure;
    /// caller can raise the limit explicitly up to the requested seed count.
    /// A cancelled batch retains results from workers that finished.
    /// </summary>
    public BatchSummary Run(string requestJson,string outputDirectory,int count,CancellationToken cancellationToken=default,
        int maxWorkers=0,Action<BatchRow,int,int>? onCompleted=null)
    {
        if(count is <1 or >20) throw new ArgumentOutOfRangeException(nameof(count),"Batch count must be 1..20 to prevent accidental runaway runs");
        if(maxWorkers<0 || maxWorkers>20)throw new ArgumentOutOfRangeException(nameof(maxWorkers),"Workers must be 1..20, or 0 for automatic selection");
        var workers=maxWorkers==0?Math.Min(count,Math.Min(4,Math.Max(1,Environment.ProcessorCount))):maxWorkers;
        if(workers>count)throw new ArgumentOutOfRangeException(nameof(maxWorkers),"Workers cannot exceed requested seed count");
        var obj=JObject.Parse(requestJson);
        var request=JsonIO.Deserialize<GeneratorRequest>(requestJson);
        var errors=RequestValidator.Validate(obj,request,catalog);
        if(errors.Count>0) throw new ArgumentException("InvalidConfig: "+string.Join("; ",errors));
        if((long)request.seed+count-1>int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count),"Seed sequence exceeds Int32 range");
        var output=Path.GetFullPath(outputDirectory);
        if(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Refusing to overwrite batch data: choose an empty output directory");
        Directory.CreateDirectory(output);
        // Only certified, unchanged game-format Levelxxxxx.json files live here.
        var playtestDirectory=Path.Combine(output,"levels");
        Directory.CreateDirectory(playtestDirectory);
        var summary=new BatchSummary{requestTemplateHash=HashUtil.Text(requestJson),catalogHash=catalog.Sha256,
            startSeed=request.seed,count=count,maxWorkers=workers};
        var watch=Stopwatch.StartNew();
        var gate=new object();
        void SaveSnapshot()
        {
            // Called only under gate. No worker writes the same aggregate file.
            summary.elapsedWallMs=watch.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(output,"batch-summary.json"),JsonIO.Serialize(summary));
            File.WriteAllText(Path.Combine(output,"batch-summary.csv"),ToCsv(summary.rows));
            File.WriteAllText(Path.Combine(output,"index.html"),ToHtml(summary),Encoding.UTF8);
        }
        // Ensure useful (empty) results exist even if Ctrl+C arrives before the first seed finishes.
        lock(gate)SaveSnapshot();
        try
        {
            Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=workers,CancellationToken=cancellationToken},i=>
            {
                if(cancellationToken.IsCancellationRequested)return;
                var seed=request.seed+i;
                // NEVER share mutable JObject/GeneratorRequest across workers.
                var current=(JObject)obj.DeepClone();
                current["seed"]=seed;
                var directory=Path.Combine(output,$"seed-{seed.ToString(CultureInfo.InvariantCulture)}");
                var row=new BatchRow{seed=seed,pattern=request.intent.pattern};
                var perSeedClock=Stopwatch.StartNew();
                try
                {
                    Directory.CreateDirectory(directory);
                    // Each worker owns its directory, attempt/repair budget, solver and replay.
                    var result=execute(current.ToString(Newtonsoft.Json.Formatting.Indented),directory,cancellationToken);
                    row.status=result.status;
                    row.attempts=result.attempts;
                    row.footprint=result.footprint;
                    row.repairsProposed=result.repairsProposed;
                    row.repairsCertified=result.repairsCertified;
                    row.qualityCertifiedCandidates=result.qualitySelection.certifiedCandidatesConsidered;
                    row.qualitySelectionStopReason=result.qualitySelection.stopReason;
                    row.selectedInputHash=result.qualitySelection.selectedInputHash;
                    row.solverCalls=result.diagnostics.solverCalls;
                    row.duplicateCandidatesSkipped=result.diagnostics.duplicateCandidatesSkipped;
                    row.constructorMs=result.diagnostics.constructorMs;
                    row.staticValidationMs=result.diagnostics.staticValidationMs;
                    row.solverReplayMs=result.diagnostics.solverReplayMs;
                    row.analysisMs=result.diagnostics.analysisMs;
                    row.repairProposalMs=result.diagnostics.repairProposalMs;
                    row.independentlyReplayed=result.independentlyReplayed && result.status=="ReviewRequired";
                    row.bottleneckEvidence=result.bottleneckEvidence;
                    row.progressionEvidence=result.progressionEvidence;
                    row.difficultyRating=result.difficultyRating;
                    row.qualityRating=result.qualityRating;
                    row.reportFile=result.reviewFile ?? "";
                    row.draftFile=result.draftFile;
                    row.analysisFile=result.analysisFile;
                    if(row.independentlyReplayed)
                    {
                        try
                        {
                            row.exportedLevelFile=ExportPlaytestLevel(result,seed,directory,playtestDirectory);
                        }
                        catch(Exception ex)
                        {
                            // Certification remains independent from convenience export.
                            row.error="LevelExportWarning: "+ex.GetType().Name+": "+ex.Message;
                        }
                    }
                    if(!string.IsNullOrWhiteSpace(result.draftFile) && File.Exists(result.draftFile))
                    {
                        try
                        {
                            var level=JsonIO.Deserialize<LevelData>(File.ReadAllText(result.draftFile));
                            var svg=LevelPreview.Render(level,catalog,row.independentlyReplayed,
                                $"Seed {seed} · {request.intent.pattern}");
                            var previewPath=Path.Combine(directory,"preview.svg");
                            File.WriteAllText(previewPath,svg,Encoding.UTF8);
                            row.previewFile=previewPath;
                        }
                        catch(Exception ex)
                        {
                            row.error=(row.error==null?"":row.error+"; ")+"PreviewWarning: "+ex.GetType().Name+": "+ex.Message;
                        }
                    }
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {
                    row.status="Cancelled";
                    row.error="Batch cancelled during this seed; result not certified";
                }
                catch(Exception ex)
                {
                    // A failure in one seed does not poison the remaining workers.
                    row.status="Error";
                    row.error=ex.GetType().Name+": "+ex.Message;
                }
                row.elapsedMs=perSeedClock.Elapsed.TotalMilliseconds;
                lock(gate)
                {
                    summary.rows.Add(row);
                    // Completion order varies with scheduling; exports always sort by seed.
                    summary.rows.Sort((a,b)=>a.seed.CompareTo(b.seed));
                    if(row.independentlyReplayed)summary.certifiedCount++;
                    if(row.exportedLevelFile!=null)summary.exportedCount++;
                    if(row.independentlyReplayed && row.bottleneckEvidence is ("ObservedLocalRelief" or "ObservedQueueRevisit"))summary.witnessedCount++;
                    if(row.bottleneckEvidence=="Unknown")summary.unknownEvidenceCount++;
                    if(row.status=="Error")summary.errorCount++;
                    summary.statuses[row.status]=summary.statuses.GetValueOrDefault(row.status)+1;
                    summary.evidenceCounts[row.bottleneckEvidence]=summary.evidenceCounts.GetValueOrDefault(row.bottleneckEvidence)+1;
                    SaveSnapshot();
                    // Progress reporting is serialized, but execution stays parallel.
                    onCompleted?.Invoke(row,summary.rows.Count,count);
                }
            });
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
        {
            // Parallel.For can cancel scheduling while already-running workers return;
            // their individually completed reports remain on disk.
        }
        lock(gate)SaveSnapshot();
        return summary;
    }

    /// <summary>
    /// Export only an unchanged, independently-replayed draft for local gameplay testing.
    /// This copy is NOT an Approved level; DesignerReview.Approve remains separate.
    /// The result folder holds JSON levels only, with names derived from seed numbers.
    /// </summary>
    private string ExportPlaytestLevel(GenerationReport report,int seed,string seedDirectory,string levelsDirectory)
    {
        if(report.status!="ReviewRequired" || !report.independentlyReplayed ||
           string.IsNullOrWhiteSpace(report.inputHash) || string.IsNullOrWhiteSpace(report.draftFile))
            throw new InvalidDataException("Candidate was not independently certified");
        if(seed is <0 or >99999)
            throw new InvalidDataException("Seed must be 0..99999 for Levelxxxxx.json naming");
        if(report.catalogHash!=catalog.Sha256)
            throw new InvalidDataException("Shape catalog hash changed after certification");
        // No arbitrary file reads: only the worker-owned certified draft may be copied.
        var certifiedDraft=Path.GetFullPath(Path.Combine(seedDirectory,"certified-draft.json"));
        if(!string.Equals(Path.GetFullPath(report.draftFile),certifiedDraft,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
            throw new InvalidDataException("Certified draft path differs from the seed output");
        var levelJson=File.ReadAllText(certifiedDraft);
        if(HashUtil.Text(levelJson)!=report.inputHash)
            throw new InvalidDataException("Certified draft hash mismatch");
        var valid=new LevelValidator(catalog).CheckJson(levelJson,25,32);
        if(!valid.valid)
            throw new InvalidDataException("Certified draft fails static validation: "+string.Join("; ",valid.errors));
        var filename="Level"+seed.ToString("D5",CultureInfo.InvariantCulture)+".json";
        var destination=Path.Combine(levelsDirectory,filename);
        // CreateNew refuses accidental overwrites even if a worker/report is duplicated.
        using(var stream=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))
            writer.Write(levelJson);
        return destination;
    }

    /// <summary>Self-contained, offline index linking to per-seed SVG previews and reports.</summary>
    public static string ToHtml(BatchSummary summary)
    {
        static string H(string? s)=>System.Net.WebUtility.HtmlEncode(s??"");
        var html=new StringBuilder();
        html.Append("<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>");
        html.Append("<title>CatDom seed experiment</title><style>body{margin:0;background:#eef2f6;color:#172739;font:15px system-ui,Segoe UI,Arial,sans-serif}header{padding:28px 5%;background:#172739;color:#fff}h1{margin:0 0 8px;font-size:26px}.meta{color:#bdcfde}.wrap{padding:25px 5%}.metrics{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:22px}.metric{background:white;border-radius:12px;padding:15px 22px;min-width:155px}.metric b{display:block;font-size:25px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:19px}.card{background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px #0001}.card img{width:100%;aspect-ratio:1.25;object-fit:contain;background:#f4f7fa}.info{padding:16px}.pill{display:inline-block;border-radius:12px;padding:4px 9px;background:#e5eef4;font-size:12px}a{color:#135888}small{color:#5b6a7a}footer{margin-top:30px;color:#637386}</style></head><body>");
        html.Append("<header><h1>CatDom · Generator seed experiment</h1><div class='meta'>Seed ")
            .Append(summary.startSeed).Append(" · requested ").Append(summary.count).Append(" · finished ").Append(summary.rows.Count)
            .Append(" · workers ").Append(summary.maxWorkers).Append(" · wall time ")
            .Append((summary.elapsedWallMs/1000).ToString("0.0",CultureInfo.InvariantCulture))
            .Append("s · footprint 25–32</div></header><div class='wrap'><div class='metrics'>");
        foreach(var pair in new[]{("Runs",summary.rows.Count),("Certified",summary.certifiedCount),("Exported",summary.exportedCount),("Pattern observed",summary.witnessedCount),("Unknown",summary.unknownEvidenceCount),("Errors",summary.errorCount)})
            html.Append("<div class='metric'><b>").Append(pair.Item2).Append("</b>").Append(H(pair.Item1)).Append("</div>");
        html.Append("</div><div class='grid'>");
        foreach(var row in summary.rows)
        {
            var prefix="seed-"+row.seed.ToString(CultureInfo.InvariantCulture)+"/";
            html.Append("<article class='card'>");
            if(row.previewFile!=null) html.Append("<a href='").Append(prefix).Append("preview.svg'><img loading='lazy' src='")
                .Append(prefix).Append("preview.svg' alt='Seed ").Append(row.seed).Append(" board preview'></a>");
            html.Append("<div class='info'><h2>Seed ").Append(row.seed).Append("</h2><span class='pill'>")
                .Append(H(row.status)).Append("</span> <span class='pill'>")
                .Append(row.independentlyReplayed?"Certified":"Not certified").Append("</span><p>")
                .Append(H(row.pattern)).Append(" · footprint ").Append(row.footprint).Append(" · attempts ").Append(row.attempts)
                .Append(" · elapsed ").Append((row.elapsedMs/1000.0).ToString("0.0",CultureInfo.InvariantCulture)).Append("s")
                .Append(" · solver calls ").Append(row.solverCalls)
                .Append(" · selected from ").Append(row.qualityCertifiedCandidates>0?row.qualityCertifiedCandidates.ToString(CultureInfo.InvariantCulture):"legacy").Append(" certified candidate(s)")
                .Append("<br>Evidence: ").Append(H(row.bottleneckEvidence)).Append("</p>");
            if(row.exportedLevelFile!=null)html.Append("<a href='levels/Level")
                .Append(row.seed.ToString("D5",CultureInfo.InvariantCulture))
                .Append(".json'>Playtest level</a> ");
            if(row.reportFile.Length>0)html.Append("<a href='").Append(prefix).Append("report.json'>Report JSON</a> ");
            if(row.analysisFile!=null)html.Append("<a href='").Append(prefix).Append("analysis.json'>Analysis JSON</a> ");
            if(row.draftFile!=null)html.Append("<a href='").Append(prefix)
                .Append(row.independentlyReplayed?"certified-draft.json":"unverified-draft.json").Append("'>Level JSON</a>");
            if(row.error!=null)html.Append("<p><small>").Append(H(row.error)).Append("</small></p>");
            html.Append("</div></article>");
        }
        html.Append("</div><footer>Preview is schematic. Certification verifies solvability, not quality or forced strategy. Difficulty and quality remain Unrated.</footer></div></body></html>");
        return html.ToString();
    }

    public static string ToCsv(IEnumerable<BatchRow> rows)
    {
        static string Cell(object? value)
        {
            var data=Convert.ToString(value,CultureInfo.InvariantCulture) ?? "";
            return "\""+data.Replace("\"","\"\"")+"\"";
        }
        var builder=new StringBuilder();
        builder.AppendLine("seed,status,pattern,attempts,elapsedMs,footprint,repairsProposed,repairsCertified,independentlyReplayed,bottleneckEvidence,progressionEvidence,reportFile,draftFile,analysisFile,previewFile,error,solverCalls,duplicateCandidatesSkipped,constructorMs,staticValidationMs,solverReplayMs,analysisMs,repairProposalMs,qualityCertifiedCandidates,qualitySelectionStopReason,selectedInputHash");
        foreach(var row in rows)
            builder.AppendLine(string.Join(",",new object?[]{row.seed,row.status,row.pattern,row.attempts,row.elapsedMs,row.footprint,
                row.repairsProposed,row.repairsCertified,row.independentlyReplayed,row.bottleneckEvidence,
                row.progressionEvidence,row.reportFile,row.draftFile,row.analysisFile,row.previewFile,row.error,
                row.solverCalls,row.duplicateCandidatesSkipped,row.constructorMs,row.staticValidationMs,row.solverReplayMs,
                row.analysisMs,row.repairProposalMs,row.qualityCertifiedCandidates,row.qualitySelectionStopReason,row.selectedInputHash}.Select(Cell)));
        return builder.ToString();
    }
}
