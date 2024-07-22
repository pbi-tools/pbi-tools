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
        [ArgDescription("Starts a new instance of Power BI Desktop, optionally loading a specified PBIX/PBIT file. Does not support Windows Store installations.")]
        public void LaunchPbiDesktop(
            [ArgDescription("The path to an existing PBIX or PBIT file.")]
                string pbixPath
        )
        {
            var defaultInstall = _dependenciesResolver.PBIInstalls.FirstOrDefault(x => x.Location != PowerBIDesktopInstallationLocation.WindowsStore);
            if (defaultInstall == null) {
                throw new PbiToolsCliException(ExitCode.DependenciesNotInstalled, "No suitable installation found. Windows Store installs cannot be launched using this command.");
            }
            
            var pbiExePath = Path.Combine(defaultInstall.InstallDir, "PBIDesktop.exe");
            Log.Verbose("Attempting to start PBI Desktop from: {Path}", pbiExePath);

            var proc = !string.IsNullOrEmpty(pbixPath) && (new FileInfo(pbixPath).Exists)
                ? Process.Start(pbiExePath, $"\"{pbixPath}\"")
                : Process.Start(pbiExePath);

            Log.Information("Launched Power BI Desktop, Process ID: {ProcessID}, Arguments: {Arguments}", proc.Id, proc.StartInfo.Arguments);
        }
#endif

    }

}
