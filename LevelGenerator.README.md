> **Milestone 3 update:** batch benchmarks, offline gallery and SVG previews are documented in [`LevelGenerator.MILESTONE3.md`](LevelGenerator.MILESTONE3.md). Existing Milestone 2 commands remain supported.

# CatDom Level Generator — Milestone 2 (analysis + repair)

The code adds a **C# .NET 10 CLI** without changing the production solver, independent reference replay, or original `Levels/` fixtures. See `Generate Docs/LEVELGEN_IMPLEMENTATION_AMENDMENT.md` for the updated footprint requirement **25..32 inclusive**, replacing the former fixed-25 target for this implementation.

## What is implemented

- Versioned request (`levelgen-prototype/0.1`) with seed, preset revision, catalog hash, explicit run/solve/alternative budgets and intent.
- Deterministic scaffold/role-first construction for `RegionRelease` and `QueueRevisitParking`. It can propose a geometry motif; it **cannot yet prove** a causal bottleneck or quality.
- Strong static validation for 7×8, catalog shapes, hole/cat/wall/box collision, interior wall component, 0..9 colors, per-hole footprint demand, matching color supply across board and queues, queue positions/directions, and **hole footprint 25..32**. Strict input reader refuses unfamiliar gameplay fields instead of dropping them.
- Production Solver 3.0.0 plus independent V42 reference-gameplay replay. Solved **without** V42 verification is not certified.
- Bounded attempt loop, stable per-attempt seed, config/catalog/level hashes, trace and safe `unverified-draft.json` versus `certified-draft.json` status.
- Guarded designer approval with rationale and no overwrite, exporting exact `Levelxxxxx.json` plus separate `.review.json` audit.

## Prerequisites

.NET SDK **10.0**, NuGet access for `Newtonsoft.Json 13.0.3` (also required by the existing solver/reference).

## Commands (from ZIP project root)

```bash
# Verify catalog content hash
 dotnet run --project LevelGenerator.Cli -- catalog IndependentSolver/IO/HoleShapes.json

# Generate a candidate with RegionRelease intent (can legitimately exhaust budget)
 dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/RegionRelease.request.json generated/region IndependentSolver/IO/HoleShapes.json

# Generate a candidate with QueueRevisitParking intent
 dotnet run --project LevelGenerator.Cli -- generate LevelGenerator.Examples/QueueRevisitParking.request.json generated/queue IndependentSolver/IO/HoleShapes.json

# Inspect a generated draft, or a reference with footprint in-range
 dotnet run --project LevelGenerator.Cli -- validate generated/region/certified-draft.json IndependentSolver/IO/HoleShapes.json

# Run C# smoke tests
 dotnet run --project LevelGenerator.Tests

# Manual approval only if report.status == ReviewRequired and replay passed
 dotnet run --project LevelGenerator.Cli -- approve generated/region/report.json generated/approved/Level90001.json IndependentSolver/IO/HoleShapes.json "Đã kiểm gameplay và thấy cách phá tắc hợp lý"
```

A nonzero exit code from `generate` with `BudgetExhausted`, `Cancelled` or `AnalysisUnknown` is **not** proof that the level cannot be solved. Inspect `generated/.../report.json` and `unverified-draft.json`; never export an unverified draft as a level.

## Milestone 2 extensions

`LevelGenerator.Core/Analysis.cs` adds replayed-witness timeline and local geometry-shadow observations. `LevelGenerator.Core/Repair.cs` adds four bounded intent-preserving, validated repair operations with re-certification and trace. Set `intent.unknownPolicy` to `Repair` to prefer a pattern-specific witnessed event while retaining a certified fallback. The original `Review` policy still returns the first certified candidate. Run `analyze <Level.json> <analysis.json> <HoleShapes.json> <pattern> [solveMs]` on an existing certified draft. See [LevelGenerator.MILESTONE2.md](LevelGenerator.MILESTONE2.md) for full Windows commands and evidence limitations.

`bottleneckEvidence=ObservedLocalRelief` means there is witness-local geometric relief with continuation, **not** proof of necessity or uniqueness. `ObservedQueueRevisit` means one already verified winning plan included a revisit, **not** that this revisit is forced. If these do not occur, evidence remains `Unknown`. `difficultyRating` and `qualityRating` remain `Unrated`.

No bounded alternative strategy search or human-experience scoring has been implemented. `alternativeSearchMs` and `alternativeSearchMaxStates` are not consumed by alternative search. The artifact packaging environment lacks the .NET SDK so Milestone 2 compilation and runtime tests need to be executed on a .NET 10 machine.

## Safety invariants

Solver and original level fixtures are untouched. Never relabel an uncertified draft as Approved, and never treat a local geometric diagnostic as global causal proof. The `ReviewRequired` state only permits **human review**, not automatic quality labeling.


## Parallel batch (Milestone 3.1)

Run 10 levels with 8 workers:

```powershell
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.repair.request.json generated/parallel-region-10 IndependentSolver/IO/HoleShapes.json 10 --workers 8
Start-Process generated/parallel-region-10/index.html
```

Without `--workers`, batch uses up to 4 workers (also capped by CPU count and seed count). Every seed has its own solver, replay, budget and folder. See `LevelGenerator.MILESTONE3_PARALLEL.md` for cancellation semantics, safety and performance notes.

## Milestone 4.1b — Performance diagnostics + guided repair (opt-in)

See [`LevelGenerator.MILESTONE4_1B.md`](LevelGenerator.MILESTONE4_1B.md). The new `diagnose` command reads existing batch output without rerunning solver. A/B testing uses `RegionRelease.repair.request.json` (baseline) and `RegionRelease.guided.request.json` (opt-in). All solver/replay certification requirements and footprint **25–32** remain unchanged; no cross-level similarity filter is added.


### Playtest-only level exports (4.1b patch)

`batch` automatically writes only certified (solver + independent V42 replay)
`Levelxxxxx.json` game files to `<output>/levels/`. Names use the numerical seed:
`seed-115` -> `levels/Level00115.json`. No logs, reports, or review metadata
are written to this directory. If a seed is not certified, no level is exported.
Export checks the original certified draft hash, shape catalog hash, and
static gameplay validation (footprint 25..32) before copying. The JSON
is an exact byte-for-byte *text* copy, not an approved level.
`batch-summary.json` adds `exportedCount`, and each row adds
`exportedLevelFile` when a copy exists. GUI gallery links to these copies.

When running the A/B BAT, each variant has its own `levels` directory,
and the BAT also copies **Guided** certified levels into the top-level
`<AB run>/levels/` directory for easy testing.

## Milestone 4.2 — Offline Strong Bottleneck Verification

Double-click `Run-M42-Verify-Batch.bat` after generating a batch. It selects
its latest batch and verifies certified levels in a **separate output folder**.
The original `levels/` folder is not touched.

For CLI usage and correct interpretation of `OrderingBypassFound` versus
`Unknown`, see `LevelGenerator.MILESTONE4_2.md`. No global necessity claim is
made; failure to find a bypass under a limited reorder search is inconclusive.


## Milestone 4.3 — Quality-guided selection (optional)

See [LevelGenerator.MILESTONE4_3.md](LevelGenerator.MILESTONE4_3.md). For one-click Windows testing, run `Run-M43-Quality-20.bat`. The new `qualitySelection` config block controls **bounded within-seed choice among independently certified candidates**. It does not assign quality/difficulty scores, check similarity across levels, or replace designer review. Batch `levels/` continues containing only playable `Levelxxxxx.json` files. Existing request examples remain unchanged.
