# GKIN-NET

Ghép khung, in nhanh cho hồ sơ bình đồ (BĐ), trắc dọc (TĐ) và trắc ngang (TN).

## Cài đặt

1. Tải `GKIN-APPLOAD.zip` từ [GitHub Releases](https://github.com/7xj1998-cell/GKIN-NET/releases).
2. Giải nén toàn bộ ZIP vào cùng một thư mục. Giữ mọi DLL phụ thuộc cạnh `GKIN.dll` và `GKIN.lsp`.
3. Trong AutoCAD chạy `APPLOAD`, chọn `GKIN.lsp`.
4. Gõ `GKIN` để mở bảng. Palette chỉ tạo khi gọi lệnh; nạp lại LISP không nạp DLL lần nữa.

Bạn nên test trên bản sao DWG trước khi dùng cho hồ sơ sản xuất.

## Quy trình

1. **Dò lại**: chọn khung tên, tim tuyến và trắc dọc cần xuất. Nếu có nhiều vùng TĐ, chọn đúng vùng trong danh sách; không ghép bbox của tất cả vùng.
2. Chọn **MODEL** hoặc **LAYOUT**. Model có kiểu một hàng hoặc xếp hàng; Layout tạo một layout cho mỗi tờ.
3. Chọn xuất riêng BĐ/TĐ/TN hoặc **Gộp bình đồ + trắc dọc**. Khi gộp, hai vùng trên mỗi tờ dùng cùng khoảng lý trình.
4. **THỰC HIỆN**, đánh số tờ rồi **In PDF**. Tên tờ lấy lý trình đã lưu, không suy từ số thứ tự bắt đầu.

TĐ cắt theo bước mét từ nhãn lý trình thật nếu đủ dữ liệu; Theo bề rộng tạo một tờ. Điểm cắt báo thiếu dữ liệu, không tự chia giả.

TN xếp tối đa 4 mặt cắt mỗi tờ, ngang hoặc dọc. Đầu bảng dò được sẽ lặp ở mặt cắt thiếu đầu bảng. Nếu chưa dò được đầu bảng, GKIN giữ vùng nguồn; bạn cần kiểm tra trước khi in.

Model chứa block được cắt bằng SpatialFilter, tránh để polyline dài tràn sang tờ khác. Model và Layout lưu loại tờ, thứ tự và tên/lý trình trong DWG để dò lại khi đổi tài liệu hoặc mở lại bản vẽ.

**Không in hình nguồn đã ghép** chuyển hình nguồn sang layer không in, không xoá hình gốc; thao tác nằm trong transaction và có thể Undo. Không bật khi layout GKIN đang dùng nguồn, vì nó làm mất nội dung viewport. Khôi phục layer nguồn trước khi chuyển từ chế độ này sang xuất Layout.

Tỷ lệ nhập ở dòng dò hiện dùng để ghi attribute khung. Hình học khớp vùng tờ, **chưa khoá theo tỷ lệ in nhập vào**. Không dùng giá trị attribute làm bằng chứng tỷ lệ in thực tế.

## Module nhận diện đường

- `RoadInteropService`: đọc nhãn trong block/attribute và đối tượng native hỗ trợ Explode; dựng tim tham chiếu trong bộ nhớ, nối hai đầu và giữ bulge cung. Không explode hay sửa đối tượng TDT gốc. Cách đọc tham chiếu dựa trên cơ sở đã khảo sát trong dự án BHT.
- `RoadDrawingDetectionService`: tách vùng TĐ và mặt cắt TN theo vị trí, tránh gom đối tượng có bbox rất lớn.
- `ProfileCutterService`: đọc `Km12+345`, nhãn canh giữa và đầu bảng Unicode/VNI/TCVN3; nội suy vị trí cắt theo lý trình.
- `ModelWindowService`: clone hình học vào block có cửa sổ cắt bền vững.
- `SheetMetadataService`: lưu và khôi phục dữ liệu tờ từ DWG.

TDT thường dùng `TDTDBALIGNMENT`, `plinetdtn`, `tracdocthietke`, `plinetntn` và XData `KS_TN`. GKIN ưu tiên các dấu hiệu này; chọn tay vẫn dùng được.

**Proxy không phải tim đọc được.** Nếu tim chỉ còn `ACAD_PROXY_ENTITY`, bạn cần mở DWG bằng profile TDT/VNroad hoặc object enabler phù hợp phiên bản AutoCAD. Có thể chọn polyline tham chiếu, nhưng gộp BĐ + TĐ chỉ chạy khi dãy cọc đủ phủ khoảng TĐ và khoảng cách dọc tim khớp chênh lệch lý trình. Nếu không đạt, GKIN dừng và báo thiếu dữ liệu, không dựng tim từ đường chéo bbox.

Chưa xác nhận tương thích toàn bộ dữ liệu VNroad 7.1/9.1, ADSCivil và HTTK. Gói HTTK từ OneDrive chưa truy cập được, nên chưa dùng làm cơ sở triển khai.

## Kiểm tra bản 0.4.0-beta.1

- Build Release: `dotnet build src/GKIN.csproj -c Release`.
- Integration test chạy trong AutoCAD Core Console 2024, không phải test giả: [hướng dẫn](tests/Integration/README.md).
- Dữ liệu tổng hợp: ghép BĐ + TĐ cùng lý trình, lặp đầu bảng TN, Model/viewport, dò lại tờ, tạo lại sau khi ẩn nguồn, PDF Model 4 trang và Layout 3 trang.
- Bản sao DWG TDT thực tế: dò 4 vùng TĐ, 120 mặt cắt TN; TĐ được chọn từ Km39+000 đến Km40+127,96, chia 4 tờ; xuất PDF 5 trang gồm 4 TĐ và 1 TN.
- PDF 1.7 được kiểm tra số trang và render để xem cửa sổ cắt. Chưa xác nhận ghép BĐ + TĐ trên DWG proxy này vì dữ liệu cọc trên tim chưa đầy đủ.

Giữ .NET Framework 4.8, x64, `AutoCAD.NET` 24.0.0, phạm vi AutoCAD 2021–2024. Runtime lần này test trên AutoCAD 2024; các phiên bản còn lại cần test thêm. Workflow CI không thay đổi.
