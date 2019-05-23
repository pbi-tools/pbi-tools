using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using Serilog;

namespace PbiTools.Utils
{
    public interface IDependenciesResolver
    {
        bool TryFindMsmdsrv(out string path);
        string ShadowCopyMsmdsrv(string path);
        string GetEffectivePowerBiInstallDir();
    }

    public class DependenciesResolver : IDependenciesResolver
    {
        private const string MSMDSRV_EXE = "msmdsrv.exe";
        private const string LOCALAPPDATA = "%LOCALAPPDATA%";

        private static readonly ILogger Log = Serilog.Log.ForContext<DependenciesResolver>();

        // Init singleton at app startup
        // This is for runtime .. Still require static dll location for dev/compile
        // Locate installs of PBI-Desktop, SSDT, SSAS Tabular

        // Support three possible resolution targets:
        // PowerBI.Packaging, msmdsrv, MashupEngine

        // TODO Allow explicit path specified in settings
        //private static readonly IDictionary<string, string> Paths = new Dictionary<string, string> {
        //    { "PBI", @"C:\Program Files\Microsoft Power BI Desktop\bin\" },
        //    { "SSDT-2017", @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\SSAS\LocalServer\" },
        //    { "SSDT-2015", @"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\PrivateAssemblies\Business Intelligence Semantic Model\LocalServer\" },
        //};

        private readonly Lazy<PowerBIDesktopInstallation> _pbiInstall = new Lazy<PowerBIDesktopInstallation>(GetPBIInstall);

        public DependenciesResolver() //TODO Initialize with PbixProj reference (which has settings)
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve(this);
        }

        private static PowerBIDesktopInstallation GetPBIInstall()
        {
            // TODO Refactor this to allow user to specify preferred PBI location

            var install = PowerBILocator.FindInstallations()
                .Where(x => x.Is64Bit)                    // TODO Support 32-bit in a future version
                .OrderByDescending(x => x.ProductVersion) // Use highest version installed
                .FirstOrDefault();
            if (install == null) throw new Exception("No 64-bit Power BI Desktop installation found"); // TODO Make specific exception

            Log.Information("Using Power BI Desktop install: {ProductVersion} at {InstallDir}", install.ProductVersion, install.InstallDir);

            return install;
        }

        private ResolveEventHandler AssemblyResolve(object token)
        {
            return (sender, args) =>
            {
                if (!Equals(this, token)) return null;

                // args.Name like: 'Microsoft.Mashup.Client.Packaging, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'
                Log.Verbose("Attempting to resolve assembly: {AssemblyName}", args.Name);

                var dllName = args.Name.Split(',')[0];
                var path = Path.Combine(_pbiInstall.Value.InstallDir, $"{dllName}.dll");
                if (File.Exists(path))
                {
                    Log.Debug("Assembly '{AssemblyName}' found at {Path}", args.Name, path);
                    return Assembly.LoadFile(path);
                }
                else
                {
                    Log.Debug("Could not resolve assembly: {AssemblyName}", args.Name);
                    return null;
                }
            };
        }

        private static string GetShadowCopyDir(string winAppHandle) => Path.Combine(
            Environment.ExpandEnvironmentVariables(LOCALAPPDATA),
            "pbi-tools", // TODO make platform specific (x64/x86)
            winAppHandle);

        private static string GetWinAppHandle(string path) => path.Split(Path.DirectorySeparatorChar)
            .FirstOrDefault(s => s.StartsWith("Microsoft.MicrosoftPowerBIDesktop"));

        public bool TryFindMsmdsrv(out string path)
        {
            // a standard user has no execute permissions for msmdsrv.exe in the Windows Store install dir
            // need to shadow-copy MSDMSRV and dependencies into %LOCALAPPDATA%

            path = Path.Combine(
                _pbiInstall.Value.InstallDir, // "C:\\Program Files\\WindowsApps\\Microsoft.MicrosoftPowerBIDesktop_2.56.5023.0_x64__8wekyb3d8bbwe\\bin"
                MSMDSRV_EXE
            );

            var winAppHandle = GetWinAppHandle(path);
            if (winAppHandle != null)
            {
                var shadowCopyPath = Path.Combine(
                    GetShadowCopyDir(winAppHandle),
                    MSMDSRV_EXE);
                if (File.Exists(shadowCopyPath))
                    path = shadowCopyPath;
            }

            return File.Exists(path);
        }

