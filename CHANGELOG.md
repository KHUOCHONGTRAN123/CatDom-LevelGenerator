## LevelGenerator Milestone 3 — Seed Benchmark + Visual Review
- Adds safe bounded CLI `batch` experiments (up to 20 seeds) and reproducible JSON/CSV summary and offline gallery.
- Adds geometry `preview` command with explicit certified/unverified labels.
- Does not modify original solver, reference replay, game corpus or the 25..32 footprint rule.
- C# compile and runtime verification not performed here because .NET SDK is unavailable.

# Changelog

## 3.0.0

- Expand/Bomb/Spawner/Curtain improvements and geometry caching.
- Countdown-free search with original-limit audit and CountdownExceeded reporting.
- Updated batch tools build current source; mechanic benchmark limits documented in docs/RELEASE_3.0.0.md.

## 2.0.0

- Chạy solver 1.0.0 tối đa 10.000 full-state expansions; khi chưa giải xong thì chạy solver cải tiến từ đầu level. Không chọn thuật toán bằng thời gian hoặc ID level.
- Giữ thứ tự moves và counter 1.0.0 cho level giải trong ngân sách. Lời giải chưa hoàn tất của lượt baseline không ghép vào lời giải cải tiến.
- `expanded` và `solveTimeMs` bao gồm cả hai lượt. Thêm `searchAlgorithm`, `baselineExpanded`, `improvedExpanded`; `algorithmVersion` là `2.0.0`.
- Thêm target corridor, relaxed pair/triple pattern ordering và cache cạnh/khoảng cách; giữ successor và replay mechanic chính xác, cancellation và lời gọi đồng thời.
- Dữ liệu benchmark riêng cho nhóm dễ, thử ngưỡng 5.000 và hybrid 10.000. Phương án 5.000 không được chọn.
- Không cam kết tổng hybrid <30.000 trạng thái/<3 giây. Hai level 206/233 còn là giới hạn đã biết; chưa chứng minh giải nhanh trên mọi level mới.

## 1.0.0

- Release solver v42, API .NET Standard 2.1, CLI .NET 10 và bundle Unity.


## Milestone 3.1: Parallel seed batch
- Run independent seeds in parallel with `batch ... <count> --workers <n>` (default up to four).
- Preserve per-seed isolation and sorted aggregate reports; add concurrent progress snapshots.
- Honor Ctrl+C cancellation and retain completed output; never overwrite previous batch.
- Add concurrency/sorting/cancellation regression tests using a deterministic fake runner.

### Milestone 4.2 — Bounded alternative-plan verification
- Added an **offline** V42-certified alternate complete-plan checker with
  focused reorder seeds and strict per-level time/state budgets.
- New `verify` and `verify-batch` CLI commands, plus Windows double-click BAT.
- Original generator, parallel workers, footprint 25–32, certified levels export,
  and absence of cross-level similarity filtering are unchanged.
- Outcomes never infer global bottleneck necessity from an incomplete search.


### Milestone 4.3 — Quality-Guided Generation
- Added optional evidence-first bounded selection from several certified candidates per seed, preserving the original first-certified default.
- Added explicit config limits, deterministic tie handling, selected candidate provenance in report and batch summaries.
- Added one-click 20-level Windows BAT and two quality request examples.
- Kept solver/V42 replay, 25–32 footprint, per-seed worker isolation, and `levels/Levelxxxxx.json` exports.
- Did not add cross-level deduplication, geometric similarity ranking, global necessity conclusions or score-based difficulty/quality.
