# Progression trong một level — tắc, phá tắc và giải tỏa

Revision: `2026-10-15-bottleneck-1`. Hướng khó chuẩn: [LEVELGEN_DIFFICULTY](LEVELGEN_DIFFICULTY.md). Scope/geometry theo [preset](LEVELGEN_PRESET.md).

## 1. Trải nghiệm cần thiết kế

**Nhận diện vấn đề → chuẩn bị/phá tắc → giải tỏa rõ rệt → khai thác thành quả → kết thúc hoặc đối mặt nút tiếp theo.**

Đây là vai trò gameplay, không phải các khoảng theo phần trăm tổng drag. Một level có thể chỉ có một bottleneck chính; clear hole footprint lớn làm phần còn lại dễ là một payoff tốt, không phải thất bại vì level hết khó sớm.

Mỗi level cần progression nội tại; không thiết kế progression giữa nhiều level ở đây. Tool phục vụ designer, không yêu cầu lưu preview/full solution.

## 2. Trước, tại và sau nút tắc

| Pha | Câu hỏi thiết kế |
|---|---|
| Trước/chuẩn bị | Mục tiêu nào đang bị khóa bởi geometry/supply? Người chơi cần hiểu và sắp xếp gì? |
| Phá tắc | Ăn/clear/reposition nào thay đổi ràng buộc quan trọng? |
| Giải tỏa | Khả năng tiếp cận/chỗ đỗ/công việc còn lại dễ hơn thế nào? |
| Khai thác | Người chơi sử dụng không gian/capability mới để hoàn tất task ra sao? |
| Nút tiếp theo nếu có | Có ràng buộc lớn độc lập còn lại, hay chỉ còn transport/cleanup? |

Không mặc định first clear, largest hole hoặc completion cuối là nút chính. Không gọi mọi queue head change là một bottleneck. Cần nhận diện tác dụng lên phần việc còn lại.

## 3. Progression intent phải mô tả gì?

1. Bottleneck candidate và điều kiện bắt đầu pha.
2. Ràng buộc đang cản ăn/clear và các role hole/cat/box/wall liên quan.
3. Công việc hỗ trợ dự định; phần supply hoặc vị trí cần đạt.
4. Sự kiện phá tắc.
5. Những task/shape còn lại dự kiến được giải phóng.
6. Cách khai thác thành quả; những residual bottleneck nếu có.
7. Biến thể lời giải được phép và phần evidence còn Unknown.

Intent không khóa exact move sequence/drag index. Nếu có cách phá cùng tắc đơn giản hơn thì cần ghi nhận; không ép solver đi đường dài để đáp ứng intent.

## 4. Vai trò geometry và supply

Footprint lớn chiếm một vùng quan trọng có thể là nút chính. Wall và các hole khác tạo lối qua/chỗ đỗ; supply quyết định việc nuôi/clear hole đó. Cần thiết kế chúng cùng nhau.

Partial consumption có thể phục vụ mở đường hoặc chuẩn bị clear; không tự tăng độ khó vì hole tồn tại lâu. Queue thứ tự theo consume events, không phải mọi màu trước phải clear hole rồi mới đến màu sau.

Sau giải tỏa, đường đi dài tới box không tự là một pha khó. Box chỉ đóng góp difficulty khi nó tạo hoặc tác động ràng buộc phá tắc thực sự. Chưa có evidence thì ghi work cost, không score khó.

## 5. Rubric review đề xuất

| Tiêu chí | Evidence cần xem | Giới hạn |
|---|---|---|
| Vấn đề có ý nghĩa | Ràng buộc/task rõ trước nút | Nhiều vật hoặc mật độ cao chưa đủ |
| Phá tắc có suy luận | Thứ tự hỗ trợ, phối hợp, cách tiếp cận mục tiêu | Nhiều actor/drag chưa chứng minh cần thiết |
| Giải tỏa có tác dụng | Same-remaining-task diagnostic và continuation dễ hơn | Cell trống/mobility tăng riêng lẻ chưa đủ |
| Thành quả rõ | Công việc sau dùng được phần vừa mở | Task đi xa hoặc nhiều consume không tự hay |
| Nhịp cuối phù hợp | Thu gọn tự nhiên; hoặc nút tiếp theo có lý do | Không cần giữ độ khó liên tục tới win |

