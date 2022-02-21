// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;
using PeNet;
using Serilog;

namespace PbiTools.Utils
{
    using Configuration;
    
    public class PowerBILocator
    {
        private const string PBIDesktop_exe = "PBIDesktop.exe";
        private static readonly ILogger Log = Serilog.Log.ForContext<PowerBILocator>();

        public static PowerBIDesktopInstallation[] FindInstallations()
        {
            var installs = new List<PowerBIDesktopInstallation>();
            if (TryFindCustomInstallation(out var customInstall))
            {
                installs.Add(customInstall);
            }
            installs.AddRange(GetWindowsStoreInstalls());
            installs.AddRange(GetInstallerInstalls());
            return installs.ToArray();
        }

        public static bool TryFindCustomInstallation(string path, out PowerBIDesktopInstallation install)
        {
            install = null;
            try
            {
                if (!Directory.Exists(path)) return false;

                var pbiExePath = Directory.EnumerateFiles(path, PBIDesktop_exe, SearchOption.AllDirectories).FirstOrDefault();
                if (pbiExePath == null) return false;

                var fileInfo = FileVersionInfo.GetVersionInfo(pbiExePath);
                using (var peFile = new PeNet.FileParser.StreamFile(File.OpenRead(fileInfo.FileName)))
                {
                    install = new PowerBIDesktopInstallation
                    {
                        InstallDir = Path.GetDirectoryName(fileInfo.FileName),
                        Is64Bit = peFile.Is64Bit(),
                        ProductVersion = fileInfo.ProductVersion,
                        Version = ParseProductVersion(fileInfo.ProductVersion),
                        Location = PowerBIDesktopInstallationLocation.Custom
                    };
                    Log.Verbose("Located Power BI Desktop custom install at {Path}", pbiExePath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "An error occurred when parsing custom PBI install location at {Path}", path);
                return false;
            }
        }

        public static bool TryFindCustomInstallation(out PowerBIDesktopInstallation install)
        {
            var envPbiInstallDir = AppSettings.GetEnvironmentSetting(AppSettings.Environment.PbiInstallDir);
            if (!string.IsNullOrEmpty(envPbiInstallDir))
                return TryFindCustomInstallation(envPbiInstallDir, out install);
            else
            {
                install = null;
                return false;
            }
        }

        private static readonly string StoreInstallKey =
            @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Packages";

        internal static IEnumerable<PowerBIDesktopInstallation> GetWindowsStoreInstalls()
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Microsoft\\Power BI Desktop Store App");
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
                                    SettingsDir = settingsPath,
                                    Is64Bit = fileInfo.FileName.Contains("x64"),
                                    ProductVersion = fileInfo.ProductVersion,
                                    Version = ParseProductVersion(fileInfo.ProductVersion),
                                    Location = PowerBIDesktopInstallationLocation.WindowsStore,
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
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Power BI Desktop");
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
                        SettingsDir = settingsPath,
                        Is64Bit = win64.Equals("yes", StringComparison.OrdinalIgnoreCase),
                        ProductVersion = fileInfo.ProductVersion,
                        Version = ParseProductVersion(fileInfo.ProductVersion),
                        Location = PowerBIDesktopInstallationLocation.Installer,
                    };
                }
            }
        }

        internal static Version ParseProductVersion(string versionString)
        {
            if (Version.TryParse(versionString.Split(' ')[0], out var version))
                return version;
            else return new Version();
        }

        private static readonly Guid V3ModelFeatureGuid = new Guid("C15D05E2-F1C1-4F62-94B2-0F179E080741");

        internal static bool TryGetFeatureSwitch(string settingsPath, Guid featureId, out bool enabled)
        {
            enabled = false;
            try
            {
                var userSettingsPath = Path.Combine(settingsPath, "User.zip");
                Log.Verbose("Attempting to read feature switches from {UserSettingsPath}", userSettingsPath);

                if (!File.Exists(userSettingsPath)) return false;

                using (var file = File.OpenRead(userSettingsPath))
                using (var zip = new ZipArchive(file))
                {
                    var entry = zip.Entries.FirstOrDefault(e => e.FullName == "FeatureSwitches/FeatureSwitches.xml");
                    if (entry == null) return false;

                    using (var stream = entry.Open())
                    {
                        var xml = XDocument.Load(stream);
                        var xEntry = xml.XPathSelectElement($"//Entry[@Type='{featureId}']");
                        if (xEntry != null)
                        {
                            enabled = xEntry.Attribute("Value").Value.Contains("1");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Unexpected exception trying to detect V3 Model feature switch.");
            }

            return false;
        }

    }
}
#endif