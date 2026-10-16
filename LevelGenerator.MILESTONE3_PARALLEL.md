# CatDom Milestone 3.1 — Parallel Seed Batch

## Change

`batch` now runs **different seeds in parallel**, not the attempts within a single seed. Each seed has an isolated `GenerationRunner`, `Certifier`, stopwatch, attempt/repair budget, random seed and output folder. An aggregate coordinator writes `batch-summary.json`, `batch-summary.csv`, and `index.html` under a lock; results are always sorted by seed regardless of completion order.

The 7×8 board and **25–32 inclusive hole footprint** rule are unchanged. Solver 3.0.0, independent V42 replay, analysis, repair and designer approval remain unchanged.

## Windows usage

From project root (requires .NET 10 SDK):

```powershell
# Compile and test
 dotnet run --project LevelGenerator.Tests

# 10 levels, eight parallel workers (choose a NEW output directory)
 dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.repair.request.json generated/parallel-region-10 IndependentSolver/IO/HoleShapes.json 10 --workers 8

# View results
 Start-Process generated/parallel-region-10/index.html

# Check concurrency and result totals
 $s = Get-Content generated/parallel-region-10/batch-summary.json -Raw | ConvertFrom-Json
 $s | Select-Object count,maxWorkers,elapsedWallMs,certifiedCount,witnessedCount,errorCount
 $s.rows | Format-Table seed,status,elapsedMs,attempts,independentlyReplayed
```

The old 5-argument `batch` command is still supported. It automatically uses `min(seedCount, CPU count, 4)` workers. You can override with `--workers N` (1 to seedCount, max 20), or the legacy shorthand trailing number (`... 10 8`).

**Practical performance guidance:** Start with `--workers 4` and try `6`, `8` if memory remains comfortable. A single solver search may allocate substantial memory. Higher workers improve throughput only while the machine still has CPU and memory headroom; they can also increase per-seed latency. Parallelism does NOT increase each seed's per-run time budget.

## Cancellation / data safety

- `Ctrl+C` requests cooperative cancellation, stops new workers and retains completed per-seed results and partial summary. Some in-progress seeds may be marked `Cancelled` rather than certified.
- Each seed is deterministic given its request+seed+catalog+solver, but **wall-clock timeouts and CPU contention can change results**: higher worker counts may cause more budget exhaustions or timeouts. Compare results with equal resources when evaluating generator improvements.
- The batch refuses to overwrite a nonempty output directory. Choose a fresh directory each run.
- Independent V42 replay remains required for a certified result. Gallery previews are not a certification verdict.
- `elapsedWallMs` is batch wall time; `rows[].elapsedMs` is each seed's duration. Summing per-seed duration is no longer equivalent to batch time.

## Smoke tests

Tests exercise overlapping workers, worker limit, one-run-per-seed, deterministic summary row ordering, persisted output, single-worker execution, bad worker count and pre-cancellation using an inexpensive stub runner.

**Environment limitation:** The provided sandbox has no .NET SDK and cannot execute builds/tests. Run the listed test command on Windows before using large batches.
