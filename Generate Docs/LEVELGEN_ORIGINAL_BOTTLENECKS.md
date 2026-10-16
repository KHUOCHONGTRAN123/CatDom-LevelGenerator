# Cơ chế tắc và cách phá tắc — Level00115 gốc

Ngày phân tích: 2026-10-15. Nguồn: `Levels/Level00115.json`, không phải biến thể. Hole/box đánh số từ 0 theo thứ tự JSON; tọa độ theo dữ liệu, y tăng lên trên. Màu dùng colorId, không suy tên màu từ số.

Trạng thái: đã bổ sung bounded targeted search, finite endpoint schedules và đối chiếu ba level gốc; chưa chứng minh dependency toàn cục/tối ưu. Kết quả chi tiết bên dưới, artifact `research/causal-study.json`; trạng thái bàn giao tại [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md).

## Kết luận chính

Level này có **tắc hình học do các footprint tranh đường và chỗ đỗ**, kết hợp **tắc thứ tự cấp mèo tại miệng box**. Phá tắc có ít nhất hai cách đã quan sát: clear hole đang chiếm đường, hoặc chuyển hole đó sang chỗ đỗ để mở cùng mục tiêu mà chưa clear. Sau đó có chuỗi ăn/clear liên tiếp đem lại thành quả rõ ràng.

Không thể định nghĩa điểm tắc của level là “phải clear hole 4 ô”. Một lời giải đã mở chuỗi phía dưới trong khi cả hai hole 4 ô còn tồn tại. Cũng chưa đủ bằng chứng để gán một main bottleneck duy nhất cho toàn level hoặc nói số drag quan sát là chi phí tối thiểu.

## Các vật tạo ràng buộc

| Hole | Màu | Footprint | Nguồn cần ăn để clear |
|---|---:|---|---|
| h0 | 6 | thanh đứng 3 ô | mèo trên map + b1 đầu hàng + b3 sau màu 0 |
| h1 | 3 | thanh đứng 3 ô | mèo trên map + b0 đầu hàng + b2 sau màu 5,7 |
| h2 | 4 | L 3 ô, type16 | ba mèo trên map: (2,7), (4,0), (6,0) |
| h3 | 7 | thanh ngang 2 ô | mèo (0,6) + b2 sau màu 5 |
| h4 | 2 | T 4 ô, type17 | bốn mèo trên map: (1,0), (5,0), (1,7), (5,7) |
| h5 | 8 | T 4 ô, type19 | hai mèo trên map + b0 sau màu 3 + b1 sau màu 6 |
| h6 | 0 | L 3 ô, type13 | mèo (4,5), (0,7) + b3 đầu hàng |
| h7 | 5 | L 3 ô, type16 | mèo (6,7), (2,0) + b2 đầu hàng |

Footprint không xoay tùy ý. h4 và h5 cùng 4 ô nhưng hướng và hình khác nhau. Tổng footprint ban đầu 25 ô; còn 16 mèo trên map và 4 ô wall duy nhất. Chỉ có 11 ô chưa bị các vật này chiếm lúc đầu, nhưng số ô trống không cho biết một footprint có đi/đỗ được hay không.

Wall ở (3,0), (3,1), (3,4), (3,7), làm các hàng băng qua cột giữa bị chia thành những khoảng có hình dạng cụ thể. Wall giữa map là (3,4). Các entry trùng trong JSON chỉ tính một lần. Trong kiểm tra bỏ riêng wall giữa map tại các mốc đang xét, không xuất hiện thêm mục tiêu ăn đầu tiên; vì vậy chưa có căn cứ gọi riêng wall này là nguyên nhân chính. Phải xét cùng footprint, mèo và vị trí các hole khác.

## 1. Tắc do thiếu đường đi và chỗ đỗ phù hợp footprint

Ban đầu, khi giữ các hole khác đứng yên, chỉ h0 ăn được mèo (0,3), h1 ăn được (6,4), h5 ăn được (2,4). Ăn những mèo này xóa chướng ngại cục bộ nhưng chưa clear hole nào. Đây là bước mở không gian ban đầu, không phải bằng chứng ba lượt đó đã giải tỏa toàn map.

Các hole còn cần mèo ở xa, hai phía map hoặc hai biên đối diện. Ví dụ h4 phải ăn bốn mèo ở gần bốn góc; h6 cần đi từ phía phải trên sang trái trên rồi miệng box trái dưới. Đường cho một ô đi được chưa chắc cho thanh đứng, L hoặc T đi được. Một hole đã gần đủ mèo vẫn phải nhường đường và tìm chỗ đỗ cho hole khác.

