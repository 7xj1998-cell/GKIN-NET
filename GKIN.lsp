;;; GKIN.lsp — nap GKIN.dll bang APPLOAD
;;; Dat GKIN.lsp va GKIN.dll CUNG THU MUC.
(vl-load-com)

(defun gkin:dll (/ f l)
  (setq f (findfile "GKIN.dll"))
  (if (and (null f) (setq l (findfile "GKIN.lsp")))
    (setq f (findfile (strcat (vl-filename-directory l) "\\GKIN.dll"))))
  f)

(defun gkin:netload (f / r)
  (setq r (vl-catch-all-apply 'command (list "._NETLOAD" f)))
  (not (vl-catch-all-error-p r)))

(defun C:GKIN (/ f)
  (setq f (gkin:dll))
  (cond
    ((null f)
     (alert (strcat
       "GKIN: khong thay GKIN.dll\n\n"
       "Tai GKIN-APPLOAD.zip tu GitHub Releases\n"
       "https://github.com/7xj1998-cell/GKIN-NET/releases\n"
       "Giai nen GKIN.dll canh file GKIN.lsp nay.")))
    (T
     (gkin:netload f)
     (vl-catch-all-apply 'command (list "._GKINUI"))))
  (princ))

(defun C:GKINDO ()
  (if (gkin:dll) (command "._GKINDO") (C:GKIN))
  (princ))

(princ "\nGKIN.lsp da nap. Go GKIN de mo palette .NET (can GKIN.dll).")
(princ)
