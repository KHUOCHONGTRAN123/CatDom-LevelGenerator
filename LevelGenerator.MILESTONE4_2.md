# CatDom Generator — Milestone 4.2: Strong Bottleneck Verification

## Purpose

Offline, evidence-based **bounded alternative-strategy testing** on an *unchanged*
level after a full plan has been solved and independently replayed by V42.

This is NOT an exhaustive global solver. It cannot prove a bottleneck is mandatory,
unique or difficult. The original `ObservedLocalRelief`/`ObservedQueueRevisit`
remains a witness-specific observation. In particular, a failure to find a
bypass NEVER means “the bottleneck is required”.

## Basic usage

From project root, double-click **`Run-M42-Verify-Batch.bat`**. It chooses the
latest `generated/**/batch-summary.json`, normally the newest Guided batch.
Or drag a specific batch directory onto the BAT to select it explicitly.

The BAT runs the exact command:

```powershell
dotnet run --project LevelGenerator.Cli -- verify-batch "generated/<your-batch>" "generated/<your-batch>/verification-m42" IndependentSolver/IO/HoleShapes.json 4 15000 5000 250
```

Use a fresh output directory (no overwrite). CLI arguments after catalog:

1. parallel verification workers (default 4)
2. maximum time to re-solve and independently certify original level in ms (default 15000)
3. per-level alternative search time in ms (default 5000)
4. maximum reordered candidate plans visited per level (default 250)

You can also analyze an individual level:

```powershell
dotnet run --project LevelGenerator.Cli -- verify generated/<batch>/seed-115/certified-draft.json generated/m42-single-115.json IndependentSolver/IO/HoleShapes.json RegionRelease 15000 5000 250
```

Original batch input is **read-only**. Output holds:

```
verification-m42-YYYYMMDD-HHMMSS/
  verification-summary.json
  verification-summary.csv
  Level00115.verification.json
  Level00117.verification.json
  ...
```

Uncertified seeds are marked `SkippedUncertified`. The original `levels/`
folder continues containing only `Levelxxxxx.json` for game testing, unchanged.

## What is tested?

For `RegionRelease`, a witness-specific *clear hole H before consuming source S*
edge is extracted from existing local relief analysis. The verifier tries to
find a **different complete winning plan** consuming S while H is still alive.
For `QueueRevisitParking`, it searches for a complete win whose trace contains
**zero same-actor queue revisit** events.

Candidate plans are permutations of the independently certified drags:

- Original drag paths, endpoints, and per-hole relative move order are retained.
- Reorders are produced by swapping neighboring drags of different holes.
- Focused seeds promote the drag that consumes S before the drag that clears H,
  or delay H until after S. This avoids wasting the entire budget on only local swaps.
- A candidate only counts as a positive bypass after FULL independent V42 replay
  returns `Solved`, and the relevant objective is verified from V42 replay state.
- The alternative plans and SHA-256 hashes are written into each report when found.

This scope cannot find plans with different drag paths, extra parking drags,
new source assignments or a different number of drags. There is **no** algorithm
that checks every possible solution within this budget.

## Understanding the result

- `OrderingBypassFound`: found full-win V42-certified alternatives bypassing all
  tested ordering edges in THIS witness. The claimed ordering is not required.
- `PartialOrderingBypassFound`: at least one tested edge has a positive bypass.
- `Unknown`: searched but no certified bypass found in the finite scope.
- `UnknownNoMotifWitness`: the certified witness did not contain the intended
  pattern to test.
- `UnknownAnalyzerFailure`: the certified witness event stream was not analyzable.
- `Uncertified`: could not re-solve/replay the original level within budget;
  **not** proof of a broken level.

Each individual check additionally records `UnknownBudget` or
`NoBypassInTestedReorders`. Neither proves any global necessity.

The verifier does not assign `difficultyRating` or `qualityRating`.

## Regression and performance

Run `dotnet run --project LevelGenerator.Tests` before verification. Use
`verify-batch` on the 20-seed guided results with `workers=4`, then review
`verification-summary.json` and per-seed report. Never enable this expensive
analysis inside each generation attempt without a separate performance budget.

Source baseline: Milestone 4.1b with playtest `levels/` export. No change to
constructor, solver, repair policies, parallel generation, or footprint 25–32.
No cross-level similarity checks or de-duplication were added.

The packaging environment does not include .NET SDK; compile and execution
checks must be performed on a .NET 10 development machine.
