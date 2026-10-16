using Newtonsoft.Json.Linq;

namespace CatDom.LevelGenerator;

public static class RequestValidator
{
    public const string CurrentRevision = "2026-10-15-footprint-25-32-v1";
    private static readonly string[] Fields = {"requestVersion", "seed", "presetId", "presetRevision", "catalogHash", "intent", "targetPolicy", "operatorPolicy", "budgets"};
    public static List<string> Validate(JObject root, GeneratorRequest? request, ShapeSet shapes)
    {
        var errors = new List<string>();
        foreach (var name in Fields) if (root[name] == null || root[name]!.Type == JTokenType.Null) errors.Add($"Missing request.{name}");
        foreach (var group in new Dictionary<string,string[]> {
            ["intent"] = new[] {"id","version","pattern","unknownPolicy","blockedTasks","actorRoles","constraintRoles","candidateBreakTypes","expectedUnlockedTasks","continuationGoal","allowedAlternatives","protectedIntent"},
            ["targetPolicy"] = new[] {"minHoleFootprint","maxHoleFootprint","targetHoleFootprint","boxCount","deviationPolicy"},
            ["operatorPolicy"] = new[] {"enabled"},
            ["budgets"] = new[] {"maxAttempts","maxRepairsPerAttempt","totalRunMs","perSolveMs","alternativeSearchMs","alternativeSearchMaxStates"}
        })
        {
            if (root[group.Key] is not JObject block) { errors.Add($"request.{group.Key} must be an object"); continue; }
            foreach (var key in group.Value) if (!block.ContainsKey(key)) errors.Add($"Missing request.{group.Key}.{key}");
        }
        if (request == null) { errors.Add("Could not parse request"); return errors; }
        if (request.requestVersion != "levelgen-prototype/0.1") errors.Add("Unsupported requestVersion");
        if (request.presetId != "wall-box-7x8-bottleneck") errors.Add("Unsupported presetId");
        if (request.presetRevision != CurrentRevision) errors.Add($"Unsupported presetRevision (expected {CurrentRevision})");
        if (!string.Equals(request.catalogHash, shapes.Sha256, StringComparison.OrdinalIgnoreCase)) errors.Add("catalogHash mismatch");
        if (request.intent.pattern is not ("RegionRelease" or "QueueRevisitParking")) errors.Add("Unsupported intent.pattern");
        if (string.IsNullOrWhiteSpace(request.intent.id) || string.IsNullOrWhiteSpace(request.intent.version)) errors.Add("intent.id/version required");
        if (request.intent.unknownPolicy is not ("Review" or "Reject" or "Repair")) errors.Add("unknownPolicy must be Review, Repair or Reject; Repair prefers witness evidence and retains a certified fallback");
        if (!request.intent.protectedIntent) errors.Add("Prototype requires protectedIntent=true");
        if (request.intent.blockedTasks.Length == 0 || request.intent.actorRoles.Length == 0 || request.intent.constraintRoles.Length == 0 || request.intent.candidateBreakTypes.Length == 0 || request.intent.expectedUnlockedTasks.Length == 0 || string.IsNullOrWhiteSpace(request.intent.continuationGoal)) errors.Add("Intent role/task declarations required");
        if (request.targetPolicy.minHoleFootprint != 25 || request.targetPolicy.maxHoleFootprint != 32) errors.Add("This revision requires hole footprint inclusive range 25..32");
        if (request.targetPolicy.targetHoleFootprint is int target && (target < 25 || target > 32)) errors.Add("Target must be null or within 25..32");
        if (request.targetPolicy.boxCount is < 1 or > 4) errors.Add("boxCount must be 1..4");
        if (request.targetPolicy.deviationPolicy is not ("Review" or "Reject")) errors.Add("deviationPolicy must be Review or Reject");
        if (root["operatorPolicy"]?["failureAware"] is JToken failureAwareToken && failureAwareToken.Type != JTokenType.Boolean)
            errors.Add("operatorPolicy.failureAware must be a boolean if supplied");
        if (request.operatorPolicy.enabled.Any(op => op != "MoveCat" && op != "MoveHole" && op != "MoveWall" && op != "EditQueueSupply")) errors.Add("Unsupported repair operator");
        // M4.3 quality-guided selection is entirely opt-in and has finite search bounds.
        if (root["qualitySelection"] is JToken qualityToken)
        {
            if (qualityToken is not JObject qualityBlock)
                errors.Add("qualitySelection must be an object");
            else
            {
                foreach (var prop in qualityBlock.Properties())
                    if (prop.Name is not ("enabled" or "maxCertifiedCandidates" or "maxAdditionalAttemptsAfterFirst"))
                        errors.Add("Unsupported qualitySelection field: "+prop.Name);
                if (qualityBlock["enabled"]?.Type != JTokenType.Boolean)
                    errors.Add("qualitySelection.enabled must be boolean");
                if (qualityBlock["maxCertifiedCandidates"]?.Type != JTokenType.Integer ||
                    qualityBlock["maxAdditionalAttemptsAfterFirst"]?.Type != JTokenType.Integer)
                    errors.Add("qualitySelection bounds must be integers");
                if (request.qualitySelection.maxCertifiedCandidates is <1 or >20 ||
                    request.qualitySelection.maxAdditionalAttemptsAfterFirst is <0 or >100)
                    errors.Add("qualitySelection bounds: maxCertifiedCandidates 1..20, maxAdditionalAttemptsAfterFirst 0..100");
            }
        }
        if (request.qualitySelection.enabled && request.intent.unknownPolicy == "Reject")
            errors.Add("qualitySelection.enabled requires unknownPolicy Review or Repair; Reject demands global proof unavailable in v0.1");
        if (request.budgets.maxAttempts is < 1 or > 10000 || request.budgets.maxRepairsPerAttempt is < 0 or > 100 || request.budgets.totalRunMs is < 100 or > 3600000 || request.budgets.perSolveMs is < 100 or > 600000 || request.budgets.alternativeSearchMs is < 0 or > 600000 || request.budgets.alternativeSearchMaxStates is < 0 or > 10000000)
            errors.Add("Invalid budgets; all bounds must be explicit and finite");
        return errors;
    }
}

