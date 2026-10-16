# Đặc tả prototype tool tạo level — v0.1

Contract: `levelgen-prototype/0.1`. Trạng thái: chốt phạm vi/luồng/contract để bắt đầu implementation prototype nghiên cứu; **chưa implement generator**. Correctness theo [CORE](LEVELGEN_CORE.md), scope theo [PRESET](LEVELGEN_PRESET.md) revision `2026-10-15-bottleneck-2`. Bằng chứng và giới hạn trong [ORIGINAL_BOTTLENECKS](LEVELGEN_ORIGINAL_BOTTLENECKS.md), dữ liệu `research/causal-study.json`.

## 1. Kết quả cần tạo

Tool nhận cấu hình và seed, tạo candidate level mới đúng luật, có nghiệm replay độc lập, kèm giải thích điểm tắc/progression cho designer review. Không tự xuất nhãn Hard/Good từ tổng drag. Không yêu cầu preview hoặc lưu full lời giải; lời giải giữ trong memory để replay, lưu hashes/evidence riêng.

Hai phần tách biệt: core generation/analysis có contract ổn định và adapter gọi/UI sau này. Lần này chưa chốt nền tảng hoặc layout UI. Source solver production không chứa policy difficulty/quality.

Prototype hoàn thành khi sinh được candidate mới bằng cả hai intent bên dưới, vượt correctness gates và có báo cáo review. Một input đã có trong Levels được dùng làm regression/reference, không gọi bản sao đó là level mới. Chưa cam kết success rate, thời gian production hoặc tự đánh giá độ khó người chơi.

## 2. Phạm vi cố định và cấu hình

- Board7×8; single-layer ordinary hole/cat/box và wall thật có component không chạm viền.
- Chỉ colorId nguyên0–9 cho hole, board cat và queue cat. Supply đúng demand từng màu, demand mỗi hole bằng footprint. Cat không đặt trên hole kể cả cùng màu.
- Shape/định hướng phải là entry catalog; toàn footprint đúng bounds/collision. Không sinh mechanic khác hoặc tự diễn giải field chưa hiểu.
- Mặc định1–4box, trần4; box body/mouth/direction và queued metadata đúng luật. Không dùng mouth như một blocker riêng.
- Target tổng footprint25cell trên56tile; trả Matched/Deviation riêng, không âm thầm đổi target. Prototype mặc định chỉ chuyển candidate Matched tới vòng duyệt chuẩn; Deviation là lệch target, không tự là InvalidInput và phải report để designer quyết định hoặc repair theo config.
- Ưu tiên shape2–3cell, xen4–5cell, hạn chế1cell theo config đã resolve. Không tự biến preference thành tỷ lệ cứng.
- Màu được phép lặp. Nếu analyzer không xác định được consumer duy nhất khi nhiều hole cùng màu, ghi ambiguity/Unknown hoặc UnsupportedAnalysis cho phép đo đó; không gán toàn bộ supply cho một hole. Không loại input chỉ vì màu lặp khi gameplay hợp lệ.

Request bắt buộc có `requestVersion`, `seed`, `presetId/revision`, `intent`, catalog hash, `targetPolicy`, `operatorPolicy` và `budgets`. Resolve một lần trước run; lưu resolvedConfigHash. Budget phải có maxAttempts, maxRepairsPerAttempt, totalRunMs, perSolveMs và alternativeSearchScope/budget. Giá trị budget dùng để chạy thử được đặt trong config, không lấy8/12/20giây của nghiên cứu làm mặc định production. Thiếu budget/config cần thiết trả InvalidConfig; không tự chạy vô hạn.

## 3. BottleneckIntent — mục tiêu bị khóa, không ép lời giải

Intent gồm `id/version`, `pattern`, `blockedTasks/region`, `actorRoles`, `constraintRoles`, `candidateBreakTypes`, `expectedUnlockedTasks`, `continuationGoal`, `allowedAlternatives`, `protectedIntent` và `unknownPolicy`. Mỗi predicate phải có version và evaluator hỗ trợ; predicate không hỗ trợ được báo rõ, không giả đạt.

| Pattern v0.1 | Dựng cấu trúc | Evidence cần tìm |
|---|---|---|
| RegionRelease | Vùng source/mouth cần một footprint đi vào; một hoặc vài hole chiếm đường/chỗ đỗ; có không gian để chuẩn bị nhường chỗ | Consume/clear/reposition mở task thật; cùng remaining tasks có local causal effect; continuation dùng vùng vừa mở |
| QueueRevisitParking | Phân supply của actor giữa board/queue hoặc nhiều queue; actor có thể ăn một phần, giữ sống và rời mouth để phục vụ vai trò sau | Witness ăn queue khi còn demand, di chuyển/đỗ khi sống, tiêu thụ nguồn khác về sau; nêu geometry/queue constraints và đoạn thành quả |