**Cách phá đã quan sát:** ăn mèo cản đường, chuyển hole hỗ trợ sang chỗ đỗ tạm, chuyển hole đích qua vùng vừa mở, rồi tiếp tục sắp xếp. Không phải mọi drag không ăn đều là suy luận cần thiết: chỉ ghi nhận đoạn nào thực sự mở mục tiêu và chuỗi sau nó.

## 2. Nút cụ thể: vùng dưới phải, mèo màu 4 và miệng b2

Hai mèo màu 4 ở (4,0), (6,0) là hai mục tiêu cuối của h2 sau khi đã ăn mèo (2,7). Vùng này cũng dẫn tới b2 có miệng (5,0). h4 đỗ ở đây có thể chiếm đúng không gian h2 cần dùng.

### Cách A — clear h4 để mở đường

Trong lời giải hybrid, sau khi đã clear h6, h0, h5, vẫn cần đoạn sắp xếp 98–107: h2 về phía trái dưới, h3 lên biên trên, h7 đổi chỗ đỗ, h1 đi lên và h4 vòng về trái. Ở drag108, h4 ăn mèo màu 2 cuối tại (5,0) rồi clear, điểm neo cuối (4,0).

Ngay sau đó, h2 tiếp cận được cả hai mèo màu 4 ở (4,0), (6,0). Kiểm tra giữ footprint h4 tại vị trí clear, trong cùng trạng thái mèo/box còn lại, chặn cả hai mục tiêu này. Đây là bằng chứng hình học cục bộ cho tác dụng giải tỏa của việc bỏ footprint h4.

Thành quả: 109–110 h2 ăn hai mèo và clear; 111 h7 ăn b2 đầu hàng và clear; 112 h3 ăn tiếp và clear; 113 h1 ăn tiếp và clear. Năm drag cuối đều ăn, không có lượt chỉ sắp xếp. Đoạn 98–108 là một witness chuẩn bị/phá tắc; chưa chứng minh cả 11 drag đều cần thiết hoặc tối thiểu.

### Cách B — đỗ h4 chỗ khác, chưa cần clear

Trong lời giải improved, sau clear h6 ở74, các drag76–80 sắp xếp h0, h7, h2, h3, h5. h2 được đưa từ (1,6) tới (4,5); h3 đỗ (3,6); h5 đỗ (1,5).

Drag81 chuyển h4 từ điểm neo (4,0) sang (0,3), không ăn và không clear. Trước bước này h2 chưa tiếp cận được hai mục tiêu trên khi giữ các hole khác đứng yên; sau bước này cả hai xuất hiện trong tập mục tiêu tiếp cận. Vì bước81 chỉ thay vị trí h4, đây là witness trực tiếp cho cách phá tắc bằng **nhường không gian**, không cần xóa hole.

Drag82–83 h2 ăn ở (4,0), (6,0) rồi clear; 84 h7 ăn/clear tại b2; 85 h3 ăn/clear; 86 h1 ăn tiếp tại b2 nhưng chưa clear. Lúc chuỗi này mở, h4 và h5 vẫn còn trên map; h4 tận93 mới clear, h5 cuối cùng95 mới clear.

**Điều cần học:** mục tiêu là mở không gian cho h2 và chuỗi b2; “clear h4” chỉ là một phương án thực hiện. Đánh giá chỉ thưởng clear hole lớn sẽ bỏ sót phương án B.

## 3. Tắc thứ tự nguồn mèo tại box, kết hợp chiếm miệng

| Box, miệng | Thứ tự màu | Thứ tự hole tiêu thụ bắt buộc trong box này |
|---|---|---|
| b3, (1,0) | 0 → 6 | h6 → h0 |
| b2, (5,0) | 5 → 7 → 3 | h7 → h3 → h1 |
| b1, (1,7) | 6 → 8 | h0 → h5 |
| b0, (5,7) | 3 → 8 | h1 → h5 |

Thứ tự cấp mèo là ràng buộc cứng. Nhưng **không bắt buộc hole trước phải clear**: chỉ cần ăn phần tử đầu là hàng đợi tiến. Ví dụ improved75: h0 ăn màu6 ở b3 nhưng chưa clear; improved86: h1 ăn màu3 ở b2 nhưng còn phải tới b0. Không được biến chuỗi hàng đợi thành dependency clear sai.

Ngoài thứ tự màu còn có footprint chiếm chỗ quanh miệng. Sau khi nguồn đầu đã được ăn, hole kế tiếp có thể vẫn không vào được nếu hole trước hoặc hole khác chắn đường. Trong các witness:

