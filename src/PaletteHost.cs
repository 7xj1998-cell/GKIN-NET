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

        public static MainPanel Panel => _panel;

        public static void Ensure()
        {
            if (_ps != null) return;
            _panel = new MainPanel();
            _ps = new PaletteSet("GKIN — Ghep khung, in nhanh")
            {
                Style = PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.Snappable,
                MinimumSize = new System.Drawing.Size(360, 480),
                Size = new System.Drawing.Size(400, 680),
                DockEnabled = DockSides.Left | DockSides.Right,
                KeepFocus = true
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
            _ps.KeepFocus = true;
            try { _panel.DoLai(); }
            catch (System.Exception ex)
            {
                _panel.Toast("Loi do: " + ex.Message);
            }
        }

        public static void RescanFromCommand()
        {
            Ensure();
            _panel.DoLai();
            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[GKIN] Da do xong.");
        }

        public static void AllowPick(Action pick)
        {
            if (_ps != null) _ps.KeepFocus = false;
            try { pick(); }
            finally
            {
                if (_ps != null) _ps.KeepFocus = true;
            }
        }
    }
}
