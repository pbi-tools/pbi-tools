using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace PbixTools
{
    public interface IDependenciesResolver
    {
        bool TryFindMsmdsrv(out string path);
    }

    public class DependenciesResolver : IDependenciesResolver
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<DependenciesResolver>();

        // Init singleton at app startup
        // This is for runtime .. Still require static dll location for dev/compile
        // Locate installs of PBI-Desktop, SSDT, SQL-Tabular

        // start off with hard-coded paths
        // make more flexible later
        // implement AppDomain.CurrentDomain.AssemblyResolve

        // TODO Detect install paths dynamically (from registry); start off with using ENV vars for common folders
        // TODO Allow explicit path specified in settings
        private static readonly IDictionary<string, string> Paths = new Dictionary<string, string> {
            { "PBI", @"C:\Program Files\Microsoft Power BI Desktop\bin\" },
            { "SSDT-2017", @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\SSAS\LocalServer\" },
            { "SSDT-2015", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\PrivateAssemblies\Business Intelligence Semantic Model\LocalServer\" },
        };


        public DependenciesResolver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // args.Name like: 'Microsoft.Mashup.Client.Packaging, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'
            var dllName = args.Name.Split(',')[0];
            // TODO Works for now, but reconsider later
            var path = Paths.Select(p => Path.Combine(p.Value, $"{dllName}.dll")).FirstOrDefault(File.Exists);
            return path != null ? Assembly.LoadFile(path) : null;
        }

        public bool TryFindMsmdsrv(out string path)
        {
            path = Paths.Select(p => Path.Combine(p.Value, "msmdsrv.exe")).FirstOrDefault(File.Exists);
            return path != null;
        }
    }
}