- h6 ăn đầu b3 và clear ở hybrid85/improved74; h0 ăn màu6 kế tiếp ngay lượt sau. Giữ footprint h6 sau clear chặn tiếp cận màu6 tại trạng thái đó: vừa có mở hàng đợi vừa có nhường không gian.
- h2 clear ở hybrid110/improved83 mở tiếp cận b2 cho h7. Giữ footprint h2 sau clear làm mất mục tiêu này.
- h7 rồi h3 ăn/clear liên tiếp tại b2; ở từng trạng thái, giữ footprint vừa clear cũng chặn mục tiêu kế tiếp. Chuỗi thành quả có cả hai yếu tố: đúng thứ tự nguồn và giải phóng vùng miệng.

**Các cách phá:** ăn đầu hàng dù chưa clear; chuyển hole vừa ăn ra chỗ đỗ nếu còn sống; hoặc hoàn tất hole tại miệng để tự giải phóng footprint. Hai cách đầu là cơ chế hợp lệ; witness rõ trong nghiên cứu này cho ăn chưa clear là h0/improved75 và h1/improved86. Chưa thực nghiệm phương án giữ sống rồi đỗ thay cho từng bước clear của chuỗi b2.

## 4. Những điểm chưa thể kết luận

Hai lời giải đều clear h6 trước, nhưng chưa chứng minh h6 là first clear bắt buộc. Chưa chứng minh h4 phải được di chuyển trong mọi lời giải, hoặc xác định chi phí suy luận tối thiểu tới nút vùng dưới phải. Phần đầu vẫn cần phân rã sâu hơn thành các nhiệm vụ nhường chỗ; không gom toàn prefix vào “độ khó clear h6/h4”.

Không dùng tổng113/95 drag để xếp độ khó. Hai witness chỉ chứng minh có hai phương án và cho biết phần nào tạo relief trong từng phương án. Chưa có điểm Hard hoặc fun score.

## Hệ quả cho tài liệu và bước nghiên cứu tiếp

Đơn vị phân tích nên là **mục tiêu bị chặn + tập ràng buộc + các cách phá + nhiệm vụ được mở**, không phải một hole được chọn sẵn. Ví dụ ở đây: “mở hai mèo màu4 vùng dưới phải, rồi mở b2”; cách phá gồm clear h4 hoặc đỗ h4 sang trái sau khi sắp xếp hỗ trợ.

Cần phân biệt ba kết quả: ăn bớt để xóa mèo cản/tiến hàng đợi; dời vật để đổi đường/chỗ đỗ; clear để bỏ footprint. Khó nằm ở việc tìm và thực hiện phối hợp này. Thành quả được xác nhận bằng nhiệm vụ tiếp theo thực hiện được và giảm nhu cầu sắp xếp; không chỉ bằng diện tích trống tăng.

Các câu hỏi này được chuyển thành bước thử nghiệm, đầu ra và điều kiện hoàn tất trong [CURRENT_WORK](LEVELGEN_CURRENT_WORK.md). Cập nhật bằng chứng thực nghiệm tại report này; không tự xem các câu hỏi đã được giải đáp chỉ vì đã có kế hoạch.

## Kiểm chứng

- Hai plan của bản gốc được chạy lại và replay thành công bằng V42 reference riêng; harness build 0 warning, 0 error.
- Snapshot, sự kiện ăn, điểm neo trước/sau từng drag: `research/bottleneck-original-00115.json`; harness `research/ProgressionStudy/BottleneckStudy.cs`.
- Chẩn đoán tiếp cận và footprint-shadow: `research/access-focus-hybrid.json`, `research/access-focus-improved.json`. Đây là frozen-phase BFS giữ các hole khác đứng yên và dừng tại lần ăn đầu, cùng engine; không phải chứng minh tối ưu hay chứng nhận difficulty độc lập.
- Chỉ chỉnh harness nghiên cứu và tài liệu; không chỉnh level nguồn hay solver production.

## Đối chiếu cấu trúc00098 và00129 từ deep research

Đã tính lại ngày2026-10-15 từ ba JSON gốc, shape catalog và hai access-focus JSON còn giữ; input hashes trong access data khớp file hiện tại. Đây là kiểm tra tĩnh/đối chiếu telemetry, không phải targeted solve mới.

