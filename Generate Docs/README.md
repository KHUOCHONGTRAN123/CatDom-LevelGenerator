# Tài liệu thiết kế tool tạo level

Cập nhật: 2026-10-15. Điểm bắt đầu cho người tiếp nhận project, không cần đọc lịch sử chat.

## Mục tiêu và trạng thái

Xây tool phục vụ designer để tạo level hay, hấp dẫn và có nghiệm, dựa trên luật và khả năng kiểm chứng của solver trong project. Làm rõ thiết kế trước khi code generator. Progression được xét trong từng level.

Ưu tiên hiện tại: 7×8, hole/cat/box thường, single-layer và có wall thật giữa map. Corpus `Levels/` là các level hay do người dùng cung cấp; chưa xếp hạng khó theo hướng mới.

Khó cần được giải thích bằng ràng buộc và sự phối hợp để phá điểm tắc có ý nghĩa, cùng tác dụng giải tỏa. Tổng drag và đi lại dài không đại diện độ khó. Một nút khó rồi phần cuối dễ là progression hợp lệ. Chưa chốt công thức Hard hoặc điểm hay.

**Đã chốt đặc tả prototype v0.1; chưa triển khai generator.** Contract/typed guards/schema đã có, thử nghiệm scoped00115 và đối chiếu00098/00129 đã chạy. Các câu hỏi global chưa chứng minh vẫn Unknown. Việc tiếp theo là implementation theo GENERATOR_DESIGN, không mở thêm nghiên cứu chỉ số trước prototype.

## Thứ tự đọc và nguồn chuẩn

1. [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md): mục tiêu hiện tại, việc đã xong, câu hỏi, đầu ra và điều kiện chuyển bước.
2. [CORE](LEVELGEN_CORE.md): yêu cầu sản phẩm và bất biến gameplay.
3. [PRESET](LEVELGEN_PRESET.md): scope, geometry, mặc định và các cấu hình chưa chốt. Preset hiện hành: `wall-box-7x8-bottleneck`.
4. [DIFFICULTY](LEVELGEN_DIFFICULTY.md): cách hiểu điểm tắc, effort phá tắc và giới hạn suy luận.
5. [PROGRESSION](LEVELGEN_PROGRESSION.md): nhịp nhận diện vấn đề → phá tắc → thành quả.
6. [ORIGINAL_BOTTLENECKS](LEVELGEN_ORIGINAL_BOTTLENECKS.md): bằng chứng Level00115 và hai cách mở cùng vùng mục tiêu.
7. [EVALUATOR](LEVELGEN_EVALUATOR.md): contract/guards/schema và scoped research đã có; đặc tả evaluator prototype, chưa có cognitive score production.
8. [GENERATOR_DESIGN](LEVELGEN_GENERATOR_DESIGN.md): đặc tả prototype v0.1 đã chốt để bắt đầu code, chưa phải implementation.

Mỗi file là nguồn chuẩn cho phạm vi ghi trên. Báo cáo quan sát không tự thay rule preset; dữ liệu solver không tự chứng minh độ khó người chơi. Khi có mâu thuẫn, sửa file nguồn chuẩn và tham chiếu liên quan, không suy quyết định từ report cũ.

## Nghiên cứu hiện tại và lịch sử

[research/README](research/README.md) mô tả dữ liệu giữ lại và cách chạy lại phân tích điểm tắc. Giữ access-focus JSON, snapshot00115, causal-study JSON/schema và harness cần dùng.

Năm report cũ về corpus/high-drag/access/robustness/variants cùng dữ liệu/script lịch sử đã rút khỏi thư mục làm việc. Đã xóa theo xác nhận của người dùng, không giữ ZIP; nghiên cứu solver đã có cache riêng của người dùng. Các kết luận dùng tổng drag, late completion hoặc số handoff làm đại diện độ khó đã rút; không khôi phục chúng thành policy hiện hành.

## Tích hợp deep research ngày2026-10-15

[deep-research-report.md](../deep-research-report.md) là báo cáo đầu vào, giữ nguyên để đối chiếu; không tự là nguồn rule sản phẩm. Phần được chọn lọc đã tích hợp vào docs hiện hành, không nhân thêm bộ tài liệu/template trùng lặp.

| Nội dung báo cáo | Quyết định và nơi tích hợp |
|---|---|
| Tách difficulty và quality | Dùng ngay về khái niệm; DIFFICULTY, PROGRESSION, CORE |
| Record/evidence/search statuses | Contract0.1/typed guards/JSON schema đã implement và chạy, mô tả tại EVALUATOR §8 |
| Queue graph và telemetry ba level | Tính lại khớp JSON/access data; bổ sung ORIGINAL_BOTTLENECKS, chưa là nhãn khó |
| Stage theo state, payoff và fairness | Tích hợp PROGRESSION; không ép stage tuần tự hoặc suy người chơi đã nhận ra từ snapshot |
| Inference/lookahead/branching/uniqueness | Backlog nghiên cứu có giới hạn; DIFFICULTY §9, chưa có cognitive extractor/calibration |
| Trọng số, band Hard và threshold | Chưa nhận làm mặc định hoặc gate; giữ ở báo cáo gốc như proposal |
| Oracle sai version/fixture | Đã sửa metadata từ runtime/source và bỏ warm-up fixture thiếu; info test đạt |
| File layout và mọi metric phải xong trước generator | Chưa tạo project/folder mới; phân biệt prototype nghiên cứu với score/gate production |
| Literature và trích dẫn | Marker `turn…` không có URL/DOI; chưa xác minh và không dùng làm evidence chuẩn |

Điểm chỉnh quan trọng: cycle trên graph consumer không là cycle sự kiện hoặc completion dependency; một winning successor chưa là forced move với người chơi; muốn giữ hole sống sau consume phải còn demand. Roadmap cụ thể nằm trong CURRENT_WORK.

Bàn giao mới: [causal-study](research/causal-study.json) có3case/4record/10search results;6full witnesses và2alternative continuations independently replay. Chi tiết giới hạn tại ORIGINAL_BOTTLENECKS, contract/acceptance prototype tại GENERATOR_DESIGN. Không diễn giải bounded NotFound thành global necessity.
