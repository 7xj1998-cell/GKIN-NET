# GKIN-NET

Palette AutoCAD ghép khung, in nhanh. Build DLL trên GitHub Actions.

- Mục tiêu AutoCAD 2021–2024: net48, build bằng SDK thấp nhất trong nhóm (`AutoCAD.NET` 24.0.0).
- CI chỉ xác nhận biên dịch. Trước khi phát hành cần smoke-test DLL trên AutoCAD 2021 và 2024.
- Chế độ `LAYOUT` tạo khung + viewport; hai chế độ `MODEL` đang khóa để tránh báo thành công khi chưa sao chép hình học.
- APPLOAD `GKIN.lsp` (kèm `GKIN.dll` cùng thư mục)

Sau khi Actions xong: [Releases](https://github.com/7xj1998-cell/GKIN-NET/releases) → tải `GKIN-APPLOAD.zip`.
