# Evaluator — xác định nút tắc trước khi đánh giá khó

Revision: `2026-10-15-bottleneck-1`. Đặc tả evaluator prototype; contract/typed guards, JSON schema và scoped research đã triển khai, cognitive metrics/score chưa triển khai. Nguồn hướng khó: [LEVELGEN_DIFFICULTY](LEVELGEN_DIFFICULTY.md); validity/preset theo [CORE](LEVELGEN_CORE.md), [preset](LEVELGEN_PRESET.md).

## 1. Phân lớp kết quả

| Lớp | Kết quả |
|---|---|
| Validity | Valid/RuleFailure theo scope, catalog, footprint, collision, supply/demand |
| Target | Matched/Deviation/Unknown theo coverage/resolved config; không đồng nhất InvalidInput |
| Solvability | Certified/Uncertified/ReplayFailed; cần replay độc lập |
| Bottleneck | Unlocated/Proposed/Supported; ghi goal, phase, ràng buộc và evidence |
| Difficulty | Chưa gán nếu bottleneck/effort chưa đủ evidence; không tự lấy tổng drag |
| Experience | Reference label và rubric review riêng; chưa tự suy Good/Bad |

Budget/cancel/NoNextCatReachable không chứng minh vô nghiệm. Chưa định vị nút không tự có nghĩa level dễ. Corpus gốc là good references của người dùng; variant không tự kế thừa nhãn đó.

## 2. Quy trình đánh giá mới

1. Kiểm input và resolved config/hash.
2. Solve/replay; giữ plan nguyên gốc trong bộ nhớ, không sửa để làm đẹp metric.
3. Đề xuất bottleneck candidates từ goal và tác dụng geometry, không xếp theo total drag hoặc largest area riêng lẻ.
4. Xác định trạng thái bắt đầu/kết thúc mỗi pha; lưu ràng buộc và task hỗ trợ.
5. Kiểm tác dụng giải tỏa trên cùng tập task còn lại và continuation thực.
6. Đo effort phục vụ phá tắc; phần chưa attribution được phải để Unknown.
7. Tách residual bottleneck khỏi transport/cleanup; so các plan và biến thể đúng pha.
8. Report evidence/giới hạn, sau đó mới review độ khó/trải nghiệm.

Không gộp thành weighted fun score hoặc đặt Hard threshold ở giai đoạn này.

## 3. Record cần có cho mỗi điểm tắc

| Field | Nội dung |
|---|---|
| goal/event | Nhiệm vụ/khả năng bị khóa; các sự kiện ăn/clear/dời vật có thể mở nó |
| phaseStart/phaseEnd | Trạng thái và event xác định ranh giới; không chỉ drag index |
| blockingConstraints | Shape, vùng hẹp, chỗ đỗ, nguồn supply, thứ tự hỗ trợ |
| supportingTasks | Các thao tác/task có evidence phục vụ goal |
| observedBreakEffort | Drag/steps được quan sát, actor/task liên quan; không ghi minimum khi chưa chứng minh |
| attributionStatus | Supported/Partial/Unknown; prefix có thể chứa unrelated work |
| reliefEvidence | Những capability/ràng buộc thay đổi trên remaining-task set tương ứng |
| continuation | Trực tiếp khai thác, còn phối hợp đáng kể, hay gặp bottleneck mới |
| alternativePlans | PlanHash/mode, cách phá tắc khác, phần bất định |

Không dùng prefix length dài nhất làm điểm khó. Nhiều actor hoặc nhiều switches chỉ mô tả work, chưa xác nhận cần thiết/cognitive difficulty.

## 4. Phép đo kỹ thuật được dùng với scope rõ

### First-event access

Giữ các nhóm khác cố định, dịch hole/group theo luật và dừng ở event ăn đầu tiên. Replay frontier lấy consumer/source và shortest grid steps trong scope đó. Phải giữ đúng mèo sai màu, wall, footprint và mechanic adapter.

