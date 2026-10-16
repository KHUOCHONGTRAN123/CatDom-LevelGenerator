# Yêu cầu sản phẩm và bất biến gameplay

Trạng thái: yêu cầu nền hiện hành; chi tiết difficulty/quality còn cần nghiên cứu và review. Mục tiêu/trạng thái công việc: [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md).

## Sản phẩm

- Tự tạo level mới có nghiệm; custom được mechanic, size và độ khó.
- Ưu tiên gameplay hay. Độ khó và chất lượng trải nghiệm là hai trục riêng; satisfaction/elegance/fairness không cộng vào difficulty để bù điểm.
- Độ khó tập trung vào ràng buộc/công việc để ăn hoặc clear điểm tắc lớn và mức giải tỏa sau đó; không dùng tổng drag hoặc đoạn đi lại sau phá tắc làm đại diện độ khó.
- Tool phục vụ designer; không yêu cầu lưu preview hoặc full lời giải cho designer. Metadata/audit tái hiện và kiểm chứng vẫn lưu riêng theo yêu cầu bên dưới.
- Progression diễn ra trong từng level; mọi level cần được kiểm progression theo đặc tả prototype và review, không phải progression giữa nhiều level.
- Setting thiết kế có thể đổi giá trị/bật tắt trên UI sau này; preset chỉ là mặc định.

## ColorId cho preset hiện tại

- **ColorId của hole và cat phải là số nguyên từ0 đến9, bao gồm hai đầu.** Áp dụng cả mèo trên map và mèo trong queue box; nguồn mèo phải khớp màu demand của hole.
- Các colorId ngoài khoảng này liên quan mechanic, không dùng trong preset hiện tại. Không mở thêm dải màu khi hết10màu; nếu cần thêm hole thì dùng lại màu trong0–9 và giữ đúng supply/demand.
- Quy tắc này giới hạn preset không mechanic hiện tại, không thay luật hỗ trợ mechanic của solver hoặc chỉnh lại corpus gốc.

## Gameplay và dữ liệu

- Hole, mèo, wall và box có tọa độ/footprint hợp lệ, không va chạm trái luật.
- **Không đặt mèo trên footprint bất kỳ hole, kể cả cùng màu.** Mèo không chồng nhau hoặc blocker.
- **Demand mỗi lớp hole bằng số cell footprint lớp đó.** Tổng supply đúng màu trên board + queue bằng demand.
- Chỉ mechanic được bật mới được phép tồn tại.
- Có nghiệm phải được solver giải hoàn chỉnh và replay độc lập đúng gameplay. Budget hết/cancelled không là chứng nhận nghiệm hoặc kết luận vô nghiệm.
- Có thể thêm diagnostic/sửa solver để lấy dữ liệu, nhưng phải giữ tính đúng. Không sửa plan, chèn vòng lặp hay thêm nước giả để đạt chỉ số.

## Tài nguyên và vận hành

- Tạo/sửa có kế hoạch, dùng dữ liệu solver để biết sai ở đâu để sửa.
- Đo chi phí sửa, solve và thời gian, tính cả thất bại.
- Không nới rule đã bật để đủ batch. Giữ seed/config/trace/audit để tái hiện và review.
- Xuất full JSON tên `Levelxxxxx.json`; metadata nghiên cứu lưu riêng.

Nguồn chuẩn đi cùng: [preset hiện tại](LEVELGEN_PRESET.md).

Nguồn hướng khó hiện hành: [difficulty](LEVELGEN_DIFFICULTY.md). Nguồn đặc tả: [progression](LEVELGEN_PROGRESSION.md), [evaluator](LEVELGEN_EVALUATOR.md), [generator](LEVELGEN_GENERATOR_DESIGN.md). Đã có scoped causal research và đặc tả prototype v0.1 để bắt đầu implementation; chưa có calibrated threshold hoặc công thức difficulty.
