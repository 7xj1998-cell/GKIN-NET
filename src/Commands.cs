using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

[assembly: CommandClass(typeof(GKIN.Commands))]
[assembly: ExtensionApplication(typeof(GKIN.Plugin))]

namespace GKIN
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            // Defer WinForms/PaletteSet creation until GKINUI is invoked.
            // This keeps NETLOAD safe in Core Console and during AutoCAD startup.
        }

        public void Terminate()
        {
            PaletteHost.Terminate();
        }
    }

    public class Commands
    {
        [CommandMethod("GKINUI", CommandFlags.Session)]
        public void ShowUi()
        {
            PaletteHost.Show();
        }

        [CommandMethod("GKINDONET", CommandFlags.Modal)]
        public void Rescan()
        {
            PaletteHost.Ensure();
            PaletteHost.RescanFromCommand();
        }
    }
}