        private static readonly string[] MsmdsrvCopyInclude = {
            "msmdsrv*",
            "ms*.dll",
            "Microsoft.AnalysisServices.*",
            "msa*.dll",
            "tm*.dll",
            "x*.dll",
            "tbb*.dll",
            "FlightRecorderTraceDef.xml"
        };

        public string ShadowCopyMsmdsrv(string path)
        {
            var winAppHandle = GetWinAppHandle(path);
            if (winAppHandle == null)
            {
                throw new Exception("Cannot shadow-copy MSMDSRV dependencies from a source directory other than a Windows Store installation.");
            }

            var basePath = Path.GetDirectoryName(path);

            var files = new HashSet<string>();
            foreach (var pattern in MsmdsrvCopyInclude)
            {
                foreach (var file in Directory.EnumerateFiles(basePath, pattern, SearchOption.AllDirectories))
                {
                    files.Add(file);
                }
            }

            var copyDest = GetShadowCopyDir(winAppHandle);

            foreach (var file in files)
            {
                var destPath = Path.Combine(copyDest, file.Substring(basePath.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(file, destPath, overwrite: true);
                Log.Verbose("File copied: {Path}", destPath);
            }

            return Path.Combine(copyDest, MSMDSRV_EXE);
        }

        public string GetEffectivePowerBiInstallDir() => _pbiInstall.Value.InstallDir;

    }

    public class PowerBIDesktopInstallation
    {
        public string ProductVersion { get; set; }
        public bool Is64Bit { get; set; }
        public string InstallDir { get; set; }
    }

    public class PowerBILocator
    {
        private const string PBIDesktop_exe = "PBIDesktop.exe";
        private static readonly ILogger Log = Serilog.Log.ForContext<PowerBILocator>();

        public static PowerBIDesktopInstallation[] FindInstallations()
        {
            return
                GetWindowsStoreInstalls()
                    .Concat(GetInstallerInstalls())
                    .ToArray();
        }

        private static readonly string StoreInstallKey =
            @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Packages";

        internal static IEnumerable<PowerBIDesktopInstallation> GetWindowsStoreInstalls()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(StoreInstallKey, writable: false))
            {
                if (key != null)
                {
                    var names = key.GetSubKeyNames();
                    foreach (var name in names.Where(n => n.StartsWith("Microsoft.MicrosoftPowerBIDesktop")))
                    {
                        using (var subKey = key.OpenSubKey(name, writable: false))
                        {
                            var path = Path.Combine((string)subKey?.GetValue("Path") ?? "", "bin", PBIDesktop_exe);
                            if (File.Exists(path))
                            {
                                var fileInfo = FileVersionInfo.GetVersionInfo(path);
                                yield return new PowerBIDesktopInstallation
                                {
                                    InstallDir = Path.GetDirectoryName(fileInfo.FileName),
                                    Is64Bit = fileInfo.FileName.Contains("x64"),
                                    ProductVersion = fileInfo.ProductVersion,
                                };
                            }
                        }
                    }
                }
            }
        }

        private static readonly string[] InstallerKeys = // TODO 32/64?
        {
            @"SOFTWARE\Microsoft\Microsoft Power BI Desktop\Installer",
            @"SOFTWARE\WOW6432Node\Microsoft\Microsoft Power BI Desktop\Installer",
        };

        internal static IEnumerable<PowerBIDesktopInstallation> GetInstallerInstalls()
        {
            foreach (var baseKey in InstallerKeys)
            using (var key = Registry.LocalMachine.OpenSubKey(baseKey, writable: false))
            {
                var path = Path.Combine((string)key?.GetValue("InstallPath") ?? "", "bin", PBIDesktop_exe);
                if (File.Exists(path))
                {
                    var fileInfo = FileVersionInfo.GetVersionInfo(path);
                    var win64 = (string)key.GetValue("Win64Install");
                    yield return new PowerBIDesktopInstallation
                    {
                        InstallDir = Path.GetDirectoryName(fileInfo.FileName),
                        Is64Bit = win64.Equals("yes", StringComparison.OrdinalIgnoreCase),
                        ProductVersion = fileInfo.ProductVersion,
                    };
                }
            }
        }

    }
}
