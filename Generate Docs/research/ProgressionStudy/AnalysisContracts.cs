using System.Security.Cryptography;
using System.Text;
using CatDom.CoreSolver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

[JsonConverter(typeof(StringEnumConverter))]
internal enum SearchStatus { Found, NotFoundWithinBudget, ProvedAbsentWithinCompleteScope, InvalidStartState, Unsupported }
[JsonConverter(typeof(StringEnumConverter))]
internal enum EvidenceClass { RuleNecessary, ObservedWitness, LocalDiagnostic, Hypothesis, ScopedSearchNecessary }
internal sealed record Evidence(string id, EvidenceClass @class, string statement, string artifact, string scope, string limitations);
internal sealed record SearchScope(string startStateHash, string prefixHash, string goalPredicate,
    string forbiddenEvents, string continuationPredicate, string successorGeneration, string stateEquivalence,
    int maxDepth, int maxExpanded, int seconds, bool completeEnumeration);
internal sealed record SearchResult(string id, SearchStatus status, SearchScope scope, int expanded,
    int generated, int frontierRemaining, string stopReason, string completenessRationale,
    string witnessHash, string endStateHash, string replayStatus, object witnessSummary);
internal sealed record BreakAlternative(string strategySignature, string eventType, string witnessPlanHash,
    string startStateHash, string endStateHash, string searchResultRef, string replayStatus, string signatureVersion="witness-style/0.1");
internal sealed record BottleneckRecord(string schemaVersion, string recordId, string levelHash, string catalogHash,
    string analysisVersion, string startStateHash, string goalPredicateVersion, string[] blockedTasks,
    int[] affectedActors, object[] constraints, object[] supportTasks, BreakAlternative[] alternatives,
    object relief, Evidence[] evidence, string[] unknowns);

internal static class AnalysisContracts
{
    internal const string Version="bottleneck-analysis/0.1";
    internal static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    // Canonical field order; arrays preserve their engine-defined order. Input/catalog hashes identify geometry.
    internal static string Canonical(JToken value)=>Sorted(value).ToString(Formatting.None);
    static JToken Sorted(JToken value)=>value switch {
        JObject o=>new JObject(o.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Sorted(p.Value)))),
        JArray a=>new JArray(a.Select(Sorted)),
        _=>value.DeepClone()
    };
    internal static string StateHash(BoardSnapshot state)=>Hash("snapshot-canonical-v1:"+Canonical(JToken.FromObject(state)));
    internal static string PlanHash(List<EngineMove> plan)=>Hash(Canonical(JToken.FromObject(plan.Select(m=>new{m.holeId,m.start,m.path}))));
    internal static void Validate(SearchResult r)
    {
        if(string.IsNullOrWhiteSpace(r.id)||string.IsNullOrWhiteSpace(r.scope.startStateHash))throw new ArgumentException("Missing identity/start provenance");
        if(r.status==SearchStatus.Found && (string.IsNullOrEmpty(r.witnessHash)||string.IsNullOrEmpty(r.endStateHash)||!r.replayStatus.StartsWith("Independent")))
            throw new ArgumentException("Found requires independently replayed witness");
        if(r.status==SearchStatus.ProvedAbsentWithinCompleteScope && (!r.scope.completeEnumeration||r.frontierRemaining!=0||string.IsNullOrWhiteSpace(r.completenessRationale)))
            throw new ArgumentException("Absence requires complete scoped enumeration and empty frontier");
        if(r.scope.maxDepth<0||r.scope.maxExpanded<0||r.scope.seconds<0)throw new ArgumentException("Negative budget");
    }
    internal static void Validate(BottleneckRecord r)
    {
        if(r.schemaVersion!=Version||r.blockedTasks.Length==0||r.evidence.Length==0||r.constraints.Length==0)throw new ArgumentException("Incomplete bottleneck record");
        var relief=JObject.FromObject(r.relief);
        if(relief["residualTasks"] is not JArray || relief["cleanupTasks"] is not JArray)throw new ArgumentException("Missing residual/cleanup attribution, including explicit Unknown");
        if(r.alternatives.Any(a=>!a.replayStatus.StartsWith("Independent")))throw new ArgumentException("Unverified alternative");
        if(r.evidence.Any(e=>e.@class==EvidenceClass.ScopedSearchNecessary && string.IsNullOrWhiteSpace(e.scope)))throw new ArgumentException("Unscoped necessity");
    }
    internal static void Tests(BoardSnapshot snapshot)
    {
        var copy=JsonConvert.DeserializeObject<BoardSnapshot>(JsonConvert.SerializeObject(snapshot));
        var original=StateHash(copy);copy.holes[0].remaining++;if(StateHash(copy)==original)throw new Exception("Demand missing from hash");
        copy=JsonConvert.DeserializeObject<BoardSnapshot>(JsonConvert.SerializeObject(snapshot));copy.boxes[0].consumed++;
        if(StateHash(copy)==original)throw new Exception("Queue state missing from hash");
        var scope=new SearchScope(original,"prefix","goal","none","none","first-event anchors","full snapshot",2,10,1,false);
        bool rejected=false;try{Validate(new SearchResult("bad",SearchStatus.ProvedAbsentWithinCompleteScope,scope,10,10,1,"budget","",null,null,"None",null));}catch(ArgumentException){rejected=true;}
        if(!rejected)throw new Exception("Budget exhaustion accepted as proof");
        rejected=false;try{Validate(new SearchResult("bad-found",SearchStatus.Found,scope,0,0,0,"goal","",null,null,"None",null));}catch(ArgumentException){rejected=true;}
        if(!rejected)throw new Exception("Unverified Found accepted");
        if(Canonical(JObject.Parse("{\"b\":2,\"a\":1}"))!=Canonical(JObject.Parse("{\"a\":1,\"b\":2}")))throw new Exception("Unstable property ordering");
        Console.WriteLine("PASS contract guards: demand/queue hash, canonical ordering, budget != absence, unverified != Found");
    }
}
