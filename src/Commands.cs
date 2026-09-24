using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GKIN.Commands))]
[assembly: ExtensionApplication(typeof(GKIN.Plugin))]

namespace GKIN
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            // Defer WinForms/PaletteSet creation until the user invokes GKIN.
            // This keeps NETLOAD safe in Core Console and during AutoCAD startup.
        }

        public void Terminate()
        {
            PaletteHost.Terminate();
        }
    }

    public class Commands
    {
        [CommandMethod("GKIN", CommandFlags.Session)]
        public void ShowUi()
        {
            PaletteHost.Show();
        }
    }
}