| Cấu trúc | 00098 | 00115 | 00129 |
|---|---|---|---|
| Hole / board cat / box / unique wall | 9 / 21 / 2 / 1 | 8 / 16 / 4 / 4 | 9 / 18 / 3 / 1 |
| Tổng footprint / ô chưa chiếm ban đầu | 29 / 5 | 25 / 11 | 31 / 6 |
| Consumer queue b0 | h4→h0→h6→h8 | h1→h5 | h4→h0→h3 |
| Consumer queue b1 | h5→h6→h3→h4 | h0→h5 | h3→h2→h0→h4 |
| Consumer queue b2 | Không có | h7→h3→h1 | h1→h5→h8→h7→h1→h1 |
| Consumer queue b3 | Không có | h6→h0 | Không có |
| Thành phần liên thông mạnh có chu trình trên graph consumer | {h0,h3,h4,h6} | Không có | {h0,h2,h3,h4}, {h1,h5,h7,h8} |

Ở ba mẫu, mỗi màu chỉ có một hole consumer nên phép ánh xạ trên là xác định. Với input có nhiều hole cùng màu, không được gán một consumer duy nhất nếu chưa biết assignment. Ô chưa chiếm không là capacity đỗ đồng thời.00098/00129 lệch target25cell của preset nhưng vẫn là reference hợp lệ; không tự đổi preset theo chúng.

Một cạnh hA→hB chỉ có nghĩa một entry phải được consume trước entry kia trong cùng queue. **Graph gộp theo ID hole có thể có cycle dù graph sự kiện consume vẫn theo thứ tự thời gian.** Nó không là deadlock, completion dependency hay bằng chứng mọi người chơi phải suy luận một vòng. Hai SCC không tự thành hai nút độc lập: geometry có thể liên kết chúng.

00098 là candidate để kiểm quan hệ partial consumption qua nhiều queue, đặc biệt h4/h6.00129 là candidate để kiểm consumer quay lại cùng queue và cách xử lý hai nhóm có chu trình; ba entry của h1 ở b2 có thể được gom một phần, không tự đòi ba chuyến đi. Các task bắt buộc, reservation và difficulty vẫn chưa xác định.

Telemetry trong bảng của deep research cũng khớp dữ liệu hiện có: hybrid consume checkpoints có đúng một pair là85,7% /88,0% /76,7%; longest DirectAccessSupported segment17/14/23drag. Chúng chỉ mô tả trace/frozen access, không chứng minh forced move hoặc độ khó.

### Làm rõ thử nghiệm ăn rồi đỗ ở b2 của00115

Supply của h7 là hai mèo trên map + một entry b2; h3 là một mèo trên map + một entry b2. Nếu đã ăn hết nguồn trên map, ăn entry b2 cuối **bắt buộc clear theo demand**, không thể chọn giữ sống để đỗ. Muốn tìm alternative park-live cần tới b2 khi còn thiếu nguồn trên map, rồi kiểm di chuyển/continuation tương ứng. Câu hỏi này là về thứ tự cấp mèo và geometry toàn pha, không chỉ thay thao tác tại snapshot ngay trước clear trong witness cũ.

Tương tự h1 có một mèo board và nguồn tại hai box; ăn b2 có thể còn sống nếu còn nguồn khác. Phân biệt remaining demand thực tế ở mỗi state trước khi gọi completion là lựa chọn chiến lược.

## Kết quả causal study — chốt cơ sở cho prototype

Artifact: [causal-study.json](research/causal-study.json), contract `bottleneck-analysis/0.1`, schema [analysis.schema.json](research/analysis.schema.json). Ba input/catalog/assembly hashes và scope từng search được lưu. Toàn bộ thử nghiệm trên bản gốc, không thêm biến thể.

### Ba câu hỏi00115

| Câu hỏi/thử nghiệm | Kết quả | Phạm vi kết luận |
|---|---|---|
| Q115-A-local: h2 ăn cả cat6/cat13 khi h6 chưa clear | NotFoundWithinBudget | Bắt đầu snapshot improved trước first clear h6 (sau73drag), beam35, depth12drag chia ở first consume, tối đa2500nodes/18giây; không chứng minh h6 bắt buộc trước h2 trên toàn level |
| Q115-B-final-reposition | Found | Sau các chuẩn bị đã quan sát, chỉ h4 di chuyển từ(4,0) sang(0,3), mở cả hai source h2; prefix replay độc lập khớp và nối tới win |
| Q115-B-endpoint-baseline | Found | Từ snapshot sau lượt h0 ăn b3 (sau75drag), mỗi actor có tối đa một action tới đích đỗ khai báo; tìm được schedule khác witness gốc |
| Q115-B-endpoint-freeze-h0/h7/h2/h3/h5 | ProvedAbsentWithinCompleteScope, cả5 phép thử | Vét hết mọi ordered subset của đúng các actor/endpoint còn lại; mỗi actor tối đa một lần. Không chứng minh actor đó cần trong mọi cách đỗ/đi nhiều lần/toàn level |
| Q115-C-h7, Q115-C-h3 | NotFoundWithinBudget | Lần lượt bắt đầu trước khi ăn cat9/cat12 trên map, giữ cat đó và cấm clear actor; tìm consume entry b2 rồi đỗ sống rời mouth. Beam25, depth10,1500nodes/12giây mỗi phép; chưa tìm được, không gọi impossible |

