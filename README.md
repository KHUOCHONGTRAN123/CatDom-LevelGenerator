# CatDom — thiết kế tool tạo level

Mục tiêu project hiện tại là xây tool tạo level hay cho designer, dựa trên source solver CatDom 3.0.0. Đã hoàn tất nghiên cứu có phạm vi trên ba level gốc và chốt đặc tả prototype v0.1; chưa implement generator.

**Bắt đầu từ [Generate Docs/README](Generate%20Docs/README.md)** và [mục tiêu hiện tại](Generate%20Docs/LEVELGEN_CURRENT_WORK.md).

## Cấu trúc cần đọc

| Thư mục | Vai trò |
|---|---|
| `Generate Docs/` | Thiết kế hiện hành, kế hoạch tiếp tục và nghiên cứu điểm tắc |
| `Levels/` | Corpus level tham chiếu hiện có; không tự sửa khi nghiên cứu |
| `IndependentSolver/` | Source solver production và catalog footprint |
| `Solver.Cli/` | CLI gọi source solver |
| `Verification/` | Test luật, công cụ replay và engine V42 dùng kiểm chứng độc lập |
| `docs/` | API, hướng dẫn API và kiểm chứng solver |
| `Tools/` | Script chạy solver/kiểm tra mechanic hiện có |

## Build và dùng solver

Cần .NET SDK 10:

```powershell
dotnet build Solver.slnx -c Release -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false /p:NuGetAudit=false --ignore-failed-sources
dotnet Solver.Cli/bin/Release/net10.0/cat-solver.dll Levels/Level00115.json --output result.json
```

API và tích hợp: [docs/API](docs/API.md). Kiểm chứng: [docs/VERIFICATION](docs/VERIFICATION.md). Lịch sử thay đổi: [CHANGELOG](CHANGELOG.md).

Đã xóa benchmark corpus, raw plans, nghiên cứu tối ưu solver và công cụ phát hành cũ theo xác nhận của người dùng; không giữ ZIP vì dữ liệu solver đã được người dùng cache riêng. Không dùng số liệu benchmark hoặc tổng drag để suy độ khó level hiện tại. Build artifacts được tạo lại khi chạy build, không cần bàn giao.

Dọn workspace ngày 2026-10-15: bỏ 4.021 file lịch sử (~305 MiB) và các bin/obj có thể build lại. Giữ source solver/CLI, test luật, V42Reference và nghiên cứu điểm tắc hiện hành.
