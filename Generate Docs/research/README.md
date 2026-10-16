# Harness và bằng chứng phân tích điểm tắc

Đọc [CURRENT_WORK](../LEVELGEN_CURRENT_WORK.md) để biết mốc đã hoàn tất và [prototype spec](../LEVELGEN_GENERATOR_DESIGN.md) để bắt đầu implementation sau goal nghiên cứu. Thư mục này là research adapter, không phải generator production.

## Artifact cần đọc

| Artifact | Vai trò |
|---|---|
| `causal-study.json` | Kết quả mới:4BottleneckRecord,10SearchResult,3case,6full witness checks và2alternative win continuations; scope/status/Unknown có provenance |
| `analysis.schema.json` | JSON Schema2020-12 cho contract bottleneck-analysis/0.1; score vẫn null/Unrated |
| `access-focus-hybrid.json`, `access-focus-improved.json` | Access/footprint-shadow/wall diagnostics của00129/00098/00115; CausalStudy kiểm lại input/plan hashes trước khi dùng |
| `bottleneck-original-00115.json` | Snapshot/eating events hai lời giải gốc00115, không lưu full đường đi plan |
| `ProgressionStudy/AnalysisContracts.cs` | Typed records, state/plan canonical hashes, guards và negative tests |
| `ProgressionStudy/TargetedSearch.cs` | Beam search và finite endpoint schedule enumeration; luật từ engine replay/geometry |
| `ProgressionStudy/CausalStudy.cs` | Ba case solve/replay, targeted queries, local relief và live queue observations |
| `ProgressionStudy/AccessStudy.cs`, `BottleneckStudy.cs` | Các adapter ban đầu còn dùng để tái lập diagnostics/snapshots |

Không sinh thêm variants hoặc metric score trong run này. Không sửa Levels. Chỉ lưu summaries/hash/state evidence, full plan ở memory.

## Build và chạy

Từ workspace root, .NET SDK10 (máy hiện tại10.0.302):

```powershell
dotnet build 'Generate Docs/research/ProgressionStudy/ProgressionStudy.csproj' -c Release -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false /p:NuGetAudit=false --ignore-failed-sources
dotnet 'Generate Docs/research/ProgressionStudy/bin/Release/net10.0/Solver.Verification.dll' 'D:\Generate VIPPRO' causal
Test-Json -LiteralPath 'Generate Docs/research/causal-study.json' -SchemaFile 'Generate Docs/research/analysis.schema.json' -ErrorAction Stop
```

`Test-Json` cần PowerShell7 có hỗ trợ JSON Schema; máy hiện tại đã chạy thành công. Typed guards chạy ngay trong modecausal: queue/demand đổi làm statehash đổi, canonical property ordering, budget≠absence, thiếu independent replay không được Found.

Modecausal solve hai mode mỗi level với deadline25giây; beam queries00115: Q-A18giây, Q-C12giây/actor, finite schedules không có heuristic pruning và chỉ tối đa một action/actor theo đích cố định. Tổng runtime phụ thuộc máy. Run ghi đè artifact cùng tên; muốn so lần chạy phải chủ động giữ result/hash trước. Wall-clock budget làm số expanded có thể khác, không là nondeterminism gameplay.

Các mode còn lại: `bottleneck` ghi snapshot00115; `access-focus` và `access-focus-improved` chạy lại access ba reference. Modecausal cần hai access-focus artifact còn giữ để đối chiếu provenance và chọn mốc shadow, không cần benchmark solver cũ.

Oracle JSONL đã bỏ warm-up Level00001 và trả info.version3.0.0 từ source runtime. CausalStudy tham chiếu source trực tiếp, không cần bridge:

```powershell
dotnet build Verification/Verification.csproj -c Release -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false /p:NuGetAudit=false --ignore-failed-sources
'{"op":"info"}' | dotnet Verification/bin/Release/net10.0/Solver.Verification.dll generator-oracle .
```

## Giới hạn phải giữ khi đọc result

- Full witnesses replay độc lập tới win. Prefix Found còn được so state classic giữa hai engine; hai prefix00115 mới cũng đã nối suffix tới cùng settled state và replay win.
- Beam/depth/time/first-event restrictions không thể chứng minh absence toàn game. NotFoundWithinBudget vẫn Unknown.
- EndpointSchedule vét ordered subsets của các actor/đích khai báo, mỗi actor tối đa một lần bằng shortest first-event path deterministic. ProvedAbsent chỉ trong scope đó; chỗ đỗ khác hoặc actor di chuyển nhiều lần chưa xét.
- Frozen access giữ nhóm khác đứng yên, dừng first consume; không chứng minh clear toàn hole hoặc full tail dễ.
- Footprint-shadow giữ cùng post-state/remaining tasks, chỉ cô lập geometry cục bộ. Tổng parking anchors không là capacity đỗ đồng thời.
- Actor graph SCC không là cycle completion hay nhãn khó. Các metric nhận thức và human quality vẫn chưa calibrated.

Input/catalog/solver/reference/analysis assembly hashes được lưu; state hash là snapshot-canonical-v1, arrays giữ engine order, object fields sort ordinal. Plan hash là canonical actor/start/path sequence, không hash annotation eaten. Không đồng nhất với format planHash cũ của access data: harness có phép tính riêng để so provenance cũ.

Benchmark/ranking/controlled variants lịch sử đã xóa theo xác nhận người dùng, không giữ ZIP. Không cần khôi phục chúng để tiếp tục prototype. Khi thay source, catalog, predicate hoặc successor scope, version và ghi provenance mới; không tái dùng kết luận absence cũ ngoài scope.
