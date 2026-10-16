# CatDom Level Generator — Milestone 4.3 Quality-Guided Generation

## What changed

Generation may now select from **multiple independently certified candidates for the same seed**. This is strictly opt-in. Existing `.request.json`, `.repair.request.json`, and `.guided.request.json` files continue the original **first-certified** behavior with identical constructor sampling seeds. No cross-level similarity, deduplication, geometry penalty, or global catalog of previous levels was added.

The optional request block is:

```json
"qualitySelection": {
  "enabled": true,
  "maxCertifiedCandidates": 3,
  "maxAdditionalAttemptsAfterFirst": 12
}
```

`maxCertifiedCandidates` (1–20) limits how many independently replayed candidates can be considered per seed. `maxAdditionalAttemptsAfterFirst` (0–100) limits the number of **new constructor attempts** after the first certified result. This search is also bounded by the unchanged `budgets.totalRunMs` and `budgets.maxAttempts`. Once a candidate is certified, the generator keeps it as a fallback and never replaces it with an uncertified candidate. If the best candidate was produced by repair, the final selected level still has to have its own V42 certification.

Selection priority, in order:

1. An observed instance of the **requested pattern** (`RegionRelease`: a locally relieved source subsequently consumed; `QueueRevisitParking`: replayed same-actor queue revisit).
2. A replayed progression timeline.
3. For RegionRelease witnesses only, whether one or at least two **distinct relieved sources** are used later (capped at 2).
4. If all these observations tie, keep the **earlier independently certified** candidate.

These are **observational evidence checks**. No drag-based scoring, similarity filtering, human quality score, difficulty rating or mandatory bottleneck proof is added. An observation in one solution does not guarantee a forced strategy across all solutions. Human gameplay review remains required.

## Quick start (Windows)

Place `Run-M43-Quality-20.bat` beside `LevelGenerator.Cli` and double-click. It runs `RegionRelease.quality.request.json`, 20 seeds starting at 115, and 10 parallel workers. Each seed has its own `GenerationRunner`, solver and replay. The script does **not** pre-create the batch output directory, and uses a unique path so older batch outputs remain unchanged.

Manual equivalent:

```powershell
dotnet run --project LevelGenerator.Tests
dotnet run --project LevelGenerator.Cli -- batch LevelGenerator.Examples/RegionRelease.quality.request.json generated/m43-region-test IndependentSolver/IO/HoleShapes.json 20 --workers 10
Start-Process generated/m43-region-test/index.html
```

For the QueueRevisitParking pattern, replace the request path with `LevelGenerator.Examples/QueueRevisitParking.quality.request.json` and choose a fresh output directory.

## Files and auditing

```text
generated/m43-region-test/
  batch-summary.json        # Aggregated counts, per-seed selected hashes and limits
  batch-summary.csv
  index.html
  levels/                   # ONLY selected and independently certified Levelxxxxx.json files
    Level00115.json
    ...
  seed-115/
    report.json             # qualitySelection.candidates, selectedInputHash, stopReason
    analysis.json           # ONLY the selected level's witness analysis
    certified-draft.json    # ONLY the selected level's certified JSON
    preview.svg
  ...
```

`levels/` is for **local playtesting**, not designer approval. A seed that has no certified candidate produces no Levelxxxxx.json. Verify a selected level against its report using `report.inputHash` and `report.qualitySelection.selectedInputHash`: they should match.

Legacy requests: `qualitySelection.enabled=false` (the default when the block is absent) selects the first certified acceptable result. For apples-to-apples evaluation use the same seed/config except for the `qualitySelection` block, with the same worker count and machine load. Quality-guided mode may use **more solver time**; performance optimization is intentionally deferred.

`verify-batch` from Milestone 4.2 remains an offline step and has not been expanded. Its `Unknown` verdict remains valid when no bypass is found within bounded search.

## Limits and safety

- Still board 7×8, hole footprint **25–32**, 1–4 boxes and the existing two patterns.
- Never export an unverified candidate as a game level.
- No global Difficulty/Quality 0–100 claims.
- No cross-seed similarity filter.
- No automatic forcedness conclusions from the witness analyzer.
- Current environment cannot execute `dotnet` to compile/run the project; run `LevelGenerator.Tests` on Windows before a large batch and report any build errors.
