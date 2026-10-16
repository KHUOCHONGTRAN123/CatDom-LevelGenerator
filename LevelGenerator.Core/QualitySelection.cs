namespace CatDom.LevelGenerator;

/// <summary>
/// Observations used to select between separately certified candidates for the SAME
/// seed. NOT a difficulty score, player-experience score or proof of a forced tactic.
/// No cross-seed comparisons and no geometry similarity/deduplication.
/// </summary>
public sealed class QualityCandidateSummary
{
    public int ordinal;
    public int attempt;
    public int repairStep;
    public string inputHash = "";
    public string evidence = "Unknown";
    public string progression = "Unknown";
    public bool intendedPatternObserved;
    public int observedContinuationSources;
    public bool selected;
    public string decision = "";
}
public sealed class QualitySelectionReport
{
    public string schemaVersion = "levelgen-quality-selection/0.1";
    public bool enabled;
    public int maxCertifiedCandidates;
    public int maxAdditionalAttemptsAfterFirst;
    public int certifiedCandidatesConsidered;
    public string selectedInputHash = "";
    public string stopReason = "Disabled";
    public string explanation = "Candidate selection uses observed witness evidence only, never difficulty or experience scoring.";
    public List<QualityCandidateSummary> candidates = new();
}

public static class QualityCandidateSelector
{
    public static QualityCandidateSummary Describe(AnalysisReport evidence,string pattern,string hash,int attempt,int repairStep,int ordinal)
    {
        var intended=pattern switch
        {
            "RegionRelease"=>evidence.localRelief.Any(r=>r.usedInContinuation.Count>0),
            "QueueRevisitParking"=>evidence.queueRevisits>0,
            _=>false
        };
        // These are real post-break sources later consumed in the same replayed witness.
        // Do not reward many drags, extra moves, raw clear counts or map complexity.
        var used = pattern=="RegionRelease"
            ? evidence.localRelief.SelectMany(x=>x.usedInContinuation).Distinct(StringComparer.Ordinal).Count()
            : 0;
        return new QualityCandidateSummary{
            ordinal=ordinal,attempt=attempt,repairStep=repairStep,inputHash=hash,
            evidence=evidence.bottleneckEvidence,progression=evidence.progressionEvidence,
            intendedPatternObserved=intended,observedContinuationSources=used
        };
    }

    /// <summary>
    /// Ordinal evidence-first selection. Equal evidence retains the earlier certified
    /// candidate (deterministic); source counts do not pretend to quantify fun.
    /// </summary>
    public static int Compare(QualityCandidateSummary x,QualityCandidateSummary y)
    {
        int c=x.intendedPatternObserved.CompareTo(y.intendedPatternObserved);
        if(c!=0)return c;
        c=(x.progression=="ObservedWitnessTimeline").CompareTo(y.progression=="ObservedWitnessTimeline");
        if(c!=0)return c;
        if(x.intendedPatternObserved && x.evidence=="ObservedLocalRelief" &&
           y.evidence=="ObservedLocalRelief")
        {
            // Cap evidence richness. Beyond a small number of actual follow-through
            // sources, extra moves/sources should not be treated as "more fun".
            c=Math.Min(2,x.observedContinuationSources).CompareTo(Math.Min(2,y.observedContinuationSources));
        }
        return c;
    }
}
