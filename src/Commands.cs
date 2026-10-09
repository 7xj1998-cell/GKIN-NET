using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(GKIN.Commands))]
[assembly: ExtensionApplication(typeof(GKIN.Plugin))]

namespace GKIN
{
    public class Plugin : IExtensionApplication
    {
        public void Initialize() { DependencyResolver.Register(); }
        public void Terminate() { DependencyResolver.Unregister(); PaletteHost.Terminate(); }
    }

    public class Commands
    {
        [CommandMethod("GKIN", CommandFlags.Session)]
        public void ShowUi() => PaletteHost.Show();
    }
}
