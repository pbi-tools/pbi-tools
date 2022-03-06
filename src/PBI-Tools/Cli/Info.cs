// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerArgs;
using Serilog.Events;

namespace PbiTools.Cli
{
    using Configuration;
    using Utils;

    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("info")]
        [ArgDescription("Collects diagnostic information about the local system and writes a JSON object to StdOut.")]
        [ArgExample(
            "pbi-tools info check", 
            "Prints information about the active version of pbi-tools, all Power BI Desktop versions on the local system, any running Power BI Desktop instances, and checks the latest version of Power BI Desktop available from Microsoft Downloads.")]
        public void Info(
            [ArgDescription("When specified, checks the latest Power BI Desktop version available from download.microsoft.com.")]
                bool checkDownloadVersion
        )
        {
            using (_appSettings.SetScopedLogLevel(LogEventLevel.Warning))  // Suppresses Informational logs
            {
                var jsonResult = new JObject
                {
                    { "version", AssemblyVersionInformation.AssemblyInformationalVersion },
                    { "edition", AppSettings.Edition },
                    { "build", AssemblyVersionInformation.AssemblyFileVersion },
                    { "pbixProjVersion", ProjectSystem.PbixProject.CurrentVersion.ToString() },
#if NETFRAMEWORK
                    { "pbiBuildVersion", AssemblyVersionInformation.AssemblyMetadata_PBIBuildVersion },
#endif
                    { "amoVersion", typeof(Microsoft.AnalysisServices.Tabular.Server).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                    { "toolPath", Process.GetCurrentProcess().MainModule.FileName },
                    { "locale", new JObject {
                        { "system", $"{System.Globalization.CultureInfo.CurrentCulture.Name} ({System.Globalization.CultureInfo.CurrentCulture.LCID})" },
                        { "ui", $"{System.Globalization.CultureInfo.CurrentUICulture.Name} ({System.Globalization.CultureInfo.CurrentUICulture.LCID})" }
                    }},
                    { "settings", AppSettings.AsJson() },
                    { "runtime", new JObject {
                        { "platform", System.Runtime.InteropServices.RuntimeInformation.OSDescription },
                        { "architecture", System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() },
                        { "framework", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription },
                    }},
#if NETFRAMEWORK
                    { "pbiInstalls", JArray.FromObject(_dependenciesResolver.PBIInstalls) },
                    { "effectivePbiInstallDir", _dependenciesResolver.GetEffectivePowerBiInstallDir() },
                    { "pbiSessions", JArray.FromObject(PowerBIProcesses.EnumerateProcesses().ToArray()) },
#endif
                };

                if (checkDownloadVersion)
                {
                    var downloadInfo = PowerBIDownloader.TryFetchInfo(out var info) ? info : new PowerBIDownloadInfo {};
                    jsonResult.Add("pbiDownloadVersion", JObject.FromObject(downloadInfo));
                }

                using (var writer = new JsonTextWriter(Console.Out))
                {
                    writer.Formatting = Environment.UserInteractive ? Formatting.Indented : Formatting.None;
                    jsonResult.WriteTo(writer);
                }
            }
        }

    }

}