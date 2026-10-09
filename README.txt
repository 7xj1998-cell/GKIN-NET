GKIN v0.4.0-beta.1 — GHÉP KHUNG, IN NHANH

1. Tải GKIN-APPLOAD.zip tại:
   https://github.com/7xj1998-cell/GKIN-NET/releases
2. Giải nén TOÀN BỘ tệp vào CÙNG một thư mục.
   GKIN.dll và mọi DLL phụ thuộc phải nằm cạnh GKIN.lsp.
3. AutoCAD 2021–2024: APPLOAD, chọn GKIN.lsp.
4. Gõ GKIN. Dò lại, chọn đúng trắc dọc cần xuất.
5. Chọn MODEL hoặc LAYOUT, rồi THỰC HIỆN, đánh số và In PDF.

Bạn nên test trên bản sao DWG. Khởi động lại AutoCAD khi đổi phiên bản DLL.

Mới: module tim/nhãn theo cơ sở BHT, tách vùng TĐ/TN, lặp đầu bảng TN,
cắt block Model để hình không tràn tờ, lưu loại tờ và lý trình trong DWG.

Gộp BĐ + TĐ chỉ chạy khi đủ cọc và khoảng cách dọc tim khớp lý trình.
Nếu tim là proxy, mở profile TDT/VNroad/object enabler phù hợp hoặc chọn
polyline tim tham chiếu. Chương trình không suy tim từ đường chéo bbox.

Tỷ lệ nhập tại dòng dò hiện dùng để ghi attribute; hình khớp vùng tờ,
chưa khoá theo tỷ lệ in nhập vào. Hãy kiểm tra tỷ lệ thực tế trước khi dùng.

Không ẩn nguồn khi layout đang dùng nguồn. Nếu nguồn đã chuyển sang layer
không in, Undo hoặc khôi phục layer trước khi xuất Layout.

Bản beta: runtime đã test trên AutoCAD 2024. Cần test thêm 2021–2023,
dữ liệu native VNroad 7.1/9.1 và việc ghép BĐ + TĐ trên hồ sơ thực tế.
