# Level khó — điểm tắc và công việc phá tắc

Revision: `2026-10-15-bottleneck-1`.

**Hướng hiện hành theo chỉnh sửa của người dùng:** độ khó phải tập trung vào việc ăn/clear các điểm tắc lớn và mức suy nghĩ/phối hợp cần để làm được việc đó. Tổng drag tới win không đại diện đủ cho độ khó. Tài liệu này thay hướng dùng high observed total drag để chọn/xếp level khó trong các báo cáo cũ.

Chưa có công thức điểm hoặc ngưỡng Hard đã chốt. Contract và một số diagnostic/search nghiên cứu đã implement; cognitive metrics bên dưới vẫn cần kiểm chứng, không phải evaluator production đã hoàn thành.

## 1. Phân biệt khó, dài và thành quả

| Khái niệm | Câu hỏi |
|---|---|
| Độ khó phá tắc | Phải hiểu/sắp xếp/giải quyết ràng buộc nào để xử lý mục tiêu đang khóa map? |
| Công việc phá tắc | Những thao tác nào phục vụ việc ăn/clear mục tiêu đó? |
| Mức giải tỏa | Sau sự kiện đó, các hole còn lại dễ di chuyển/ăn/clear hơn bao nhiêu? |
| Chi phí phần còn lại | Sau khi ràng buộc chính đã hết, còn bao nhiêu việc đi lại và tiêu thụ để kết thúc? |

Một level có thể khó ở một nút chính rồi rất dễ về cuối. Level dài vì các hole phải đi xa đến box có thể vẫn dễ nếu không còn tranh chấp chỗ đỗ/thứ tự. Không nâng difficulty chỉ vì tăng drag hoặc steps ở phần này.

## 2. Điểm tắc lớn là gì?

Là mục tiêu/ràng buộc mà khi chưa xử lý, một phần đáng kể công việc còn lại bị hạn chế; khi xử lý, khả năng chơi thực sự được giải phóng và nhu cầu phối hợp giảm.

Trường hợp quan trọng theo người dùng: clear một hole footprint lớn làm map rộng hẳn, các hole khác ăn/clear dễ. Footprint là nguyên nhân hình học cần xét, nhưng **không mặc định hole nhiều cell nhất là điểm tắc chính**: còn vị trí, connectivity, shape của các hole còn lại và supply.

Không tự đồng nhất điểm tắc lớn với:

- Hole clear đầu tiên hoặc cuối cùng.
- Hole footprint lớn nhất, vùng trống lớn nhất hoặc state có mobility count cao nhất.
- Mỗi lần queue đổi đầu hoặc mỗi completion mở một source.
- Một đoạn có nhiều drag/non-eat/actor switches.

Sự kiện phá tắc có thể là clear hole, ăn mèo đang cản hoặc dời vật khỏi vị trí trọng yếu. Phân tích song song clear và dời hole: Level00115 đã có witness mở cùng vùng mục tiêu bằng cả hai cách. Mỗi loại cần semantics/diagnostic phù hợp; không ưu tiên một loại chỉ vì đã có phép đo sẵn.

## 3. Đánh giá độ khó trước và tại điểm tắc

Đối với mỗi bottleneck candidate cần trả lời:

1. Nhiệm vụ/khả năng đang bị khóa và trạng thái bắt đầu pha là gì? Ăn, clear hoặc dời vật nào là các cách mở dự kiến?
2. Điều gì ngăn tiếp cận/hoàn thành: footprint, lối qua, chỗ đỗ, supply, thứ tự phối hợp?
3. Những hole nào phải hỗ trợ, nhường chỗ hoặc được xử lý trước? Vì sao?
4. Phải nhìn trước và giữ thứ tự nào? Đâu là lựa chọn thực sự có ý nghĩa?
5. Có cách phá tắc khác đơn giản hơn không?
6. Sau phá tắc, những ràng buộc nào biến mất và còn nút lớn nào khác?

Drag cần được gắn với **nhiệm vụ phá tắc**, không chỉ cắt prefix rồi coi mọi drag trong prefix là cần thiết. Prefix có thể chứa xử lý sớm nhiệm vụ khác hoặc đường đi kém của solver.

