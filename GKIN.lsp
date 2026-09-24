;;; GKIN.lsp — nạp GKIN.dll bằng APPLOAD
;;; Đặt GKIN.lsp và GKIN.dll CÙNG THƯ MỤC.
(vl-load-com)

(defun gkin:dll (/ f l)
  (setq f (findfile "GKIN.dll"))
  (if (and (null f) (setq l (findfile "GKIN.lsp")))
    (setq f (findfile (strcat (vl-filename-directory l) "\\GKIN.dll"))))
  f)

(defun gkin:cmd (args / r)
  ;; COMMAND không phải first-class function trên một số bản AutoCAD.
  ;; VL-CMDF có thể truyền an toàn vào VL-CATCH-ALL-APPLY.
  (setq r (vl-catch-all-apply 'vl-cmdf args))
  (if (vl-catch-all-error-p r)
    (progn
      (princ (strcat "\n[GKIN] Lỗi: " (vl-catch-all-error-message r)))
      nil)
    T))

(defun gkin:netload (f)
  (gkin:cmd (list "_.NETLOAD" f)))

(defun C:GKIN (/ f)
  (setq f (gkin:dll))
  (cond
    ((null f)
     (alert (strcat
       "GKIN: không thấy GKIN.dll\n\n"
       "Tải GKIN-APPLOAD.zip từ GitHub Releases\n"
       "https://github.com/7xj1998-cell/GKIN-NET/releases\n"
       "Giải nén GKIN.dll cạnh file GKIN.lsp này.")))
    ((not (gkin:netload f))
     (alert "GKIN: không nạp được GKIN.dll. Xem dòng lệnh để biết chi tiết."))
    ((not (gkin:cmd (list "GKINUI")))
     (alert "GKIN: DLL đã nạp nhưng không gọi được lệnh GKINUI.")))
  (princ))

(defun C:GKINDO ()
  (if (and (gkin:dll) (gkin:netload (gkin:dll)))
    (gkin:cmd (list "GKINDONET"))
    (alert "GKIN: không nạp được GKIN.dll."))
  (princ))

(princ "\nGKIN.lsp đã nạp. Gõ GKIN để mở palette .NET (cần GKIN.dll).")
(princ)
