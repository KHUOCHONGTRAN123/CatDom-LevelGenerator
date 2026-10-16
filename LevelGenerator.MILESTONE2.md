# CatDom Level Generator v0.1 — Milestone 2

Extends the working Milestone 1 source. Board stays **7 × 8**, total hole footprint stays **25–32 inclusive**. Original `Levels/`, original Solver 3.0.0 and the V42 reference implementation are not changed.

## What's new

- `WitnessAnalyzer`: reads **only a solver plan independently certified by V42**, emits `analysis.json` (phase timeline, board/queue consume, hole clear, reposition, queue-live-visit/revisit, actor switches, and move counts).
- A local **footprint-shadow** check for clear events compares target/source reachability in **the same post-clear remaining state** with and without the cleared hole's old footprint. A target is recorded as used in continuation only if the same certified witness later consumes it. This is a limited geometric diagnostic, not a game-wide necessity/uniqueness proof. A positive local observation is labeled `ObservedLocalRelief`; a queue revisit in the witness is labeled `ObservedQueueRevisit`. When absent, evidence remains `Unknown`.
- `CandidateRepair`: bounded, deterministic **MoveCat**, **MoveHole**, **MoveWall** and **EditQueueSupply** operations, guided by failure category. Each proposal is cloned from a parent, static validated, checked against protected intent preconditions, and **must be newly solved + independently replayed**. Both successful and unsuccessful repair attempts have trace records and parent/child hashes. Old certification is never transferred.
- `intent.unknownPolicy` supports `Review` (unchanged Milestone 1 behavior), `Repair` (prefer observed pattern evidence, retain an independently certified fallback for review if search fails) and `Reject` (strict unsupported global claim is not accepted). No policy calls an observed pattern a globally forced strategy.
- `generate` produces `report.json` and, on a certified candidate, `certified-draft.json` plus `analysis.json` with its hash. `approve` checks that analysis file hash before export. Stale success artifacts are removed when rerunning generation in the same output directory.
- Standalone `analyze` command can re-solve and analyze a **Milestone 1** certified draft without generating a new level.

## Prerequisites

Install **.NET SDK 10** (the project's existing TargetFramework), and restore NuGet dependencies on a machine with connectivity. All commands below assume PowerShell and the ZIP project root as current directory.

## Run tests

```powershell
dotnet run --project LevelGenerator.Tests
```

Tests include construction, static invariants, seed determinism, rollback/non-mutation, valid repairs and conservative analyzer statuses. This is not a replacement for runtime solver/replay verification on generated levels.

## Test the milestone with a new candidate

```powershell
dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/RegionRelease.request.json generated/m2-region IndependentSolver/IO/HoleShapes.json
```

To actively **prefer witnessed gameplay structure** (rather than stop at the first independently certified candidate), run the supplied `.repair.request.json` sample:

```powershell
dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/RegionRelease.repair.request.json generated/m2-region-repair IndependentSolver/IO/HoleShapes.json
```

The `Repair` policy retains the first independently certified level as a fallback if no pattern-specific observation is found within the configured budget. The returned candidate may therefore still have `Unknown` evidence; check the warning and analysis. This is a search policy, not difficulty/quality scoring.

Or the other pattern:

```powershell
dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/QueueRevisitParking.request.json generated/m2-queue IndependentSolver/IO/HoleShapes.json

# Prefer an observed queue-revisit witness:
dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/QueueRevisitParking.repair.request.json generated/m2-queue-repair IndependentSolver/IO/HoleShapes.json
```

Inspect result:

```powershell
$r = Get-Content generated/m2-region/report.json -Raw | ConvertFrom-Json
$r | Select-Object status,attempts,repairsProposed,repairsCertified,bottleneckEvidence,analysisFile,draftFile
$r.repairTrace | Select-Object -First 15 attempt,step,operatorName,state,reason
$a = Get-Content generated/m2-region/analysis.json -Raw | ConvertFrom-Json
$a | Select-Object pattern,bottleneckEvidence,drags,clearEvents,queueLiveVisits,queueRevisits,actorSwitches
$a.localRelief | Select-Object drag,clearedHole,newlyReachable,usedInContinuation
```

`analysis.json` only exists if a candidate is independently certified and accepted for review. A `BudgetExhausted` outcome is not proof of unsolvability. A certified level is not automatically a good or difficult level.

## Analyze an existing certified draft from Milestone 1

```powershell
dotnet run --project LevelGenerator.Cli -- analyze generated/region/certified-draft.json generated/region/analysis-m2.json IndependentSolver/IO/HoleShapes.json RegionRelease 30000
```

This command explicitly re-solves and independently verifies the level before writing analytics. Replace the pattern argument with `QueueRevisitParking` for queue candidates. The last argument is optional `solveMs` (default 30000 ms).

## Compare generation with and without repair

Copy a request JSON, edit `budgets.maxRepairsPerAttempt` to `0` (repair disabled) or `8` (enabled), use **separate output directories**. Because normalized config hashes contribute to attempt seeds, these are not strict paired trials; compare aggregate rates, not individual candidate identity. All solve/repair runs respect `budgets.totalRunMs` and `budgets.perSolveMs`.

## Known limits / not yet implemented

- **No complete scoped alternative search**: `alternativeSearchMs` and `alternativeSearchMaxStates` are parsed and validated but not consumed by an alternative-solution search. Therefore no claim of global necessity, forcedness or unique strategy is made.
- Footprint-shadow checks are static translation reachability. They do **not** model all transient rules, consumption timing, queue order possibilities or interactions; they are not a causal proof for all possible plays.
- Repairs are failure-guided mutations, not an optimizer calibrated for difficulty or quality. Failure to certify within time/budget does not prove unsolvability.
- The analyzer uses solver-produced eat events after the *full independently certified plan*; if those events are incomplete or inconsistent, status stays `Unknown` and the plan must not be labeled as causally verified.
- Designer assessment for **Satisfaction, Elegance and Fairness** still remains human. `difficultyRating` and `qualityRating` always remain `Unrated`.

## Validation status

This package was inspected and subjected to static checks in an environment without the .NET SDK. **It has not been compiled or run here.** The user's Windows .NET 10 machine should run `dotnet run --project LevelGenerator.Tests` first. Any compiler or runtime errors should be treated as unresolved until that succeeds.