Corpus Levels có positive label từ người dùng. Rubric chi tiết và nhãn variant chưa được review; không phủ nhận nhãn gốc. Validity/Certified không tự thành Good.

## 6. Những hướng đã rút

Không ưu tiên level bằng tổng drag/tile, late first-completion, non-eat ratio, breadth hoặc số handoff. Không kéo dài cleanup để làm level trông khó. Không bắt mỗi hole clear mở đường hoặc mọi drag lập tức tăng access.

Các relation cục bộ trong báo cáo cũ là dữ liệu phụ, chưa xác định được bottleneck chính. Một chuỗi ăn nối tiếp ở cuối có thể chỉ là phần thưởng sau khi bài toán đã được giải.

## 7. Thứ tự làm tiếp

Đã chạy scoped study00115 và đối chiếu00098/00129; [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md) ghi kết quả/giới hạn và bàn giao implementation prototype. [Phân tích00115](LEVELGEN_ORIGINAL_BOTTLENECKS.md) đã có hai cách mở cùng vùng mục tiêu: clear h4 hoặc dời h4 khi còn sống. Đặc tả prototype đã chốt tại GENERATOR_DESIGN; chưa implement generator hoặc mở batch mới.

## 8. Stage predicates và review trải nghiệm

Tích hợp hướng từ [deep research](../deep-research-report.md): phân pha theo thay đổi trạng thái, không theo phần trăm timeline. Đây là đặc tả stage classifier; harness đã có causal records/search, chưa có tự động phân loại đủ mọi stage hoặc cognitive model.

| Vai trò pha | Điều kiện/evidence để phân loại |
|---|---|
| Establish/Scan | Những thông tin geometry, màu, queue/mouth người chơi thực sự được nhìn thấy; có thể có consume hiển nhiên |
| Recognize | Analyst nêu goal bị khóa và giả thuyết phá; người chơi đã nhận ra hay chưa cần playtest, không suy từ snapshot |
| Prepare | Task có evidence góp phần đạt precondition của ít nhất một cách phá |
| Break | Consume/clear/reposition thay đổi ràng buộc của goal |
| Payoff | Continuation thực sự dùng capability vừa mở để hoàn thành task liên quan |
| Residual/Transfer | Còn nút có ràng buộc cần giải thích riêng; không suy có nút mới chỉ từ queue cycle hoặc non-eat |
| Cleanup/Finish | Đã kiểm các task còn lại chủ yếu thực thi/di chuyển/ăn trực tiếp; nếu chưa biết thì Unknown |

Relief validation là bước của analyzer, không là stage gameplay bắt người chơi thực hiện. Vai trò có thể lặp, xen kẽ hoặc chồng nhau giữa các task; không ép thành state machine tuyến tính duy nhất. Stage gate là predicate/version cùng evidence, không dùng `drag==81` hoặc yêu cầu mọi plan clear cùng hole.

Pattern `Expose → Practice → Combine → Test → Payoff → optional Transfer` có thể dùng như ý tưởng thiết kế trong một level. Chưa bắt mọi level đủ các bước; queue order/visibility thực tế phải cho phép người chơi đọc vấn đề. Nếu chưa kiểm UI có lộ toàn queue không, không tự kết luận việc lập kế hoạch tương lai là fair.

### Ba trục quality riêng

- **Satisfaction:** người chơi thấy được tác dụng của phá tắc và dùng thành quả đó.
- **Elegance:** có lời giải thích gọn cho sự phối hợp; constraint phục vụ ý tưởng thay vì thêm việc thừa.
- **Fairness:** thông tin cần thiết được cung cấp, hậu quả có thể suy ra và phản hồi giúp hiểu lỗi.

Có thể ghi review định tính kèm rationale; thang1–5 chỉ là đề xuất thử nghiệm, chưa có threshold gate. Không cộng quality vào difficulty. Unique strategy hoặc nhiều lựa chọn không tự là hay/dở.

Flag để review: clear không mở task có ý nghĩa, solver đi vòng, đoạn cuối dài chỉ do transport, hoặc solution duy nhất mà thiếu thông tin suy ra. Không tự reject level/corpus từ một diagnostic chưa đủ evidence.
