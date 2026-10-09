using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GKIN
{
    internal static class DependencyResolver
    {
        static readonly string DirectoryPath = Path.GetDirectoryName(typeof(DependencyResolver).Assembly.Location);
        static bool registered;
        public static void Register()
        {
            if (registered) return;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve; registered = true;
        }
        public static void Unregister()
        {
            if (!registered) return;
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve; registered = false;
        }

        static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            // NETLOAD does not reliably probe transitive dependencies next to
            // a plugin. Never resolve AutoCAD binaries or unrelated plugins.
            try
            {
                if (args.RequestingAssembly != null && !string.Equals(Path.GetDirectoryName(args.RequestingAssembly.Location), DirectoryPath, StringComparison.OrdinalIgnoreCase)) return null;
                var name = new AssemblyName(args.Name);
                string simple = name.Name;
                if (!(simple.StartsWith("PdfSharp", StringComparison.Ordinal)
                    || simple.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                    || simple == "Microsoft.Bcl.AsyncInterfaces"
                    || simple == "System.Buffers" || simple == "System.Memory" || simple == "System.Numerics.Vectors"
                    || simple == "System.Runtime.CompilerServices.Unsafe" || simple == "System.Threading.Tasks.Extensions"
                    || simple == "System.Diagnostics.DiagnosticSource" || simple == "System.Security.Cryptography.Pkcs")) return null;
                string file = Path.Combine(DirectoryPath, simple + ".dll");
                if (!File.Exists(file)) return null;
                var definition = AssemblyName.GetAssemblyName(file);
                if (!AssemblyName.ReferenceMatchesDefinition(name, definition)
                    || !(name.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(definition.GetPublicKeyToken() ?? Array.Empty<byte>())
                    || (name.Version != null && (definition.Version.Major != name.Version.Major || definition.Version.Minor != name.Version.Minor || definition.Version < name.Version))) return null;
                return Assembly.LoadFrom(file);
            }
            catch { return null; }
        }
    }
}