`observedDragsToBreak` là chi phí quan sát của một plan và một pha đã xác định. Chưa có targeted search/chứng minh thì không gọi là minimum/necessary drags. Số actor hoặc số lần đổi actor chỉ mô tả phối hợp, không chứng minh khó suy luận.

Difficulty được mô tả theo mức khó của các bottleneck quan trọng, đặc biệt nút chi phối, và quan hệ giữa chúng. Không cộng toàn bộ cleanup vào difficulty. Một nút lớn có thể đủ; không buộc nhiều nút để tạo độ khó giả.

## 4. Xác nhận mức giải tỏa sau sự kiện

Phải đo trên những task/hole còn lại tương ứng, tránh nhầm việc ít nhiệm vụ hơn với việc ràng buộc đã giảm.

Với hole clear, so trạng thái sau clear thực với cùng post-state nhưng giữ footprint vừa mất như blocker diagnostic. Cùng tập hole/supply còn lại giúp tách tác dụng giải phóng geometry khỏi lượng nhiệm vụ đã hoàn thành.

Các evidence cần xem cùng nhau:

- Các hole còn lại có thêm vùng/anchor di chuyển hoặc đỗ hữu ích không?
- Có tiếp cận được mèo/mouth hoặc clear task mà không cần dời nhiều hole khác không?
- Nhu cầu chuẩn bị/cooperation có giảm trong các nhiệm vụ tiếp theo không?
- Phần tiếp theo là khai thác trực tiếp hay xuất hiện một bottleneck độc lập khác?

Một local access/mobility delta chưa đủ xác nhận map đã dễ. First-event access chỉ xét đến event ăn đầu tiên, không chứng minh các bước tiếp sau đều dễ. Cần kiểm tra continuation thực và các plan khác nhau.

Không dùng số cell được giải phóng làm difficulty score. Một hole nhỏ ở chokepoint có thể ảnh hưởng lớn; một hole lớn ở góc không quan trọng có thể ảnh hưởng ít.

## 5. Tách ba phần của lời giải

| Phần | Ghi nhận | Cách diễn giải |
|---|---|---|
| Chuẩn bị/phá tắc | Task, ràng buộc, dependency, observed drags và hỗ trợ | Trọng tâm phân tích độ khó; effort chưa tự thành cognitive difficulty |
| Sự kiện giải tỏa | Ăn/clear/reposition và tác dụng thực tế | Xác nhận điểm tắc và cảm giác thành quả |
| Khai thác/dọn phần còn lại | Task trực tiếp, transport, residual cooperation | Tổng work còn lại; chỉ phần có bottleneck mới mới được phân tích như difficulty tiếp |

Không mặc định mọi việc sau first clear là cleanup. Nếu vẫn còn ràng buộc lớn thì đó là pha khó tiếp. Cũng không coi mọi non-eat sau clear là khó: có thể chỉ là đặt lại vị trí để đi đến nguồn đã dễ tiếp cận.

## 6. Dời box và đổi footprint phải được đánh giá lại thế nào?

- Dời box chỉ làm đường lấy mèo/phần dọn cuối dài hơn, trong khi bottleneck không đổi: **tăng work/transport, chưa tăng difficulty**.
- Dời box làm supply cần để phá tắc khó tiếp cận hơn do ràng buộc mới: có thể ảnh hưởng difficulty, nhưng cần evidence tại pha phá tắc. Không khẳng định mọi box move chỉ là transport.
- Đổi shape/anchor có thể thay mức nghẽn hoặc cách giải phóng map. Đánh giá ảnh hưởng lên bottleneck và hậu quả sau clear, không chỉ tổng drag.
- Đổi size vẫn phải đúng catalog, bounds/collision, demand/supply và coverage status.
- Reflection giữ topology không được gọi khó hơn chỉ vì heuristic tạo plan dài hơn.

Rút kết luận cũ coi biến thể box90016 là ứng viên tăng độ khó từ việc tăng tổng drag. Dữ liệu solve/replay vẫn đúng; độ khó của biến thể đó hiện **chưa được đánh giá theo bottleneck**.

