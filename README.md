# GKIN-NET

Palette AutoCAD ghép khung, đánh số tờ và in nhanh cho hồ sơ bình đồ — trắc dọc — trắc ngang.

## Tính năng

- Bảng tiếng Việt, tab nằm ngang, chữ không bị cắt. Trang dài thì cuộn.
- Tự dò khung tên, tim tuyến VNROAD (`TDTDBALIGNMENT` / `TDTDBPOLYLINE` / `plinetntn`), trắc dọc, trắc ngang.
- Cắt trắc dọc theo bước mét: nhân bản đầu bảng, cắt dải theo X, lấy lý trình thật (`Km0+00 -:- Km0+340.00`).
- Xếp mặt cắt ngang lưới Ngang 1-2-3-4 hoặc Dọc; đầy tờ thì sang tờ mới.
- Mặc định xuất `LAYOUT — gộp IN-BDTDTN`: cắt BĐ/TĐ/TN rồi xếp nhiều khung+viewport trên **một** paperspace, giống PXHS. Tùy chọn cũ «mỗi tờ một layout» vẫn còn.
- Ghi đồng loạt thẻ khung: `TOSO`/`STT`, `MATO`/`MSBV`, `TONGTO`/`BVS`, `TENTOBVE`/`TENBVE`, `TYLE`.
- In PDF nhiều trang bằng AutoCAD `PlotEngine`.

## Lõi hình học (0.3.8)

| Service | Việc làm |
|---|---|
| `ProfileCutterService` | Đầu bảng + cắt dải trắc dọc + bóc text lý trình |
| `CrossSectionPackerService` | Gom cọc trắc ngang và xếp lưới theo tờ |
| `LayoutViewportService` | Viewport paperspace, tỷ lệ, xoay theo tim tuyến |
| `AttributeSyncService` | Ghi alias attribute khung tên |

Nút **THỰC HIỆN** tab Bản vẽ gọi các service này (không chỉ in phương án ra dòng lệnh).

## Cài đặt

1. Tải `GKIN-APPLOAD.zip` tại [GitHub Releases](https://github.com/7xj1998-cell/GKIN-NET/releases).
2. Giải nén toàn bộ ZIP vào cùng một thư mục. `GKIN.dll` và mọi DLL phụ thuộc phải nằm cạnh `GKIN.lsp`.
3. Trong AutoCAD chạy `APPLOAD`, chọn `GKIN.lsp`.
4. Gõ lệnh `GKIN` để mở bảng. Nút **Dò lại** nằm trên palette.

## Tương thích

- AutoCAD 2021–2024, .NET Framework 4.8, SDK AutoCAD 2021 (`AutoCAD.NET` 24.0.0).
- Nên thử trên bản sao DWG trước khi áp dụng cho hồ sơ đang sản xuất.