Chỉ xác nhận event đầu tiên reachable, không chứng minh task clear hoặc toàn phần còn lại dễ. Source/actor count không là difficulty score.

### Tác dụng clear footprint

So post-state thực với cùng post-state có footprint vừa mất được giữ như blocker diagnostic. Cùng remaining-hole/supply set để tách geometry effect khỏi số task đã làm xong.

Ghi reachable/parking cho các hole còn lại, useful access mới, shortest access thay đổi và continuation. Aggregate endpoint count là diagnostic theo từng nhóm, không là capacity đỗ đồng thời. Một delta lớn chưa tự xác nhận nút chính; một large hole cũng chưa tự là bottleneck.

Cat-consumption và critical-reposition cần adapter đúng semantics riêng. Không thay mọi vật vừa biến mất bằng wall rồi coi kết quả luôn đúng. Với dời hole, đối chiếu trước/sau khi chỉ vị trí hole thay đổi và kiểm continuation; Level00115 đã có witness loại này. Chưa có adapter production cho các phép đo mới.

### Công việc trong/ngoài pha phá tắc

Segment non-eat có thể hỗ trợ goal, là transport, xử lý task khác hoặc do solver đi vòng. Không tự đưa tất cả vào difficulty. Tương tự, sau first clear vẫn có thể còn bottleneck khác.

Ghi coordination/breadth/switches và state repetition để giải thích, không cộng điểm theo số lượng. Cùng position chưa là cùng state nếu supply/demand/mechanics khác.

### Progress và remaining work

Consume demand, completion/layer, queue head và active footprint area vẫn ghi để mô tả timeline. Total drag, drag/tile, steps và cleanup cost là work telemetry, không score khó.

Cần kiểm liệu các task sau giải tỏa còn phải di dời/giải quyết ràng buộc lẫn nhau, hay chỉ đi tới nguồn và ăn. Khoảng cách lớn nhưng không có phối hợp mới không tự tăng difficulty.

## 5. So variant đúng task và đúng pha

Căn chỉnh goal/ràng buộc/break event giữa input, không so một phần trăm tổng plan. Queue index không là token identity: giữ source lineage, consumer và current container role. Recolor/size rebalance có thể làm một phần role không comparable; ghi rõ compound.

Nếu box move chỉ tăng transport/cleanup mà nút tương ứng không đổi, báo WorkIncreased, không Harder. Nếu ảnh hưởng supply/geometry của pha phá tắc, cần đánh giá lại pha đó. Chưa đủ attribution thì DifficultyUnknown, không chọn một kết luận thuận chỉ số.

Reflection/topology-equivalent input không tự khác độ khó vì heuristic thay plan length. Cùng planHash qua nhiều config không là evidence độc lập. Unobserved trong một trace không là impossible trong mọi solution.

## 6. Validity và provenance

ColorId của hole, mèo trên map và mèo trong box queue phải là số nguyên0–9 theo preset hiện tại; ngoài khoảng báo RuleFailure/P-COLOR, không tự remap hoặc mở dải mechanic. Catalog shape phải hợp lệ, kiểm toàn footprint/bounds/collision, cat cùng màu không được đặt trên hole. Đổi size cân demand/supply/coverage; dời wall/box đồng bộ aliases/mouth/queued metadata. Gate này không được bù bằng evidence progression đẹp.

Giữ seed/config/input/catalog/solver/oracle/plan hashes, mode/budget và failed cost. Default không cần full solution hoặc preview. Same-engine diagnostics không thay independent certification.

## 7. Acceptance trước khi mở lại nghiên cứu variant