## 7. Phân tích lại dữ liệu hiện có: biết gì và chưa biết gì?

Các mẫu00129/00098/00115 từng được chọn theo total drag. Giữ chúng là good references do người dùng cung cấp và fixtures hợp scope, **chưa xác nhận chúng là ba mẫu khó nhất**.

Ví dụ00115 hybrid: first clear hole6 tại drag85, còn28drag và19non-eat. Vì vậy first clear không tự chứng minh map đã chuyển hẳn sang dễ. Sau clear hole4 (4cell) tại drag108, còn5drag, đều có ăn. Đây là dấu hiệu một mốc cần nghiên cứu, **chưa chứng minh hole4 là bottleneck chính** hoặc toàn bộ effort trước108 chỉ phục vụ hole4.

Improved có thứ tự clear khác: hole4 clear ở93/95. Không thể suy bottleneck từ exact drag index hay phần trăm timeline.

Chi tiết hữu ích cho phân tích lại00115:

| Quan sát | Có thể kết luận | Chưa thể kết luận |
|---|---|---|
| Hole4 (4cell, màu2) ăn từng phần ở hybrid6/65/82/108 | Công việc liên quan rải trong plan, không chỉ một segment cuối | Cả108drag đều cần để phá nút này |
| Sau clear hole5 tại97, có10non-eat rồi hole4 clear108 | Có một đoạn chuẩn bị trực tiếp trước goal được quan sát | Clear hole5 không có tác dụng hoặc đoạn10drag tối ưu |
| Clear hole4 tại108 mở2source cho hole2; shadow giữ footprint làm mất access; tail5drag đều ăn | Footprint release có tác dụng geometry và tail của trace này trực tiếp | Hole4 là main bottleneck của mọi cách giải |
| Improved đã clear nhiều hole trước hole4 | Có thứ tự xử lý khác; main bottleneck có thể là goal/nhóm khác | Cứ chọn nơi tail hết non-eat sẽ xác định đúng nút chính |

Phân tích cơ chế bước đầu đã có trong [Level00115 gốc](LEVELGEN_ORIGINAL_BOTTLENECKS.md): clear h4 hoặc dời h4 đều có thể mở hai mục tiêu cuối của h2 và chuỗi b2. Chưa chứng minh first clear h6 là bắt buộc. Công việc kế tiếp là kiểm chứng alternative và dependency, không thay total-drag bằng “drag tới first clear”.

Biến thể box90016 còn đổi hành vi solver trước các clear; trace cũ chưa căn chỉnh theo cùng bottleneck/task. Vì vậy chưa kết luận mức tăng drag nằm hoàn toàn trước hoặc sau điểm tắc thật. Điều chắc chắn cần rút là tổng drag không đủ chứng minh khó hơn.

## 8. Việc cần làm theo thứ tự

Kế hoạch/trạng thái tập trung tại [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md): kiểm chứng alternative trên00115 → sơ đồ phụ thuộc → đối chiếu00098/00129 → review và cập nhật đặc tả. Chưa mở batch biến thể hoặc khóa generator. Nguyên tắc đánh giá trong tài liệu này áp dụng cho mọi bước; không dùng prefix dài nhất, footprint lớn nhất hoặc tổng drag làm điểm khó.

## 9. Tích hợp deep research: mô hình cần kiểm chứng

Nguồn đề xuất: [deep-research-report](../deep-research-report.md). Đã đối chiếu cấu trúc ba level và telemetry cục bộ ngày 2026-10-15; sau đó đã chạy scoped targeted study trong `research/causal-study.json`; chưa hiệu chỉnh trên người chơi.

**Difficulty** và **ExperienceQuality** là hai trục riêng. Satisfaction (thành quả), elegance (sự gọn và rõ của ý tưởng), fairness (khả năng suy ra từ thông tin người chơi có) không cộng vào điểm khó. Relief xác nhận nút có ý nghĩa và phục vụ review trải nghiệm; relief lớn không tự tăng độ khó.

Các nhóm đặc trưng nghiên cứu được giữ, chưa thành score:

