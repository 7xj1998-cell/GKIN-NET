# GKIN-NET

Palette AutoCAD ghép khung, đánh số tờ và in nhanh cho hồ sơ bình đồ — trắc dọc — trắc ngang.

## Tính năng v0.3.0

- Giao diện Unicode tiếng Việt, dựng lại theo bố cục PXHS với 4 tab dọc, card màu và trạng thái từng nhóm bản vẽ.
- Tự dò khung tên, tim tuyến, trắc dọc, trắc ngang; hỗ trợ chọn tay khi cần.
- Tạo khung trong `MODEL` kèm hình học hoặc tạo từng tờ trong `LAYOUT` với viewport khóa.
- Ghi đồng loạt `STT`, `MSBV`, `BVS`, `TENBVE`, `TYLE` theo từng loại hoặc nối tiếp cả bộ.
- In PDF đồng bộ bằng AutoCAD `PlotEngine`, sau đó ghép đúng thứ tự: một file nhiều trang, tách theo loại, CTB riêng và in lại theo cú pháp `2,5-7`.
- Tự chọn khổ giấy phù hợp và kiểm tra file PDF đầu ra trước khi báo thành công.

## Cài đặt

1. Tải `GKIN-APPLOAD.zip` tại [GitHub Releases](https://github.com/7xj1998-cell/GKIN-NET/releases).
2. Giải nén toàn bộ tệp trong gói vào cùng một thư mục; không tách `GKIN.dll` khỏi các DLL đi kèm.
3. Trong AutoCAD chạy `APPLOAD`, chọn `GKIN.lsp`, sau đó gõ `GKIN`.

## Tương thích

- Mục tiêu AutoCAD 2021–2024, .NET Framework 4.8, build bằng SDK AutoCAD 2021 (`AutoCAD.NET` 24.0.0).
- CI xác nhận biên dịch; bản v0.3.0 đã smoke-test `NETLOAD`, loader LISP và luồng MODEL/LAYOUT/attribute/PDF trên AutoCAD Core 2024.
- Nên thử trên bản sao DWG trước khi áp dụng cho hồ sơ đang sản xuất.
