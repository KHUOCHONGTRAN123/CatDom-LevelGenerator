# Milestone 4.1b — Benchmarked generation optimization (experimental)

This builds on v0.1 Milestone 3.1 (parallel batch) and keeps board 7x8, **hole footprint 25–32**, supply/demand invariants, independent solver/replay certification, both motifs, and per-seed isolation. **No cross-level duplicate or similarity filters** are introduced.

## Implemented

1. **Solver outcome classification.** Existing `NoNextCatReachable` outcomes are split by actual engine message into `NoMovableConsumptionTarget` and `ParkingSearchExhausted`. These are *diagnostic results* and do not prove the level is intrinsically unsolvable. Timeout and cancellation remain inconclusive.
2. **Opt-in failure-aware repair (`operatorPolicy.failureAware=true`).** For parking-state exhaustion: prefer `MoveWall` and `MoveHole`. For missing consumption target: prefer `MoveCat` and `EditQueueSupply`; the `MoveCat` proposal can try a free cell adjacent to a same-color hole. All proposals still pass unchanged structural validation and protected intent checks, followed by independent solve/replay before certification. The default is `false`, preserving baseline repair behavior.
3. **Fine-grained telemetry.** Each new `report.json` includes `diagnostics` containing timing sums for constructor, intent check, static validation, solver/replay, analyzer, and repair proposal; solver outcome groups, operator efficacy, exact local repeat counts, and number of calls. Batch rows/CSV carry the telemetry for each seed.
4. **Safe micro-optimizations.** Constructor-owned typed levels are validated without serializing/deserializing JSON just to run static validation. An *exact* candidate seen again **within the same generation attempt** is not re-solved only if the previous search returned `NoNextCatReachable` or `Solved`. Cancelled/timeouts are never cached. There is no cross-seed or similarity-based filtering.
5. **Offline `diagnose` command.** Inspects old/new `batch-summary.json` + per-seed `report.json` and outputs `diagnostics.json` and `diagnostics.md`. Does not invoke solver and does not modify existing level files.
6. **Comparable A/B sampling.** With identical request except `operatorPolicy.failureAware`, constructor attempt seeds are unchanged. `resolvedConfigHash` still changes so full inputs remain auditable; `samplingConfigHash` identifies the exact constructor seed stream.

## Build/test

```powershell
# From the extracted project root
dotnet run --project LevelGenerator.Tests
```

The authored tests verify classification, backward compatible config validation, deterministic guided repair, protected intent, preserved parent object, A/B sampling, and offline diagnostics. This package has **NOT** been compiled in the authoring environment because .NET 10 SDK is unavailable there. Building and running tests on your Windows machine is required.

## Analyze your existing 20-seed batch without regenerating

After extracting `parallel-region-10-w10(1).zip` so it contains the `parallel-region-10-w10` folder:

```powershell
dotnet run --project LevelGenerator.Cli -- diagnose parallel-region-10-w10
Start-Process parallel-region-10-w10/diagnostics.md
```

Open the Markdown file in your editor if Windows does not associate `.md` with a viewer. Historic Milestone 3.1 reports do **not** contain per-repair stage timings; the diagnostic tool reports that data as unavailable instead of inventing it.

## Fair A/B comparison on same 20 seeds

Run each variant in a **fresh** output directory with the same count/workers/hardware, preferably when the machine is not doing competing heavy work:

```powershell
# A: original repair policy; unchanged .repair.request.json
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.repair.request.json generated/m41b-baseline-20 IndependentSolver/IO/HoleShapes.json 20 --workers 10

# B: same geometry sampling, failure-aware repair enabled
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.guided.request.json generated/m41b-guided-20 IndependentSolver/IO/HoleShapes.json 20 --workers 10

# Analyze both sets offline
dotnet run --project LevelGenerator.Cli -- diagnose generated/m41b-baseline-20
dotnet run --project LevelGenerator.Cli -- diagnose generated/m41b-guided-20
```

Also supported: `QueueRevisitParking.guided.request.json`.

**Compare:** certifiedCount, witnessedCount, budgetExhaustedCount, batchElapsedWallMs, solver failure categories, totals/efficacy by repair operator, and stage timings. The new policy is experimental, not claimed faster before this comparison. For accurate timings, compare NEW 4.1b baseline to NEW 4.1b guided; old 3.1 results have incomplete telemetry.

## What is intentionally NOT implemented yet

- Globally necessary bottleneck proof (Milestone 4.2).
- Candidate quality/difficulty scoring (Milestone 4.3).
- Deduplication, layout similarity measures, or filtering near-similar levels.
- Rejection of a candidate because a cheap heuristic thinks it is hard to solve.

## Baseline evidence

See `LevelGenerator.Benchmarks/baseline-20.md` and `.json`. Source: user batch with 20 seeds (115–134), 10 workers, 16 certified in about 510 seconds, 3,610 repair proposals. Baseline numbers are **observed**, not projected performance of 4.1b.
