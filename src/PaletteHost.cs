using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GKIN
{
    public static class PaletteHost
    {
        static PaletteSet _ps;
        static MainPanel _panel;
        static bool _eventsAttached;
        static bool _picking;

        public static MainPanel Panel => _panel;

        public static void Ensure()
        {
            if (_ps != null) return;
            _panel = new MainPanel();
            _ps = new PaletteSet("GKIN — Ghép khung, in nhanh")
            {
                Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.Snappable,
                MinimumSize = new System.Drawing.Size(400, 460),
                Size = new System.Drawing.Size(430, 560),
                DockEnabled = DockSides.Left | DockSides.Right,
                KeepFocus = false
            };
            _ps.Add("GKIN", _panel);
            AttachDocumentEvents();
        }

        static void AttachDocumentEvents()
        {
            if (_eventsAttached) return;
            AcadApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            _eventsAttached = true;
        }

        static void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (_picking) return;
            _panel?.OnDocumentChanged(e.Document);
        }

        static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (ReferenceEquals(e.Document, AcadApp.DocumentManager.MdiActiveDocument))
                _panel?.OnDocumentChanged(null);
        }

        public static void Terminate()
        {
            if (!_eventsAttached) return;
            AcadApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
            AcadApp.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            _eventsAttached = false;
        }

        public static void Show()
        {
            Ensure();
            _ps.Visible = true;
            try { _panel.DoLai(); }
            catch (System.Exception ex)
            {
                _panel.Toast("Lỗi dò: " + ex.Message);
            }
        }

        public static void Hide()
        {
            if (_ps != null) _ps.Visible = false;
        }

        public static void AllowPick(Action pick)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null || pick == null) return;
            _picking = true;
            bool visible = _ps != null && _ps.Visible;
            try
            {
                if (_ps != null)
                {
                    _ps.KeepFocus = false;
                    _ps.Visible = false;
                }
                try { Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView(); }
                catch { }
                pick();
            }
            finally
            {
                _picking = false;
                if (_ps != null)
                {
                    _ps.Visible = visible;
                    _ps.KeepFocus = false;
                }
            }
        }
    }
}
