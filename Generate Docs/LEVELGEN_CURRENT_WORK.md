# Trạng thái và bàn giao — đặc tả prototype đã chốt

Cập nhật: 2026-10-15. Đã hoàn tất mốc chuẩn hóa contract, thử nghiệm có phạm vi trên ba level gốc và chốt [đặc tả prototype v0.1](LEVELGEN_GENERATOR_DESIGN.md). **Chưa implement generator.** Các câu hỏi toàn cục chưa được chứng minh vẫn Unknown, không biến thành acceptance rule.

## 1. Mục tiêu sản phẩm và quyết định giữ nguyên

Tool phục vụ designer, tự tạo level hay/có nghiệm, progression trong từng level. Scope đầu tiên7×8, hole/cat/box thường, single-layer và wall thật giữa map. Hole và toàn bộ cat chỉ colorId nguyên0–9. Footprint đúng catalog/bounds/collision; supply theo màu bằng demand. Xem CORE/PRESET để có luật đầy đủ.

Khó tập trung vào ràng buộc và sự phối hợp để phá điểm tắc; tổng drag và cleanup không là điểm khó. Difficulty tách satisfaction/elegance/fairness. Corpus Levels là good references của người dùng, không bị tự xếp hạng lại bằng metric máy. Không yêu cầu lưu preview/full solution cho designer.

## 2. Bằng chứng hoàn thành mốc hiện tại

| Deliverable | Artifact/kiểm chứng | Giới hạn |
|---|---|---|
| Contract record/evidence/search | `research/ProgressionStudy/AnalysisContracts.cs`, `research/analysis.schema.json`, `research/causal-study.json` | Version bottleneck-analysis/0.1; chưa là analyzer production |
| Guards và schema | Typed tests: demand/queue đủ state hash, canonical properties, thiếu replay không Found, budget không thành absence; PowerShell Test-Json kiểm artifact/schema | Không suy mọi future adapter đã đúng |
| Oracle provenance | GeneratorOracle.info lấy algorithmVersion3.0.0 từ SolveResult; bỏ warm-up thiếu Level00001, info command chạy thành công | Causal harness dùng source references trực tiếp, không phụ thuộc JSONL bridge |
| Q115-A | NotFoundWithinBudget từ state trước clear h6, goal h2 ăn hai source, h6 phải sống | Chưa chứng minh prerequisite toàn level |
| Q115-B | Found một bước cuối h4; finite endpoint baseline Found;5 ablation ProvedAbsentWithinCompleteScope | Necessity chỉ trong tập endpoint/action đã khai báo, không là global proof |
| Q115-C | Hai bounded searches h7/h3 chưa tìm được witness sống/đỗ sau b2 | Giữ Unknown; nguồn cuối thì clear là bắt buộc theo demand |
| Cross-case | 3case/4record,6full solver witnesses independently replay; thêm2win continuations cho alternative00115;6shadow checks có continuation | Chỉ nút cục bộ/observed strategy, chưa có minimal inference/lookahead hoặc human rating |
| Prototype contract | GENERATOR_DESIGN: intent,construct,operator,repair,status,validation/replay,review/export và acceptance | Là đặc tả để bắt đầu code, không là tool đã tồn tại |

Artifact có input/catalog/solver/reference/analysis assembly hashes, search scope/budget/status và witness hashes. Không sửa level nguồn. Run mới có thể chạm wall-clock budget ở expanded khác; không dùng số đó làm difficulty.

## 3. Kết quả nghiên cứu và điều còn mở

Đọc [ORIGINAL_BOTTLENECKS](LEVELGEN_ORIGINAL_BOTTLENECKS.md) cho scope, trạng thái bắt đầu, các đích đỗ, câu hỏi và bảng so ba level. Tri thức dùng cho prototype:

- Goal bị khóa quan trọng hơn ID hole được clear;00115 mở cùng vùng bằng clear hoặc reposition.
- Nhường chỗ/parking có thể có tác dụng ngay cả khi không ăn; actor order có thể có phương án hoán đổi hợp lệ.
- Consume queue không đòi completion; nếu còn supply khác, hole có thể rời mouth khi sống. Completion cuối cùng vẫn bị demand quyết định.
- Queue cycle theo ID actor không là deadlock hay proof cognitive difficulty.00098/00129 có cả geometry release và queue revisit thực.
- Shadow/first-event access chỉ là causal evidence cục bộ; chưa tự xác định main bottleneck, minimum effort hoặc toàn bộ tail dễ.

Q115-A/C còn Unknown toàn cục; support actors chưa được chứng minh cần trên mọi chỗ đỗ/thứ tự. Các giới hạn được khai báo là đầu ra hợp lệ của nghiên cứu bounded, không phải lý do dùng total drag thay vào. Prototype không được hard-code các hypothesis này.

## 4. Việc tiếp theo khi bắt đầu implementation prototype

1. Implement config/seed/preset validator và transactions placement/supply theo đặc tả; chưa thêm metric score.
2. Implement constructor theo RegionRelease và QueueRevisitParking; nhận role/intent, dựng scaffold và điền đủ25cell/supply.
3. Nối source solve + independent replay, snapshot/evidence/alternative search, rồi cause-directed repair có budget.
4. Tạo report ngắn cho designer; Approved đòi correctness, target decision và review có rationale. Không tự gán Hard/Good.
5. Regression trên ba references và controlled invalid inputs; sinh candidate mới từ mỗi pattern, đo failed cost/repair và review thủ công trước khi bàn tự động hóa thêm.

Đây là backlog **ngoài mốc chốt đặc tả vừa hoàn tất**, không tự bắt đầu generator trong goal nghiên cứu này. Chưa chốt platform/layout UI, budget production, quality thresholds hoặc calibrated difficulty score. Budget prototype là config được resolve, không kế thừa số đo research.

## 5. Hướng nghiên cứu sau prototype

Pilot người chơi để kiểm khả năng đọc thông tin, chiến lược thực, perceived difficulty, satisfaction/elegance/fairness; dùng hold-out trước khi score/Hard gate production. Metric inference/branching/lookahead/heuristic-gap là candidate nghiên cứu trong DIFFICULTY, không yêu cầu xây hết trước prototype có designer review.

Nếu tiếp tục Q115-A/C, dùng scope rộng hơn/heuristic khác và lưu result mới có provenance; không sửa nhãn cũ thành impossible. Muốn dùng variant cũ phải tái tạo có lineage vì dữ liệu lịch sử đã xóa theo xác nhận người dùng.

## 6. Quy tắc bàn giao

Đọc README → tài liệu này → GENERATOR_DESIGN → ORIGINAL_BOTTLENECKS. Cách build/chạy/validate ở [research/README](research/README.md). Contract và semantics nguồn chuẩn ở EVALUATOR; điều chỉnh scope mới phải version/ghi Unknown đúng nghĩa.

Record/source summaries chỉ để giải thích và tái lập; không thay source correctness hoặc yêu cầu đọc toàn chat. Giữ nguyên level gốc và không sửa plan để đạt chỉ số. Goal hiện tại kết thúc ở đặc tả, chưa ở implementation/sinh batch mới.