public sealed class LevelValidator
{
    private readonly Dictionary<int,Point[]> shapes;
    public LevelValidator(ShapeSet set) => shapes = set.Footprints;
    public ValidationResult Check(LevelData level, int minFootprint = 25, int maxFootprint = 32, int? target = null)
    {
        var result = new ValidationResult();
        var errors = result.errors;
        if (level.sizeX != 7 || level.sizeY != 8) errors.Add("P-SIZE: only 7x8 is supported");
        if (level.countdownMoveLimit != 0) errors.Add("P-SCOPE: countdown mechanic must be disabled");
        if (level.catBoxInfos.Count is < 1 or > 4) errors.Add("P-BOX: box count must be 1..4");
        var inside = (Point p) => p.x >= 0 && p.y >= 0 && p.x < level.sizeX && p.y < level.sizeY;
        var walls = new HashSet<Point>(level.obstacleInfos.Concat(level.brokenObstacleInfos));
        foreach (var w in walls) if (!inside(w)) errors.Add($"P-WALL: wall out of bounds {w}");
        if (!HasInteriorWallComponent(new HashSet<Point>(level.obstacleInfos), level.sizeX, level.sizeY)) errors.Add("P-WALL: missing isolated interior wall component");
        var occupied = new HashSet<Point>(walls);
        var mouths = new HashSet<Point>();
        foreach (var (b,idx) in level.catBoxInfos.Select((b,i)=>(b,i)))
        {
            if (b.direction is < 0 or > 3) { errors.Add($"P-BOX: invalid direction box {idx}"); continue; }
            if (b.requiredHolesToUnlock != 0) errors.Add($"P-SCOPE: box {idx} locked");
            var offsets = new[] { new Point(0,1), new Point(-1,0), new Point(0,-1), new Point(1,0) };
            var mouth = new Point(b.position.x + offsets[b.direction].x, b.position.y + offsets[b.direction].y);
            if (!inside(mouth) || walls.Contains(mouth)) errors.Add($"P-BOX: invalid mouth box {idx}");
            if (inside(b.position) && !occupied.Add(b.position)) errors.Add($"P-PLACEMENT: box {idx} body collision");
            if (!mouths.Add(mouth)) errors.Add("P-BOX: duplicate mouth position");
            if (b.colorIds.Count == 0) errors.Add($"P-BOX: box {idx} has no queue");
            foreach (var c in b.colorIds)
            {
                if (c.colorId is < 0 or > 9) errors.Add($"P-COLOR: queue {idx} invalid color {c.colorId}");
                if (c.position != b.position) errors.Add($"P-BOX: queue token position mismatch box {idx}");
                AddCount(result.supply, c.colorId);
            }
        }
        foreach (var (h, idx) in level.holeInfos.Select((h,i)=>(h,i)))
        {
            if (!shapes.TryGetValue(h.holeType, out var footprint)) { errors.Add($"P-SHAPE: hole {idx} unsupported type {h.holeType}"); continue; }
            if (h.colorIds.Length != 1 || h.numOfCats.Length != 1 || h.colorIds[0] is < 0 or > 9)
            { errors.Add($"P-SCOPE/P-COLOR: hole {idx} must have a single color 0..9"); continue; }
            if (h.numOfCats[0] != footprint.Length) errors.Add($"P-CORRECT: hole {idx} demand must equal shape area {footprint.Length}");
            result.footprint += footprint.Length;
            AddCount(result.demand,h.colorIds[0],h.numOfCats[0]);
            foreach (var p in footprint)
            {
                var cell = new Point(h.position.x+p.x,h.position.y+p.y);
                if (!inside(cell)) errors.Add($"P-SHAPE: hole {idx} footprint outside board {cell}");
                else if (!occupied.Add(cell)) errors.Add($"P-PLACEMENT: hole {idx} collides at {cell}");
            }
        }
        foreach (var (c,idx) in level.catInfos.Select((c,i)=>(c,i)))
        {
            if (c.colorId is < 0 or > 9) errors.Add($"P-COLOR: cat {idx} invalid color {c.colorId}");
            if (!inside(c.position)) errors.Add($"P-PLACEMENT: cat {idx} outside board");
            else if (!occupied.Add(c.position)) errors.Add($"P-PLACEMENT: cat {idx} overlaps a hole/cat/blocker at {c.position}");
            AddCount(result.supply,c.colorId);
        }
        foreach (var color in result.demand.Keys.Union(result.supply.Keys).OrderBy(x=>x))
            if (result.demand.GetValueOrDefault(color) != result.supply.GetValueOrDefault(color))
                errors.Add($"P-CORRECT: color {color} demand={result.demand.GetValueOrDefault(color)} supply={result.supply.GetValueOrDefault(color)}");
        result.coverageStatus = result.footprint < minFootprint || result.footprint > maxFootprint ? "OutsideRange" :
            target.HasValue && result.footprint != target.Value ? "WithinRangeTargetDeviation" : "WithinRange";
        if (result.coverageStatus == "OutsideRange") errors.Add($"P-TARGET: footprint {result.footprint} outside {minFootprint}..{maxFootprint}");
        return result;
    }
    public ValidationResult CheckJson(string json, int minFootprint=25, int maxFootprint=32, int? target=null)
    {
        var root = JObject.Parse(json);
        var result = new ValidationResult();
        var allowed = new HashSet<string> { "countdownMoveLimit", "sizeX", "sizeY", "holeInfos", "catInfos", "obstacleInfos", "brokenObstacleInfos", "catBoxInfos" };
        foreach (var prop in root.Properties()) if (!allowed.Contains(prop.Name))
            result.errors.Add($"P-SCOPE: unsupported/unknown field {prop.Name}; refusing to ignore gameplay metadata");
        static void CheckFields(JToken? a, string[] names, List<string> errors, string prefix)
        {
            if (a is not JArray list) return;
            var known = names.ToHashSet();
            foreach (var obj in list.OfType<JObject>()) foreach (var prop in obj.Properties())
                if (!known.Contains(prop.Name)) errors.Add($"P-SCOPE: unsupported field {prefix}.{prop.Name}");
        }
        CheckFields(root["holeInfos"],new[]{"colorIds","position","holeType","numOfCats"},result.errors,"holeInfos");
        CheckFields(root["catInfos"],new[]{"colorId","position"},result.errors,"catInfos");
        CheckFields(root["catBoxInfos"],new[]{"colorIds","position","direction","requiredHolesToUnlock"},result.errors,"catBoxInfos");
        foreach (var b in root["catBoxInfos"] as JArray ?? new JArray()) CheckFields(b["colorIds"],new[]{"colorId","position"},result.errors,"catBoxInfos.colorIds");
        if (root["countdownMoveLimit"] is JValue count && count.Type != JTokenType.Integer)
            result.errors.Add("P-SCOPE: countdownMoveLimit must be integer zero");
        if (root["holeInfos"] is not JArray || root["catInfos"] is not JArray || root["obstacleInfos"] is not JArray || root["catBoxInfos"] is not JArray)
            result.errors.Add("P-SCOPE: required arrays holeInfos/catInfos/obstacleInfos/catBoxInfos");
        if (result.errors.Count != 0) return result;
        LevelData? level;
        try { level = JsonIO.Deserialize<LevelData>(json); }
        catch (Exception ex) { result.errors.Add("Malformed level: "+ex.Message); return result; }
        return Check(level,minFootprint,maxFootprint,target);
    }
    private static void AddCount(Dictionary<int,int> dict,int color,int amount=1) => dict[color] = dict.GetValueOrDefault(color)+amount;
    private static bool HasInteriorWallComponent(HashSet<Point> walls,int width,int height)
    {
        var seen = new HashSet<Point>();
        foreach (var start in walls)
        {
            if (!seen.Add(start)) continue;
            var queue = new Queue<Point>(); queue.Enqueue(start);
            var interior = true;
            while (queue.Count>0)
            {
                var p = queue.Dequeue();
                if (p.x <= 0 || p.y <= 0 || p.x >= width-1 || p.y >= height-1) interior = false;
                foreach (var n in new[]{new Point(p.x+1,p.y),new Point(p.x-1,p.y),new Point(p.x,p.y+1),new Point(p.x,p.y-1)})
                    if (walls.Contains(n) && seen.Add(n)) queue.Enqueue(n);
            }
            if (interior) return true;
        }
        return false;
    }
}