| Nhóm | Cần đo | Giới hạn trước khi dùng |
|---|---|---|
| Inference | Lập luận từ facts đến cách phá nút, theo tập luật công khai | Proof ngắn nhất trong rule set chưa là suy nghĩ tối thiểu của người chơi; phải version rule set |
| Relational complexity | Các ràng buộc cần kết hợp đồng thời | Không đếm tên family hoặc object rồi coi là cognitive complexity; cần kiểm ràng buộc có độc lập/thực sự cần không |
| Branching/forcedness | Lựa chọn chiến lược, lựa chọn có vẻ hợp lý nhưng sai, lựa chọn dẫn tới win | Tách legal, plausible theo policy và winning; biết đúng một winning successor chưa có nghĩa người chơi thấy nước đi forced |
| Lookahead | Độ sâu semantic cần để thấy hậu quả của lựa chọn trong scope | Độ sâu phát hiện thất bại của một search không chứng minh con người bắt buộc nhìn trước ngần ấy; có thể có lập luận khác |
| Alternative scarcity | Những cách phá khác nhau đã tìm được | Thiếu alternative trong budget không chứng minh unique; phải ghi phạm vi, equivalence và cost envelope |
| Coordination | Dependency, nhường chỗ, reservation và queue revisit có evidence | Không lấy actor count hoặc SCC count làm điểm khó; dependency dựa trên sự kiện, không chỉ ID hole |
| Heuristic divergence | Policy cục bộ bỏ qua cách phá tốt hoặc tạo việc thừa ra sao | Proxy nghiên cứu, không gọi là human model khi chưa có dữ liệu người chơi |

Hai đường đi chỉ được gộp khi khác biệt không đổi khả năng tiếp diễn trong scope đã nêu. Cùng actor, consumed set hoặc điểm cuối có cùng nhãn chưa đủ tương đương: vị trí các hole, demand còn lại, queue và chỗ đỗ có thể khác. Clear-h4 và reposition-h4 là hai witness theo cách phá, chưa phải chứng minh một phân hoạch strategy class đầy đủ toàn level.

### Không promotion công thức số lúc này

Báo cáo đề xuất trọng số I/L/R/B/C/A/H, quantile Q20–Q80, hệ số nút thứ hai và các band Intro…Extreme. Giữ chúng tại báo cáo gốc như engineering prior; **không đưa vào preset, UI default hay acceptance hiện hành**. Chưa có dataset người chơi để chọn trọng số hoặc chứng minh tính đơn điệu của các feature. Nếu Q20=Q80, normalization không được chia cho0; xử lý phải được khai báo trong calibration sau này.

Metric chưa có evidence là Unknown/null, không thay bằng0, không tự renormalize metric còn lại để xuất điểm chính thức. Ghi coverage theo từng metric và loại evidence trước khi có bất kỳ aggregate score. Ba level hiện tại vẫn Unrated về difficulty số.

### Hiệu chỉnh và giới hạn nguồn

Sau khi analyzer giải thích được các case, dùng playtest so sánh thứ tự level, cách giải người chơi, thời gian suy nghĩ tách đi lại khi có thể, hint/undo, perceived difficulty và ba trục quality. Dùng hold-out levels và kiểm counterexample: cleanup dài hơn không được tự thành khó hơn. Cỡ mẫu, weight và threshold được quyết định sau pilot.

Các citation web trong báo cáo là marker xuất từ phiên research (`turn…`), không có URL/DOI để truy ngược tại workspace. Chưa coi các phát biểu literature là bằng chứng đã xác minh. Tham chiếu `docs/SOLVER_ALGORITHM.md` cũng đã lỗi thời vì file đã được dọn. Quyết định hiện hành ở đây dựa vào source, JSON và replay của project, không dựa vào marker nguồn bên ngoài.

Kết quả bounded/scoped đã được dùng để chốt [prototype v0.1](LEVELGEN_GENERATOR_DESIGN.md), không để chốt difficulty score. Q115-A/C vẫn Unknown toàn cục; support necessity chỉ trong finite endpoint scope.00098/00129 có live queue/parking witnesses và local relief tái kiểm, chưa có global reasoning proof.
