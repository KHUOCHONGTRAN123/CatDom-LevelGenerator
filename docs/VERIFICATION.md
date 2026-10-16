# Kiểm chứng solver và nghiên cứu level

`Verification/V42Reference/` là engine V42 riêng dùng replay; không đóng vào thư viện/CLI production. Giữ source này vì nghiên cứu điểm tắc cần kiểm chứng lời giải độc lập.

## Build và nhóm test còn giữ

```powershell
dotnet build Solver.slnx -c Release -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false /p:NuGetAudit=false --ignore-failed-sources
dotnet Verification/bin/Release/net10.0/Solver.Verification.dll v3-countdown-oracle-tests .
```

Các mode test luật và replay trong `Verification/Program.cs` vẫn giữ. Mode `tests` chạy bộ tổng hợp cũ có phụ thuộc các fixture từ corpus299level, ví dụ Level00001/00206. Corpus đang có trong workspace chỉ là tập chọn lọc; không coi build thành công hoặc test một nhóm là toàn bộ test cũ đã pass. Muốn chạy nhóm cụ thể cần kiểm tra các level mà nhóm đó đọc.

`route-graph-tests` dùng danh sách21level nhỏ trong `Verification/Fixtures/route-graph-levels.txt`, đã tách khỏi CSV benchmark. Nó vẫn cần những level tương ứng, không cần báo cáo hiệu năng.

## Nghiên cứu hiện tại

Cách build/chạy hai plan Level00115 và independent replay: [Generate Docs/research/README](../Generate%20Docs/research/README.md). Kế hoạch thử alternative/dependency: [CURRENT_WORK](../Generate%20Docs/LEVELGEN_CURRENT_WORK.md). Harness hiện tại không tự chứng minh tối ưu hoặc quan hệ bắt buộc.

## Những phần đã dọn

Đã xóa raw benchmark, `Verification/V42Results`, nghiên cứu tối ưu solver cũ và report generator cũ theo xác nhận của người dùng. Các mode `worker`, `all`, `research`, `research-acceptance`, `profile`, `hybrid-check`, `easy-comparison`, `budget-5000`, `phase-search`, `phase-fixture`, `constraint-probe` và script report/đóng gói cũ đi kèm đã bỏ khỏi workspace làm việc. Chúng không thuộc đường chạy solver production hoặc nghiên cứu điểm tắc hiện hành.

Đây là dọn dữ liệu/công cụ lịch sử, không phải thay engine hay nới luật. Không giữ ZIP tại project; người dùng đã cache nghiên cứu solver riêng.