Q115-A đo goal **ăn cả hai source**; chưa trả lời mọi biến thể yếu hơn như chỉ tiếp cận source hoặc tiến b2. Q115-C h3 còn phụ thuộc việc mở head b2 của h7. Nghiên cứu kết thúc với giới hạn cụ thể, không tự chuyển Unknown thành rule dựng level.

Đích đỗ trong finite schedule: h0(0,0), h7(1,0), h2(4,5), h3(3,6), h5(1,5), h4(0,3). Schedule mới tìm được: **h0 → h2 → h7 → h3 → h5 → h4**, thay vì h0→h7→h2→h3→h5→h4 của witness improved. Hai thứ tự về cùng full settled state; nối suffix cũ và replay độc lập đều win. Vì vậy ngay trong đoạn này cũng không được coi exact actor order là progression bắt buộc.

Các lượt hỗ trợ trong schedule không ăn mèo: vai trò chính là đưa các footprint khỏi vùng cần cho bước sau và giữ chỗ đỗ hợp lệ. Đó là work hỗ trợ có witness, không phải minimum reasoning hoặc minimum drags toàn pha. Tập đích đỗ cố định chỉ là một cách hiện thực; đích đỗ khác hoặc nhiều lần dịch actor có thể thay dependency.

### Đối chiếu cùng contract trên00098 và00129

Hai mode của mỗi bản gốc được solve lại và replay V42 tới win. Plan hashes còn khớp dữ liệu access-focus giữ lại. Queue observations và local shadow checks đều được trích cùng pipeline như00115:

| Level | Witness ăn queue khi còn sống và rời mouth | Local relief tái kiểm | Điều rút ra cho prototype |
|---|---|---|---|
| 00098 | Hybrid h4 ăn b0 ở24, rời mouth ở25; h0 ăn b0 ở26, rời ở27. Improved h6 ăn b0 ở126, rời ở127 | Clear h1 ở78/119 mở cat12 cho h7 ăn ngay79/120; giữ footprint h1 làm mất access trên cùng remaining tasks | Partial supply/queue và hình học đỗ phải xét cùng nhau. Nút mở source trên map có thể quan trọng hơn SCC queue |
| 00115 | Improved h0 ăn b3 ở75, rời ở76; h1 ăn b2 ở86 còn sống rồi rời ở92 | Clear h4 ở108 mở hai source h2; h6 ở74 mở b3 cho h0 ở75. Reposition h4 cũng mở h2 | Mục tiêu vùng/source/mouth tách khỏi một completion cụ thể; có nhiều cách phá |
| 00129 | Hybrid h1 ăn entry0 b2 ở82, rời ở83; improved ăn ở178, rời ở179. h4/h0 cũng ăn queue rồi tiếp tục sống | Hybrid clear h8 ở108 mở entry3 b2 cho h7 ở109; improved clear h5 ở180 mở entry2 cho h8 ở181 | Một actor có nhiều vai trò/supply còn lại; clear và rời mouth sau partial consumption là hai semantics cần representation riêng |

Chỗ đỗ “rời mouth” được kiểm bằng footprint không còn phủ ô mouth và actor vẫn sống; chưa có phép cô lập nhân quả cho mọi lượt parking này. Các mốc shadow đều giữ cùng snapshot/demand/supply còn lại và continuation thực dùng source vừa mở. Chúng chứng minh **nút cục bộ**, chưa chứng minh mọi cycle là core khó hoặc hai SCC00129 là hai act độc lập.

00098 có thể clear h1 rồi mở h7 trước chuỗi queue cuối;00129 có đoạn partial queue rồi cascade clear. Đây là các kiểu progression được quan sát, không phải nhãn difficulty giữa ba level. Những lượt solver sắp xếp trước đó chưa được quy hết về nhiệm vụ; phần attribution chưa biết vẫn Unknown.

### Quyết định thiết kế từ evidence

Chọn hai pattern v0.1: **RegionRelease** và **QueueRevisitParking**. Cần checker goal/constraint/break/continuation và bounded alternative search; không cần generator ép một hole phải clear đầu, max SCC hay tăng total drag. Giữ human review cho legibility, elegance/fairness và nhịp chơi. Mọi record hiện có difficultyScore=null/Unrated.
