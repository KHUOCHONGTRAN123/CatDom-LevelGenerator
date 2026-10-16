# Implementation amendment — 2026-10-15

**Supersedes only the previously fixed 25-cell target in GENERATOR_DESIGN/PRESET/CURRENT_WORK.**

The user explicitly set initial hole footprint to an **inclusive range of 25 through 32 cells** on the fixed 7×8 board. The generator must not reject a level merely because its hole footprint is 26–32. Supply still matches demand exactly, including tokens queued in boxes; no mechanic or collision rule is relaxed.

- Preset implementation revision `2026-10-15-footprint-25-32-v1`.
- `targetPolicy.minHoleFootprint=25`, `maxHoleFootprint=32`, `targetHoleFootprint=null` means all eight values are valid; a target may optionally select one value within the range.
- `coverageStatus=WithinRange` for 25..32 with no target; `WithinRangeTargetDeviation` is a valid level outside an optional target, not an invalid level.
- The 45%/25-cell description in historic preset is not a requirement of this implementation revision.
- The generator is still limited to 7×8. It doesn't auto-relax placement/geometry/supply to make 32 fit.

Implementation deliverable: initial C# CLI/source milestone includes config validation, deterministic intent-first geometry constructor, strict static validation, production solver 3.0.0 adapter, reference V42 independent replay adapter, audit reports, guarded approval/export, and smoke test source. This is **not** a claim that generator v0.1 causal analysis or repair has been completed. `RegionRelease` / `QueueRevisitParking` geometry are currently **hypotheses**; only verified evidence may elevate them to Supported.
