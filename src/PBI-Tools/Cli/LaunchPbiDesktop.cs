// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.IO;
using System.Linq;
using PowerArgs;

namespace PbiTools.Cli
{
    using Utils;

    public partial class CmdLineActions
    {

#if NETFRAMEWORK
        [ArgActionMethod, ArgShortcut("launch-pbi")]
        [ArgDescription("Starts a new instance of Power BI Desktop with the PBIX/PBIT file specified. Does not support Windows Store installations.")]
        public void LaunchPbiDesktop(
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX or PBIT file.")]
                string pbixPath
        )
        {
            var defaultInstall = _dependenciesResolver.PBIInstalls.FirstOrDefault(x => x.Location != PowerBIDesktopInstallationLocation.WindowsStore);
            if (defaultInstall == null) {
                throw new PbiToolsCliException(ExitCode.DependenciesNotInstalled, "No suitable installation found.");
            }
            var pbiExePath = Path.Combine(defaultInstall.InstallDir, "PBIDesktop.exe");
            Log.Verbose("Attempting to start PBI Desktop from: {Path}", pbiExePath);

            var proc = Process.Start(pbiExePath, $"\"{pbixPath}\""); // Note the enclosing quotes are required
            Log.Information("Launched Power BI Desktop, Process ID: {ProcessID}, Arguments: {Arguments}", proc.Id, proc.StartInfo.Arguments);
        }
#endif

    }

}