00115 là reference cho RegionRelease với hai cách phá.00098/00129 cung cấp QueueRevisitParking và phối hợp geometry; cycle consumer chỉ dùng đề xuất motif, không là acceptance difficulty. Không bắt cả hai pattern xuất hiện trong một candidate. Không bắt hole lớn phải clear, completion đầu phải là nút chính hoặc nhiều nút mới là hay.

## 4. Cách dựng candidate ban đầu

Chọn pattern và role trước; sampling có seed, không random toàn board rồi tối ưu tổng drag:

1. Chọn vùng task/mouth và scaffold wall hợp P-WALL; khai báo vùng dự kiến cần giải tỏa và các chỗ đỗ hỗ trợ có thể dùng.
2. Chọn catalog shapes/anchors cho actor đích, blocker và support, kiểm placement tăng dần. Với QueueRevisitParking, gán các vai trò consume theo entry event, không theo completion DAG.
3. Chọn colorId trong0–9 và phân số token demand thành board/queue. Đặt một phần nguồn cần thiết vào các vùng phục vụ intent; không rải xa chỉ để tăng thao tác. Không đặt mèo lên hole/wall/box hoặc mèo khác.
4. Điền các hole/source còn lại tới target footprint/supply và số box đã resolve. Backtrack trong budget nếu geometry không fit, không nới rule.
5. Validate rồi solve/replay. Scaffold chỉ là giả thuyết; chỉ gameplay replay và analysis mới xác nhận có cách chơi thực.

Có thể import reference để chạy regression hoặc mutation study với lineage riêng; đường tạo candidate mới phải hỗ trợ dựng từ scaffold/intent, không chỉ sao chép level gốc rồi đổi ID/màu. V0 chưa dùng học máy, reverse solver hoặc tối ưu difficulty score.

## 5. Operator và transaction repair

| Operator | Bắt buộc kiểm lại |
|---|---|
| MoveHole | Anchor + toàn footprint bounds/collision; protected role và route/parking bị ảnh hưởng |
| ReplaceShapeSameArea | Catalog entry đúng; footprint mới fit; supply không đổi nhưng topology/intent phải phân tích lại |
| ReplaceShapeAndRebalance | Transaction rõ: đổi shape/anchor, thêm/bớt token cùng màu theo demand mới, cập nhật queue/board metadata, coverage; không sửa ngầm vật khác |
| MoveCat | Ô hợp lệ, không đè mọi footprint; đúng color/token lineage; xem có biến thành nguồn quá dễ hay không |
| MoveBox | Position/direction/mouth/body/queued coordinates đồng bộ; xem tác dụng pha phá tắc, không thưởng đường đi dài |
| EditQueueSupply | Thay thứ tự hoặc chia token board↔queue trong transaction, giữ tổng supply theo màu; kiểm consumer ambiguity |
| MoveWall | Đồng bộ obstacle aliases, deduplicate, giữ component wall thật giữa map và placement hợp lệ |
| Recolor | Map màu0–9 nhất quán cho hole và supply; đổi assignment phải khai báo compound, không gọi đổi tên màu là tăng khó |

Mỗi transaction có parentInputHash, operator/version, touchedEntities và reason từ failure/analysis. Commit chỉ khi static gates đạt; rollback nguyên state nếu thất bại. Sau mọi commit, solution/certification/analysis cũ hết hiệu lực; input hash mới phải được solve/replay lại. Không tự thay protected intent để làm attempt thành công; muốn đổi intent tạo request/lineage khác.

## 6. Luồng thực thi và trạng thái

Resolve config → construct → static validate → solve → independent replay → snapshot/causal analysis → targeted alternatives → review package → accept/repair/stop.

| Trạng thái | Xử lý |
|---|---|
| InvalidConfig / UnsupportedScope | Dừng với reason/field cụ thể; không tự bật mechanic |
| RuleFailure | Repair theo loại lỗi trước khi solve; không dùng score đẹp bù lỗi |
| TargetDeviation | Report tách validity; repair hoặc chuyển review ngoại lệ theo targetPolicy đã khai báo |
| Uncertified | Solver cancelled/budget/no plan trong phạm vi; không kết luận vô nghiệm; repair hoặc dừng theo budget |
| ReplayFailed | Candidate không được duyệt/export như playable; giữ failure và kiểm correctness/tooling |
| AnalysisUnknown | Chưa xác nhận intent/relief hoặc consumer ambiguity; không gán Easy/Hard; chuyển review hoặc repair theo unknownPolicy |
| ReviewRequired | Có full level đúng luật/certified và evidence package để designer đánh giá progression/quality |
| Approved / Rejected | Quyết định designer có rationale và evidenceRefs, không tự suy từ Solved |
| BudgetExhausted | Dừng sạch, trả attempts/failed cost và candidates hiện có cùng đúng trạng thái; không sinh file thành công giả |