- Không phân Hard từ total drag hoặc actor count.
- Không gán first clear/largest hole thành nút chính khi chưa có relief/continuation evidence.
- Phân biệt level có một nút khó rồi tail dễ với level dài vì đi lại.
- So cùng remaining-task set khi đánh giá footprint release.
- Tách đúng residual bottleneck và cleanup, ghi Unknown khi thiếu attribution.
- Box variant tăng total nhưng không đổi nút không được tự tăng difficulty.
- Giữ mọi result/no-solution-budget status và positive reference labels đúng phạm vi.

Dữ liệu nghiên cứu trước vẫn có giá trị validity/replay/local access, nhưng chưa thực hiện đầy đủ acceptance của mô hình bottleneck này. Tạm dừng mở rộng batch cho tới khi hướng khó được review.

Kế hoạch kiểm chứng và đầu ra trước mắt theo [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md); prototype implementation theo GENERATOR_DESIGN; không tự bật difficulty score production.

## 8. Hợp đồng dữ liệu nghiên cứu v0

Tích hợp từ [deep research](../deep-research-report.md), có điều chỉnh giới hạn tại [DIFFICULTY §9](LEVELGEN_DIFFICULTY.md#9-tích-hợp-deep-research-mô-hình-cần-kiểm-chứng). Contract đã có trong `research/ProgressionStudy/AnalysisContracts.cs` và `research/analysis.schema.json`; artifact `research/causal-study.json` đã qua typed guards và PowerShell Test-Json. Đây là harness nghiên cứu, chưa phải evaluator production.

### BottleneckRecord

Contract ID: `bottleneck-analysis/0.1`. Thay đổi semantics/predicate/equivalence phải đổi version; contract nghiên cứu đã implement, chưa là schema production cho mọi mechanic.

| Nhóm | Field bắt buộc/nội dung |
|---|---|
| Identity | schemaVersion, recordId, levelHash, catalogHash, analysisVersion |
| Phase | startStateHash, goalPredicateVersion, blockedTasks, affectedActors; goal tách khỏi một break event cụ thể |
| Constraints | id, type (Geometry/Parking/QueueOrder/Supply/Dependency/Reservation/Other), statement, evidenceRefs |
| Support | taskId, actor, reason, necessityStatus, evidenceRefs; Unknown khi chưa biết cần thiết |
| Alternatives | strategySignature, signatureVersion, eventType (Consume/Clear/Reposition/Composite), planHash, start/endStateHash, searchResultRef, replayStatus |
| Relief | diagnosticType/scope, sameRemainingTaskCheck, unlockedTasks, continuationRefs, residualTasks; không chỉ area delta |
| Evidence | evidence items có class, statement, artifact/hash, scope và limitations; không gán một confidence chung thay evidence từng relation |
| Unknowns | Những câu hỏi chưa giải đáp, task chưa attribution và missing metrics |

Evidence class: `RuleNecessary`, `ObservedWitness`, `LocalDiagnostic`, `Hypothesis`, `ScopedSearchNecessary`. Class cuối chỉ dùng khi đã exhaust đầy đủ một không gian hữu hạn có luật sinh trạng thái/equivalence và chứng cứ completeness; scope cục bộ không thành necessity toàn level.

State hash phải bao gồm positions, remaining board cats, remaining demand, queue consumed/head và các state field gameplay liên quan. Không hash chỉ hình học hoặc drag index. Mọi hash kèm version của serialization/canonicalization; không gộp khác state vì cùng hình board.

### SearchQuestion / SearchResult

Mỗi question phải khai báo inputHash, startStateHash và prefix provenance (hoặc initial state), goalPredicate, forbiddenEvents, continuationPredicate, scope, budget, maxDepth nếu có, depthUnit, successorGeneration, stateEquivalence và completenessMode.

| Kết quả | Ý nghĩa |
|---|---|
| Found | Có witness đạt đúng goal và continuation đã yêu cầu; ghi rõ partial/full và replay |
| NotFoundWithinBudget | Chưa có witness trước giới hạn; không suy absence/necessity |
| ProvedAbsentWithinCompleteScope | Exhaust phạm vi đã khai báo; không suy toàn level nếu start/depth/successors bị hạn chế |
| InvalidStartState | State/prefix không hợp lệ hoặc provenance không khớp |
| Unsupported | Chưa hỗ trợ mechanic, predicate hoặc semantics cần thử |

Nếu dừng vì depth limit, không được ghi toàn level vô nghiệm. Chỉ có thể nói không có witness trong depth đó nếu mọi nhánh trong phạm vi đã được xét; nếu còn budget-pruned frontier thì vẫn NotFoundWithinBudget. Lưu expanded, frontier còn lại, stopReason và completeness rationale.

Với Q115-A, tách goal `hai source h2 tiếp cận được` khỏi `h2 ăn cả hai` và `b2 đã tiến`. Nếu hỏi “trước clear h6”, cấm clear(h6) suốt prefix/continuation tới goal tương ứng, không chỉ kiểm h6 còn sống tại snapshot bắt đầu. Tìm từ snapshot muộn chỉ trả lời phạm vi snapshot đó.

### Strategy và phép đo

Phân loại macro theo actor, tác dụng consume/queue/clear/reposition và trạng thái sau có ý nghĩa. Semantic depth phải tính cả reposition có ảnh hưởng chiến lược; không bỏ toàn bộ non-eat khỏi search rồi tuyên bố đầy đủ. Nếu abstraction bỏ vị trí đỗ/đường đi thì cần chứng minh nó bảo toàn reachability cho claim completeness.

Báo riêng legal successors, plausible successors (policy/version cụ thể) và winning successors (kết quả search với scope). Không đếm chỉ các lựa chọn đã biết win rồi kết luận forcedness nhận thức. Số strategy đã tìm là lower bound cho lớp chữ ký đang dùng, chưa là uniqueness.

Output metric ghi value hoặc null, evidenceStatus, scope, extractorVersion, coverage và limitations. Không có calibration hợp lệ thì difficultyScore=null, label=Unrated; ExperienceQuality giữ riêng từng trục. Không dùng arbitrary coverage threshold để hợp thức hóa score thiếu inference/lookahead.

### Kiểm tra khi implement

Ưu tiên kiểm status/Unknown, consume≠clear, hash đủ state, replay witness và completeness bị giới hạn. Sau đó kiểm semantic equivalence: đổi ID không đổi quan hệ, path khác nhưng cùng full post-state không tạo strategy mới. Mirror chỉ là invariance khi shape, direction, queue/mouth và toàn bộ luật thực sự được biến đổi tương đương theo catalog; không coi mọi phép phản xạ đều hợp lệ.

## 9. Phạm vi implementation đã kiểm chứng

CausalStudy dùng source references, replayV42 độc lập và so projection toàn bộ state classic liên quan (hole id/color/remaining/position/finished, remaining board cats và queue consumed). Mọi full witness còn kiểm Solved và hết nguồn; hash snapshot dùng toàn object của product engine để không bỏ sót demand/queue state khi dedup.

TargetedSearch hỗ trợ classic single-layer/no links của ba reference. Beam dừng first-consume rồi tạo drag kế tiếp và có depth/time/beam restrictions; kết quả âm luôn NotFoundWithinBudget. EndpointSchedule chỉ vét ordered subsets của các actor/đích cố định, mỗi actor tối đa một action theo đường BFS deterministic; hoàn tất vét chỉ chứng minh absence trong đúng ngôn ngữ action đó. Nó không chứng minh tối ưu, uniqueness hoặc necessity toàn game.

Kết quả fact/proof giữ tách: RuleNecessary queue/demand; ObservedWitness live moves/alternative full win; LocalDiagnostic footprint-shadow; ScopedSearchNecessary finite endpoint ablation. Bounded search không được nâng thành global evidence. Meaningful branching/forcedness nhận thức, inference và lookahead vẫn chưa có extractor đã hiệu chỉnh.
