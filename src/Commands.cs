using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GKIN.Commands))]
[assembly: ExtensionApplication(typeof(GKIN.Plugin))]

namespace GKIN
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize() { }
        public void Terminate() { PaletteHost.Terminate(); }
    }

    public class Commands
    {
        [CommandMethod("GKIN", CommandFlags.Session)]
        public void ShowUi() => PaletteHost.Show();

        [CommandMethod("GKINUI", CommandFlags.Session)]
        public void ShowUiAlias() => PaletteHost.Show();

        [CommandMethod("GKINDO", CommandFlags.Modal)]
        public void Rescan()
        {
            PaletteHost.Ensure();
            PaletteHost.Panel?.DoLai();
        }
    }
}