Không có status “level vô nghiệm” từ timeout. Không dùng plan dài hơn làm tie-break chất lượng. Cùng seed/config/source cho phép tái lập sampling; wall-clock budget có thể thay điểm dừng nên không cam kết bit-for-bit cùng plan hoặc batch nếu môi trường khác. Log đủ để tái chạy attempt cụ thể.

## 7. Kiểm tự động và designer review

| Tự động chịu trách nhiệm | Designer chịu trách nhiệm |
|---|---|
| P-SIZE/SCOPE/COLOR/SHAPE/PLACEMENT/WALL/BOX; supply/demand và metadata | Có đọc ra vấn đề từ những gì game thực sự hiển thị không |
| Coverage Matched/Deviation | Cách phối hợp có ý nghĩa, hay chỉ làm việc thừa |
| Full solve + replay độc lập với final board/queue hết | Thành quả có rõ, nhịp kết thúc phù hợp không |
| Typed record + schema, provenance và trạng thái Unknown đúng nghĩa | Satisfaction, elegance, fairness; rationale thay vì điểm giả chính xác |
| Local footprint-release/reposition/consume evidence + continuation nếu adapter hỗ trợ | Có chấp nhận unknown scope/alternative chưa đầy đủ và coverage ngoại lệ không |
| Bounded alternative search, status/completeness scope, ghi cách phá khác tìm được | Độ khó cảm nhận và việc chọn candidate cho người chơi |

Review package phải cho thấy goal, constraint, support, các cách phá và phần chưa chứng minh. Không ép designer đọc raw plan hoặc counters dài; report tóm tắt theo task. Không yêu cầu mọi candidate có proof tối ưu hoặc global main bottleneck. Tuy nhiên được gọi Supported chỉ khi có causal evidence trong scope và continuation thực; thiếu thì Proposed/Unknown, không che đi.

Sự kiện clear cần post-state/shadow giữ cùng remaining-task set. Reposition cần kiểm trước/sau và task được mở; consume cần tách xóa mèo/tiến queue khỏi xóa footprint. Stage là role có thể chồng/lặp; Recognize là giả thuyết analyst, không tự suy trạng thái nhận thức người chơi.

Nếu search tìm cách phá dễ hơn/bỏ qua intent, lưu alternative; review lại intent/effect hoặc repair candidate. Không cấm solver đi đường đó để giữ difficulty. Chưa có calibrated score thì `difficultyScore=null`, `difficultyLabel=Unrated`; không xuất Hard tự động.

## 8. Output và export

Run trả summary + candidate records: resolved config/hash, seed/attempt, input/catalog/solver/reference hashes, operator lineage, validity failures, coverage status, solvability/replay, bottleneck records, search scopes/results, relief/continuation, work telemetry, Unknowns, designer decision và cost kể cả thất bại.

Full game JSON dùng tên `Levelxxxxx.json`, có đủ field hợp format hiện tại; metadata đặt riêng. Không chèn analysis vào gameplay JSON, không overwrite level gốc/ngầm tái dùng tên. Các candidate cần review có thể lưu bản draft tách khỏi output Approved và mang trạng thái ReviewRequired, không coi là đã phát hành.

Export Approved yêu cầu input hash đúng bản đã kiểm, correctness certified, target decision rõ, evidence/progression và designer review được ghi. API export không tự gửi/publish ra dịch vụ ngoài. Full solution/preview không lưu mặc định; planHash/provenance và endpoint/event summaries đủ cho audit, còn plan dùng trong memory để replay.

## 9. Acceptance khi bắt đầu implement

1. Static gates bắt được colorId ngoài0–9 ở cả hole/cat/queue, cat trên hole cùng màu, footprint không fit và supply lệch.
2. Operator hợp lệ/rollback và compound rebalance giữ đủ bounds/collision/supply; scope thay đổi không âm thầm.
3. Solve deadline/cancel không biến thành InvalidInput hoặc proved unsolvable; independent replay thất bại chặn duyệt.
4. Record/schema và Unknown guards chạy được trên cả ba references; input/mode/assembly hashes có thật.
5. RegionRelease nhận được cả clear và park-live, không gắn vào exact drag; QueueRevisitParking không nhầm consume với completion hoặc cycle với deadlock.
6. Mọi claim ProvedAbsent có complete scope; beam/depth/time pruning không được suy global necessity.
7. Có candidate mới từ mỗi pattern, có causal report/continuation hoặc Unknown được khai báo, designer review và JSON export đúng format; không cần quota batch/success rate trước khi có đo.
8. Mutation chỉ tăng transport không tự tăng quality/difficulty; rename màu hoặc mirror hợp semantics không tự được xem khó hơn vì solver đổi plan.

Mốc này là điều kiện nghiệm thu implementation prototype sắp tới, không phải tuyên bố tool đã tồn tại. Phần tiếp sau: đo success/failure/repair cost, review diversity, rồi pilot người chơi. Weighted score, band Hard, tự chọn Good/Bad và automation production phải chờ calibration/hold-out; không nằm trong contract v0.1.
