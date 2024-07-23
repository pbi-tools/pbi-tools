/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;

namespace PbiTools.Utils
{
    using Configuration;

    public interface IDependenciesResolver
    {
        bool TryFindMsmdsrv(out string path);
        string ShadowCopyMsmdsrv(string path);
        string GetEffectivePowerBiInstallDir();
        PowerBIDesktopInstallation[] PBIInstalls { get; }
    }

    public partial class DependenciesResolver
    { 
        private static IDependenciesResolver _defaultInstance;
        private static object _syncObj = new object();

        public static IDependenciesResolver Default
        {
            get
            {
                lock (_syncObj)
                { 
                    if (_defaultInstance == null)
#if NETFRAMEWORK
                        _defaultInstance = new DependenciesResolver();
#elif NET
                        _defaultInstance = new NetCoreDependenciesResolver();
#endif
                    return _defaultInstance;
                }
            }
            set
            {
                _defaultInstance = value;
            }
        }
    }


#if NETFRAMEWORK
    public partial class DependenciesResolver : IDependenciesResolver
    {
        private const string MSMDSRV_EXE = "msmdsrv.exe";

        private static readonly ILogger Log = Serilog.Log.ForContext<DependenciesResolver>();


        private readonly Lazy<PowerBIDesktopInstallation[]> _pbiInstalls = new Lazy<PowerBIDesktopInstallation[]>(PowerBILocator.FindInstallations);
        private PowerBIDesktopInstallation _effectivePbiInstall;

        public PowerBIDesktopInstallation[] PBIInstalls => _pbiInstalls.Value;



        public DependenciesResolver() // TODO Initialize with PbixProj reference (which has settings)
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve(this);
        }

        private PowerBIDesktopInstallation GetPBIInstall()
        {
            // TODO Allow to customize preference: Custom|WindowsStore|Installer
            var allInstalls = _pbiInstalls.Value;
            var install = allInstalls
                .Where(x => x.Is64Bit && x.Location == PowerBIDesktopInstallationLocation.Custom)
                .OrderByDescending(x => x.Version)        // Use highest version installed
                .FirstOrDefault();
                
            if (install == null)
            {
                install = allInstalls
                    .Where(x => x.Is64Bit && x.Location != PowerBIDesktopInstallationLocation.Custom)
                    .OrderByDescending(x => x.Version)        // Use highest version installed
                    .FirstOrDefault();

                if (install == null) throw new Exception("No 64-bit Power BI Desktop installation found"); // TODO Make specific exception
            }

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
                var path = Path.Combine(GetEffectivePowerBiInstallDir(), $"{dllName}.dll");
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

        private static string GetWinAppHandle(string path) => path.Split(Path.DirectorySeparatorChar)
            .FirstOrDefault(s => s.StartsWith("Microsoft.MicrosoftPowerBIDesktop"));

        public bool TryFindMsmdsrv(out string path)
        {
            // a standard user has no execute permissions for msmdsrv.exe in the Windows Store install dir
            // need to shadow-copy MSDMSRV and dependencies into %LOCALAPPDATA%

            path = Path.Combine(
                GetEffectivePowerBiInstallDir(), // "C:\\Program Files\\WindowsApps\\Microsoft.MicrosoftPowerBIDesktop_2.56.5023.0_x64__8wekyb3d8bbwe\\bin"
                MSMDSRV_EXE
            );

            var winAppHandle = GetWinAppHandle(path);
            if (winAppHandle != null)
            {
                var shadowCopyPath = Path.Combine(
                    ApplicationFolders.GetShadowCopyDir(winAppHandle),
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
            "tm*.dll",
            "x*.dll",
            "tbb*.dll",
            "FlightRecorderTraceDef.xml",
            "Microsoft.Data*",
            "Microsoft.Mashup*",
            "Microsoft.Spatial*",
            "PowerBIExtensions*"
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

            var copyDest = ApplicationFolders.GetShadowCopyDir(winAppHandle);

            foreach (var file in files)
            {
                var destPath = Path.Combine(copyDest, file.Substring(basePath.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(file, destPath, overwrite: true);
                Log.Verbose("File copied: {Path}", destPath);
            }

            return Path.Combine(copyDest, MSMDSRV_EXE);
        }

        public string GetEffectivePowerBiInstallDir()
        {
            if (_effectivePbiInstall == null)
            {
                _effectivePbiInstall = GetPBIInstall();
            }
            return _effectivePbiInstall.InstallDir;
        }

        public static void LoadExternalAmoLibraries()
        {
            if (Env.TryGetEnvironmentSetting(Env.ExternalAmoPath, out var externalAmoPath)) 
            {
                var dirInfo = new DirectoryInfo(externalAmoPath);
                if (!dirInfo.Exists)
                {
                    Console.WriteLine($"[WRN] Cannot resolve directory from AppSetting '{Env.ExternalAmoPath}': [{externalAmoPath}]");
                    return;
                }

                foreach (var file in dirInfo.GetFiles("Microsoft.AnalysisServices*.dll", SearchOption.TopDirectoryOnly).OrderBy(x => x.Name))
                {
                    try
                    {
                        Assembly.LoadFrom(file.FullName);

                        Console.WriteLine($"External assembly loaded: {file.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load assembly: {file.FullName}");
                        Console.WriteLine(ex.ToString());
                    }

                }
            }
        }
    }
#endif

#if NET
    internal class NetCoreDependenciesResolver : IDependenciesResolver
    {
        PowerBIDesktopInstallation[] IDependenciesResolver.PBIInstalls => throw new NotImplementedException();

        string IDependenciesResolver.GetEffectivePowerBiInstallDir()
        {
            throw new PlatformNotSupportedException("Not supported in the pbi-tools Core version.");
        }

        string IDependenciesResolver.ShadowCopyMsmdsrv(string path)
        {
            throw new PlatformNotSupportedException("Not supported in the pbi-tools Core version.");
        }

        bool IDependenciesResolver.TryFindMsmdsrv(out string path)
        {
            throw new PlatformNotSupportedException("Not supported in the pbi-tools Core version.");
        }
    }
#endif
}
