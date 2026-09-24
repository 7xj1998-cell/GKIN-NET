;;; GKIN.lsp — nap GKIN.dll bang APPLOAD
;;; Dat GKIN.lsp va GKIN.dll CUNG THU MUC.
(vl-load-com)

(defun gkin:dll (/ f l)
  (setq f (findfile "GKIN.dll"))
  (if (and (null f) (setq l (findfile "GKIN.lsp")))
    (setq f (findfile (strcat (vl-filename-directory l) "\\GKIN.dll"))))
  f)

(defun gkin:cmd (args / r)
  ;; COMMAND khong phai first-class function tren mot so ban AutoCAD.
  ;; VL-CMDF co the truyen an toan vao VL-CATCH-ALL-APPLY.
  (setq r (vl-catch-all-apply 'vl-cmdf args))
  (if (vl-catch-all-error-p r)
    (progn
      (princ (strcat "\n[GKIN] Loi: " (vl-catch-all-error-message r)))
      nil)
    T))

(defun gkin:netload (f)
  (gkin:cmd (list "_.NETLOAD" f)))

(defun C:GKIN (/ f)
  (setq f (gkin:dll))
  (cond
    ((null f)
     (alert (strcat
       "GKIN: khong thay GKIN.dll\n\n"
       "Tai GKIN-APPLOAD.zip tu GitHub Releases\n"
       "https://github.com/7xj1998-cell/GKIN-NET/releases\n"
       "Giai nen GKIN.dll canh file GKIN.lsp nay.")))
    ((not (gkin:netload f))
     (alert "GKIN: khong nap duoc GKIN.dll. Xem dong lenh de biet chi tiet."))
    ((not (gkin:cmd (list "GKINUI")))
     (alert "GKIN: DLL da nap nhung khong goi duoc lenh GKINUI.")))
  (princ))

(defun C:GKINDO ()
  (if (and (gkin:dll) (gkin:netload (gkin:dll)))
    (gkin:cmd (list "GKINDONET"))
    (alert "GKIN: khong nap duoc GKIN.dll."))
  (princ))

(princ "\nGKIN.lsp da nap. Go GKIN de mo palette .NET (can GKIN.dll).")
(princ)
