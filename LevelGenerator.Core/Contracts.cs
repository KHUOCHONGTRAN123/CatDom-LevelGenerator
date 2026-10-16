using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CatDom.LevelGenerator;

// Versioned request; every timing and search bound is mandatory in JSON input.
public sealed class GeneratorRequest
{
    public string requestVersion = "";
    public int seed;
    public string presetId = "";
    public string presetRevision = "";
    public string catalogHash = "";
    public BottleneckIntent intent = new();
    public TargetPolicy targetPolicy = new();
    public OperatorPolicy operatorPolicy = new();
    public GenerationBudgets budgets = new();
    // Optional; legacy requests continue stopping at the first certified candidate.
    public QualitySelectionPolicy qualitySelection = new();
}

public sealed class QualitySelectionPolicy
{
    public bool enabled;
    public int maxCertifiedCandidates = 3;
    public int maxAdditionalAttemptsAfterFirst = 12;
}

public sealed class BottleneckIntent
{
    public string id = "";
    public string version = "";
    public string pattern = "";
    public string unknownPolicy = "";
    public string[] blockedTasks = Array.Empty<string>();
    public string[] actorRoles = Array.Empty<string>();
    public string[] constraintRoles = Array.Empty<string>();
    public string[] candidateBreakTypes = Array.Empty<string>();
    public string[] expectedUnlockedTasks = Array.Empty<string>();
    public string continuationGoal = "";
    public string[] allowedAlternatives = Array.Empty<string>();
    public bool protectedIntent = true;
}
public sealed class TargetPolicy
{
    public int minHoleFootprint;
    public int maxHoleFootprint;
    public int? targetHoleFootprint;
    public int boxCount;
    public string deviationPolicy = "";
}
public sealed class OperatorPolicy
{
    public string[] enabled = Array.Empty<string>();
    // Optional in v0.1 to keep all existing request files valid and comparable.
    public bool failureAware;
}
public sealed class GenerationBudgets
{
    public int maxAttempts;
    public int maxRepairsPerAttempt;
    public int totalRunMs;
    public int perSolveMs;
    public int alternativeSearchMs;
    public int alternativeSearchMaxStates;
}
public readonly record struct Point(int x, int y)
{
    public override string ToString() => $"({x},{y})";
}
public sealed class Shape
{
    public int holeType;
    public Point[] footprint = Array.Empty<Point>();
}
public sealed class ShapeSet
{
    public List<Shape> shapes = new();
    [JsonIgnore] public string Sha256 = "";
    [JsonIgnore] public Dictionary<int, Point[]> Footprints => shapes.ToDictionary(s => s.holeType, s => s.footprint);
    public static ShapeSet Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var catalog = JsonConvert.DeserializeObject<ShapeSet>(System.Text.Encoding.UTF8.GetString(bytes))
            ?? throw new InvalidDataException("Invalid shape catalog.");
        if (catalog.shapes.Count == 0 || catalog.shapes.GroupBy(s => s.holeType).Any(g => g.Count() > 1))
            throw new InvalidDataException("Empty catalog or duplicate holeType.");
        if (catalog.shapes.Any(s => s.footprint.Length == 0 || s.footprint.Distinct().Count() != s.footprint.Length || s.footprint.Any(p => p.x < 0 || p.y < 0)))
            throw new InvalidDataException("Invalid catalog footprint.");
        catalog.Sha256 = HashUtil.Bytes(bytes);
        return catalog;
    }
}
public sealed class LevelData
{
    public int countdownMoveLimit;
    public int sizeX = 7, sizeY = 8;
    public List<HoleData> holeInfos = new();
    public List<CatData> catInfos = new();
    public List<Point> obstacleInfos = new();
    public List<Point> brokenObstacleInfos = new();
    public List<BoxData> catBoxInfos = new();
}
public sealed class HoleData
{
    public int[] colorIds = Array.Empty<int>();
    public Point position;
    public int holeType;
    public int[] numOfCats = Array.Empty<int>();
}
public sealed class CatData
{
    public int colorId;
    public Point position;
}
public sealed class BoxData
{
    public List<CatData> colorIds = new();
    public Point position;
    public int direction;
    public int requiredHolesToUnlock;
}
public sealed class ValidationResult
{
    public List<string> errors = new();
    public int footprint;
    public string coverageStatus = "Unknown";
    public Dictionary<int,int> demand = new();
    public Dictionary<int,int> supply = new();
    public bool valid => errors.Count == 0;
}
public sealed class AttemptRecord
{
    public int index;
    public int attemptSeed;
    public string state = "";
    public string detail = "";
    public string? inputHash;
    public int footprint;
    public string solverStatus = "NotRun";
    public string replayStatus = "NotRun";
    public double solveTimeMs;
    public double constructTimeMs;
    public double validationTimeMs;
    public double analysisTimeMs;
    public long solverExpandedStates;
    public string solverFailureCategory = "NotRun";
}
public sealed class GenerationReport
{
    public string contract = "levelgen-prototype/0.1";
    public string status = "";
    public string inputHash = "";
    public string resolvedConfigHash = "";
    public string samplingConfigHash = "";
    public string catalogHash = "";
    public string solverVersion = "3.0.0";
    public int seed;
    public string pattern = "";
    public int footprint;
    public string coverageStatus = "Unknown";
    public string bottleneckEvidence = "Unknown";
    public string progressionEvidence = "Unknown";
    public string difficultyRating = "Unrated";
    public string qualityRating = "Unrated";
    public string warning = "";
    public bool independentlyReplayed;
    public int attempts;
    public List<AttemptRecord> trace = new();
    public string? draftFile;
    public string? reviewFile;
    public string? analysisFile;
    public string? analysisHash;
    public int repairsProposed;
    public int repairsCertified;
    public List<RepairRecord> repairTrace = new();
    public GenerationDiagnostics diagnostics = new();
    public QualitySelectionReport qualitySelection = new();
}
public static class JsonIO
{
    public static readonly JsonSerializerSettings Settings = new() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Include };
    public static string Serialize(object x) => JsonConvert.SerializeObject(x, Settings);
    public static T Deserialize<T>(string json) where T : class => JsonConvert.DeserializeObject<T>(json)
        ?? throw new ArgumentException("Unable to deserialize JSON.");
}
public static class HashUtil
{
    public static string Bytes(byte[] data) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
    public static string Text(string data) => Bytes(System.Text.Encoding.UTF8.GetBytes(data));
}
