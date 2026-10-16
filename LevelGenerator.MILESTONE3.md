# CatDom Level Generator v0.1 — Milestone 3 (Seed Benchmark + Visual Review)

Milestone 3 **extends Milestone 2** without modifying `IndependentSolver/`, `Verification/`, the original corpus under `Levels/`, or the previous request schemas. The 7×8 board and **25–32 inclusive** hole footprint requirement remain unchanged.

## What changed

- New `batch` command runs 1–20 consecutive seeds starting at the request JSON's `seed`, each in its own output directory. Each run uses the original bounded solver/replay/repair process and its own per-run budget. **Update (Milestone 3.1):** seed experiments now run in parallel; see `LevelGenerator.MILESTONE3_PARALLEL.md`.
- New `batch-summary.json` (machine readable), `batch-summary.csv` (Excel-friendly), and offline `index.html` gallery with per-seed links.
- New `preview` command renders a **static SVG board** from a validated level. It depicts hole footprints using the real shape catalog, walls, cats, boxes and box mouths. It is not a gameplay renderer.
- When a batch produces a candidate, the gallery includes `seed-N/preview.svg`; certified drafts are labeled differently from unverified drafts.
- The batch refuses to run in a nonempty output directory to avoid overwriting previous experiments. Partial summaries are written after each seed, so interrupted batches retain finished runs.
- Milestone 3 smoke tests check escaped SVG and HTML, CSV quoting, preview validation, the no-overwrite guard, and the 20-seed limit.

## Windows / PowerShell instructions

From the project root, using **.NET 10 SDK**:

```powershell
dotnet run --project LevelGenerator.Tests
```

Preview an **existing generated level** from Milestone 2:

```powershell
dotnet run --project LevelGenerator.Cli -- preview generated/m2-region/certified-draft.json generated/m2-region/preview.svg IndependentSolver/IO/HoleShapes.json generated/m2-region/report.json
Start-Process generated/m2-region/preview.svg
```

If you do not have a certification report, omit the final report argument and the SVG will explicitly say **NOT CERTIFIED**.

Compare **three** consecutive RegionRelease seeds, with `unknownPolicy: Repair`:

```powershell
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.repair.request.json generated/m3-region-001 IndependentSolver/IO/HoleShapes.json 3
Start-Process generated/m3-region-001/index.html
```

Other pattern:

```powershell
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/QueueRevisitParking.repair.request.json generated/m3-queue-001 IndependentSolver/IO/HoleShapes.json 3
Start-Process generated/m3-queue-001/index.html
```

**Important:** Choose a NEW empty output directory each time. Each seed retains its own `totalRunMs` budget; with Milestone 3.1 the wall time can be lower because seeds run in parallel (depending on worker count and CPU/memory contention). Start with 1–3 seeds and use different directories for each experiment.

To inspect aggregate results:

```powershell
$b = Get-Content generated/m3-region-001/batch-summary.json -Raw | ConvertFrom-Json
$b | Select-Object count,certifiedCount,witnessedCount,unknownEvidenceCount,errorCount
$b.rows | Format-Table seed,status,footprint,attempts,repairsProposed,independentlyReplayed,bottleneckEvidence
```

## Interpretation

- **Certified** = solver solved the level and V42 independently replayed the solution. This does NOT mean the level is entertaining.
- **Pattern observed** means a *witness-specific* observation (`ObservedLocalRelief` or `ObservedQueueRevisit`); this does NOT establish that every solution needs that mechanism.
- **Unknown** = no supported pattern observation; it is not an unsolvable verdict.
- `difficultyRating` and `qualityRating` remain `Unrated` until human evaluation / an agreed calibration method exists.
- Some batches may produce no certified levels because solver/repair attempts are bounded. This is a useful experimental result, not automatically a generator bug.

## Test / validation limits

The project was statically checked and its ZIP manifest examined in the sandbox, but the sandbox lacks the .NET SDK and cannot compile, run the C# smoke tests or certify generator output. Run `dotnet run --project LevelGenerator.Tests` on the user's .NET 10 machine before using the new commands. The new batch + preview have not been measured on live generated data here.

## Next technical milestone (after measured runs)

Analyze the aggregate seed results and identify the top causes of construction failure, solver timeouts, missing pattern evidence or repair loops. Improve the constructor and targeted repair operators accordingly, while retaining the separate notion of correctness, witness evidence and human quality review.
