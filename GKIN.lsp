;;; GKIN.lsp — nạp GKIN.dll đúng một lần bằng APPLOAD.
;;; Đặt GKIN.lsp, GKIN.dll và các DLL phụ thuộc trong cùng thư mục.
(vl-load-com)

;; Lưu đường dẫn ngay trong lúc APPLOAD; không tìm lại khi lệnh đã kết thúc.
(setq *gkin-lsp-file* (findfile "GKIN.lsp"))
(setq *gkin-lsp-dir*
  (if *gkin-lsp-file* (vl-filename-directory *gkin-lsp-file*) nil))
(setq *gkin-dll-file*
  (if *gkin-lsp-dir* (strcat *gkin-lsp-dir* "\\GKIN.dll") nil))

(defun gkin:command-available-p (/ value)
  (setq value (vl-catch-all-apply 'getcname (list "GKIN")))
  (and (not (vl-catch-all-error-p value)) value))

(if (not (gkin:command-available-p))
  (if (and *gkin-dll-file* (findfile *gkin-dll-file*))
    (vl-catch-all-apply 'vl-cmdf (list "_.NETLOAD" *gkin-dll-file*))))

(if (gkin:command-available-p)
  (princ "\nGKIN đã nạp. Gõ GKIN để mở bảng.")
  (princ "\nGKIN không nạp được. Kiểm tra GKIN.dll và các DLL phụ thuộc cùng thư mục."))
(princ)
