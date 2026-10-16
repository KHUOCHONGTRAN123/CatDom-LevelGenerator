using System.Diagnostics;
using CatDom.CoreSolver;
using CatDom.Reference.V42.IO;
using Newtonsoft.Json;
using Ref = CatDom.Reference.V42;

namespace CatDom.LevelGenerator;

public sealed class CertificationResult
{
    public string solverStatus = "NotRun";
    public string replayStatus = "NotRun";
    public string explanation = "";
    public double elapsedMs;
    public long expandedStates;
    public string searchAlgorithm="NotRun";
    public bool certified;
    // Populated ONLY when full independent V42 replay certifies the solver plan.
    public List<SolverMove> certifiedMoves = new();
    public string solverAlgorithmVersion = "3.0.0";
}
public sealed class Certifier
{
    private readonly string catalogJson;
    public Certifier(string catalogPath) => catalogJson=File.ReadAllText(catalogPath);
    public CertificationResult Check(string json, int perSolveMs, CancellationToken runToken)
    {
        var result = new CertificationResult();
        var watch = Stopwatch.StartNew();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(runToken);
            deadline.CancelAfter(perSolveMs);
            var solve = CatDom.CoreSolver.CatLevelSolver.SolveLevel(json,deadline.Token);
            result.solverStatus = solve.status.ToString();
            result.expandedStates=solve.expanded;
            result.searchAlgorithm=solve.searchAlgorithm;
            result.solverAlgorithmVersion = solve.algorithmVersion;
            result.explanation = solve.message ?? "";
            if (solve.status != SolverStatus.Solved) return result;
            if (deadline.IsCancellationRequested) {result.solverStatus="CancelledAfterSearch";return result;}
            // Do NOT call the production solver's own replay as the independent replay.
            // Reference V42 has an independent state representation and game engine.
            var catalog = SolverJson.ReadCatalog(catalogJson);
            var input = SolverJson.ReadLevel(json,catalog);
            var moves = solve.moves.Select(m=> new Ref.SolverMove
            {
                holeId=m.holeId,
                start=new Ref.Cell(m.start.x,m.start.y),
                path=m.path.Select(c=>new Ref.Cell(c.x,c.y)).ToList(),
                eaten=new List<Ref.EatEvent>()
            }).ToList();
            var remaining=Math.Max(0.01,(perSolveMs-watch.Elapsed.TotalMilliseconds)/1000.0);
            var replay = Ref.CatLevelSolver.VerifyPlan(input,moves,remaining);
            result.replayStatus=replay.status.ToString();
            result.certified=replay.status==Ref.SolveStatus.Solved && !deadline.IsCancellationRequested;
            if(result.certified) result.certifiedMoves = solve.moves;
            result.explanation=result.certified ? "Solved by 3.0.0 and independently replayed by V42" : replay.message ?? "Independent replay did not certify";
        }
        catch (OperationCanceledException) {result.solverStatus="Cancelled";result.explanation="Cancellation/timeout is not proof of no solution";}
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        { result.replayStatus="ReplayFailed";result.explanation=ex.Message; }
        catch (Exception ex)
        { result.replayStatus="ToolFailure";result.explanation="Unexpected solver/replay failure (not a gameplay verdict): "+ex.Message; }
        finally {result.elapsedMs=watch.Elapsed.TotalMilliseconds;}
        return result;
    }
}
