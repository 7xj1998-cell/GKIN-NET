# GKIN-NET

Palette AutoCAD ghép khung, đánh số tờ và in nhanh cho hồ sơ bình đồ — trắc dọc — trắc ngang.

## Tính năng

- Bảng tiếng Việt, tab nằm ngang, chữ không bị cắt. Trang dài thì cuộn.
- Tự dò khung tên, tim tuyến, trắc dọc, trắc ngang; hỗ trợ chọn tay khi cần.
- Tạo khung trong `MODEL` kèm hình học hoặc tạo từng tờ trong `LAYOUT` với viewport khóa.
- Ghi đồng loạt `STT`, `MSBV`, `BVS`, `TENBVE`, `TYLE` theo từng loại hoặc nối tiếp cả bộ.
- In PDF nhiều trang trực tiếp bằng AutoCAD `PlotEngine`: một file, tách theo loại, CTB riêng và in lại theo cú pháp `2,5-7`.
- Tự chọn khổ giấy phù hợp; chỉ báo thành công khi file tồn tại và đủ số trang.

## Cài đặt

1. Tải `GKIN-APPLOAD.zip` tại [GitHub Releases](https://github.com/7xj1998-cell/GKIN-NET/releases).
2. Giải nén toàn bộ ZIP vào cùng một thư mục. `GKIN.dll` và mọi DLL phụ thuộc phải nằm cạnh `GKIN.lsp`.
3. Trong AutoCAD chạy `APPLOAD`, chọn `GKIN.lsp`.
4. Gõ duy nhất lệnh `GKIN` để mở bảng. Nút **Dò lại** nằm ngay trên palette.

## Tương thích

- Mục tiêu AutoCAD 2021–2024, .NET Framework 4.8, build bằng SDK AutoCAD 2021 (`AutoCAD.NET` 24.0.0).
- CI xác nhận biên dịch; loader LISP chỉ gọi `NETLOAD` khi lệnh `GKIN` chưa được đăng ký.
- Nên thử trên bản sao DWG trước khi áp dụng cho hồ sơ đang sản xuất.
