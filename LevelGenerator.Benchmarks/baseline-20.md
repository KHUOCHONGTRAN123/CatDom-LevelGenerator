# Baseline 20 seed — Milestone 3.1

- Seeds: **115–134**; workers **10**.
- Certified: **16/20**; budget exhausted: **4**.
- Wall time: **510.2s**; attempts: **463**.
- Repair proposals: **3610**; certified repairs: **10**.

## Failure reasons counted in original trace (including repairs)

| Solver explanation | Count |
|---|---:|
| All reachable parking states exhausted without a consumption event. | 3702 |
| No movable component has a consumption target. | 207 |

## Repair operators

| Operator | Proposals | Certified | Certified / proposal |
|---|---:|---:|---:|
| MoveCat | 1792 | 5 | 0.28% |
| EditQueueSupply | 898 | 2 | 0.22% |
| MoveHole | 471 | 0 | 0.00% |
| MoveWall | 449 | 3 | 0.67% |

## Caveats
- Old M3.1 reports do not retain timings for each repair/solver call.
- Original attempt solver status can be overwritten after repair succeeds, so solverStatusesApprox is not an exact unique evaluation count.
- The 3702 parking exhaustion and 207 missing targets reflect recorded events, not necessarily unique candidate layouts.
- No claims of improved speed are possible until controlled A/B benchmark is run.
- No cross-level similarity filtering performed.
