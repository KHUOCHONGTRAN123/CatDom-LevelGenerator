namespace CatDom.LevelGenerator;

/// <summary>
/// Observational telemetry only. It must not change gameplay validity or solver verdicts.
/// Phase times are wall-clock sums across proposals (NOT the elapsed time of the batch).
/// </summary>
public sealed class GenerationDiagnostics
{
    public string schemaVersion="levelgen-generation-diagnostics/0.1";
    public double elapsedMs;
    public double constructorMs;
    public double intentCheckMs;
    public double staticValidationMs;
    public double solverReplayMs;
    public double analysisMs;
    public double repairProposalMs;
    public int solverCalls;
    public int validCandidates;
    public int invalidCandidates;
    public int duplicateCandidatesSkipped;
    public int repairProposalsSkipped;
    public Dictionary<string,int> solverStatuses=new(StringComparer.Ordinal);
    public Dictionary<string,int> solverFailureCategories=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOperatorsProposed=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOperatorsCertified=new(StringComparer.Ordinal);
    public Dictionary<string,int> repairOutcomes=new(StringComparer.Ordinal);
    public Dictionary<string,int> attemptOutcomes=new(StringComparer.Ordinal);
    public Dictionary<string,int> constructionFailures=new(StringComparer.Ordinal);

    public static void Increment(Dictionary<string,int> map,string key)
        =>map[key]=map.GetValueOrDefault(key)+1;
}

/// <summary>
/// A diagnostic grouping of engine outcomes, NOT a proof that a level is unsolvable.
/// Preserve original solverStatus and message on the record for audit.
/// </summary>
public static class SolverFailureClassifier
{
    public static string Classify(string status,string? explanation)
    {
        if(status=="Solved")return "Solved";
        if(status=="NoNextCatReachable")
        {
            if(explanation?.Contains("No movable component has a consumption target",StringComparison.OrdinalIgnoreCase)==true)
                return "NoMovableConsumptionTarget";
            if(explanation?.Contains("parking states exhausted",StringComparison.OrdinalIgnoreCase)==true)
                return "ParkingSearchExhausted";
            return "NoNextCatReachableOther";
        }
        return status switch
        {
            "Cancelled" or "CancelledAfterSearch" => "TimeoutOrCancelled",
            "InvalidInput"=>"InvalidInput",
            "UnsupportedMechanics"=>"UnsupportedMechanics",
            "NotRun"=>"NotRun",
            _=>"OtherSolverOutcome"
        };
    }
}

/// <summary>
/// Keep constructor sampling identical for baseline/guided A/B runs. The experimental
/// failureAware switch affects only repair policy, not layout sampling. Full resolved
/// config hash in the report still tracks the switch for auditability.
/// </summary>
public static class GeneratorSeeding
{
    public static string SamplingConfigHash(GeneratorRequest request)
    {
        var tree=Newtonsoft.Json.Linq.JObject.Parse(JsonIO.Serialize(request));
        tree.Remove("qualitySelection"); // Selection must NOT change constructor sampling seeds.
        if(tree["operatorPolicy"] is Newtonsoft.Json.Linq.JObject ops)
            ops.Remove("failureAware");
        return HashUtil.Text(tree.ToString(Newtonsoft.Json.Formatting.Indented));
    }
    public static int AttemptSeed(int rootSeed,string samplingConfigHash,int attemptIndex)
    {
        var bytes=System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{rootSeed}|{samplingConfigHash}|{attemptIndex}"));
        return BitConverter.ToInt32(bytes,0);
    }
}
