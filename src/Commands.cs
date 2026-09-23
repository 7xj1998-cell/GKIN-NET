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
            PaletteHost.Ensure();
        }

        public void Terminate() { }
    }

    public class Commands
    {
        [CommandMethod("GKINUI", CommandFlags.Session)]
        public void ShowUi()
        {
            PaletteHost.Show();
        }

        [CommandMethod("GKINDO", CommandFlags.Modal)]
        public void Rescan()
        {
            PaletteHost.Ensure();
            PaletteHost.RescanFromCommand();
        }
    }
}
