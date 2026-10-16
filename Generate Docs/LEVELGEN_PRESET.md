# Preset hiện hành — 7×8, wall và boxcat, tập trung điểm tắc

Preset ID: `wall-box-7x8-bottleneck`. Revision: `2026-10-15-bottleneck-2`.

Thay preset `wall-box-7x8-high-drag`. Tổng drag cao và drag/tile không còn là mục tiêu độ khó. Hướng chuẩn: [LEVELGEN_DIFFICULTY](LEVELGEN_DIFFICULTY.md). [CORE](LEVELGEN_CORE.md) giữ các bất biến gameplay.

## 1. Loại quy tắc

- **Bắt buộc:** candidate dùng preset này phải đạt, không tự nới để đủ batch.
- **Mặc định/preference:** khai báo rõ trong resolved config; không tự thành quality gate.
- **Telemetry:** mô tả dữ liệu, không chứng minh độ khó.
- **Chưa chốt:** không kế thừa threshold từ harness hoặc median corpus.

Đổi rule trước run tạo config/hash khác. Không đổi ngầm giữa run.

## 2. Phạm vi bắt buộc

| ID | Quy tắc |
|---|---|
| P-SIZE | Board 7×8, 56 tile |
| P-SCOPE | Hole/cat thường, boxcat thường và wall; single-layer; không mechanic bổ sung đang bật |
| P-COLOR | Hole, mèo trên map và mèo trong box queue chỉ dùng colorId nguyên0–9; ngoài khoảng là lỗi scope preset |
| P-SHAPE | holeType có trong shape catalog đúng hash; dùng đúng local footprint, không tự tạo polygon/xoay ngoài catalog |
| P-PLACEMENT | Toàn footprint trong map, không đè hole/cat/wall/box body; cat cùng màu cũng không đặt trên hole |
| P-WALL | Có component wall thật 4-neighbor trong map không chạm viền; không dùng box/broken-wall thay wall thật |
| P-BOX | Tối đa 4 box; không box lock hoặc queued-cat mechanic |
| P-CORRECT | Demand/supply, geometry và certification đúng CORE; Solved + replay độc lập |
| P-SOLVER | Source3.0.0 hiện tại; ghi source/assembly/catalog/oracle hashes thích hợp, không dùng artifact2.0 cũ |

Với board7×8, component giữa có mọi cell thỏa 0<x<6 và 0<y<7; không buộc đúng tâm hình học. Wall viền khác không bị cấm. Duplicate wall entries chỉ tính một grid cell.

Box ngoài board hợp lệ khi position/direction/mouth đúng gameplay; mouth trong map, không ở wall/box blocker. Box trong board là blocker. Mouth không phải vật riêng để cấm hole đi vào.

Field tắt như countdownMoveLimit=0 không đồng nghĩa mechanic bật. Field lạ chưa hiểu không tự coi vô hại. Dời box/wall phải đồng bộ queued metadata/aliases. Đổi size phải cân demand/supply và ghi coverage status; operator kết hợp có khai báo.

## 3. Mặc định bố trí

| Mục | Giá trị/hướng |
|---|---|
| Box | Mặc định1–4, trần4; đặt cách nhau, cân đối nhẹ; chưa có minimum distance/quota đối xứng |
| Shape | Ưu tiên2–3cell, xen4–5cell, hạn chế1cell; không có tỷ lệ cứng |
| Hole count | Theo footprint/capacity/geometry/intent; không bắt buộc10–15 |
| Màu | Chỉ0–9; hạn chế lặp khi phù hợp, cho phép dùng lại màu; không unique tuyệt đối hoặc threshold after8 |
| Wall count | Chưa có min/max cell ngoài P-WALL; không mặc định random1–2cell |
| Supply | Đúng từng màu; phân bố phục vụ cơ chế phá tắc, không rải xa chỉ để tăng work |

Các từ “cách nhau”, “cân đối nhẹ” chưa có threshold số. Giá trị thử nghiệm phải khai báo riêng.

## 4. Coverage

Giữ target45% diện tích board, không trừ wall: A=56, targetH=round(0,45×56)=25cell, coverage thực25/56≈44,64%.

H là tổng cell footprint hole chơi được ban đầu, placement không overlap. Single-layer H=25 có demand25 và supply25.

Builder mặc định nhắm25cell. `coverageTargetStatus`: H=25 là Matched; khác25 là Deviation khi chưa có tolerance khai báo. Deviation không tự là InvalidInput, cũng không ngầm coi đã đạt target. Tolerance/target khác phải được khai báo trước run; không nới để đủ batch.

## 5. Mục tiêu khó và progression

Mục tiêu nghiên cứu/trạng thái: [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md). Evidence mới không tự đổi rule bố trí của preset; revision hiện tại là `2026-10-15-bottleneck-2` sau khi bổ sung P-COLOR.

- Tập trung ràng buộc, thứ tự/phối hợp và effort phục vụ phá điểm tắc lớn; ăn, clear hoặc dời vật đều có thể là cách phá.
- Kiểm tra mức giải tỏa: hole còn lại di chuyển/ăn/clear dễ hơn sau sự kiện nào, vì sao?
- Một nút chính rồi phần cuối dễ có thể là progression đúng; không buộc nhiều nút hoặc giữ khó tới cuối.
- Tổng drag, total drag/tile, steps và cleanup cost chỉ là work telemetry, không score difficulty.
- Dời box làm phần dọn cuối dài hơn không được tự gọi khó hơn. Nếu tác động pha phá tắc thì cần evidence tại pha đó.
- Chưa chốt Hard threshold, weighted score hoặc minimum required drags. Không dùng thời gian/expanded của solver thay độ khó người chơi.

## 6. Không gate từ metric chưa chốt

R/fill chưa có định nghĩa canonical. Không gate R≥65%, fill>80% hoặc fill85–93%. Initial occupied-cell ratio là diagnostic riêng, không tự là R/fill chuẩn. Non-eat ratio, actor breadth, late-completion ratio và mobility count không phải quality score.

Production budgets, coverage tolerance, shape ratio, box spacing và difficulty thresholds còn chưa chốt; unset không đổi thành0 hoặc lấy12/20giây nghiên cứu.

## 7. Mẫu và cấu hình lịch sử

00129/00098/00115 vẫn là good references và fixtures, chưa xếp hạng lại theo điểm tắc. 00115 có footprint25cell; hai mẫu kia không tự nâng coverage preset.

High-observed-drag ranking, hole10–15, unique màu tuyệt đối, after8, random1–2wall và same-engine-only certification không còn là quy tắc chuẩn. Báo cáo/dữ liệu lịch sử đã dọn theo xác nhận người dùng; không làm nguồn định nghĩa khó.

## 8. Trạng thái sau tích hợp deep research

Lần tích hợp deep research không đổi rule bố trí. Sau đó bổ sung P-COLOR theo xác nhận người dùng: revision hiện tại là `2026-10-15-bottleneck-2`; bỏ hướng mở100–109 vì liên quan mechanic, không thuộc preset này. Công thức Difficulty Model v0, bands Intro…Extreme, rating1–5 và coverage threshold trong báo cáo mới chưa được bật làm default/gate. Hướng nghiên cứu và giới hạn metric ở [DIFFICULTY §9](LEVELGEN_DIFFICULTY.md#9-tích-hợp-deep-research-mô-hình-cần-kiểm-chứng); kế hoạch ở CURRENT_WORK.00098/00129 vẫn là reference có coverage deviation, không lấy footprint của chúng thay target25cell.
