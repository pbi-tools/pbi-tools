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

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using PbiTools.Win32;
using Serilog;

namespace PbiTools.Utils
{
    public class PowerBIProcesses
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PowerBIProcesses>();

        public static IEnumerable<PowerBIProcess> EnumerateProcesses()
        {
            foreach (var proc in Process.GetProcessesByName("msmdsrv")) // TODO Look for "pbidesktop" instead??
            {
                var result = default(PowerBIProcess);

                using (proc)
                try
                {
                    var cmdLine = proc.GetCommandLine().SplitCommandLine();

                    var args = cmdLine.Aggregate(
                        (Dict: new Dictionary<string, string>(), Prev: string.Empty),
                        (acc, s) => 
                        {
                            if (s.StartsWith("-"))
                                acc.Prev = s;
                            else if (!string.IsNullOrEmpty(acc.Prev))
                            {
                                acc.Dict.Add(acc.Prev, s);
                                acc.Prev = string.Empty;
                            }
                            return acc;
                        }, 
                        acc => acc.Dict);

                    var portFilePath = Path.Combine(args["-s"], "msmdsrv.port.txt");
                    var port = File.Exists(portFilePath)
                        ? Convert.ToInt32(File.ReadAllText(portFilePath, Encoding.Unicode))
                        : new int?();

                    using (var parent = proc.GetParent())
                    {
                        var pbixPathCandidates = SystemUtility.GetHandles(new[] { parent.Id })
                            .Where(f => Path.GetExtension(f.DosFilePath ?? "").StartsWith(".pbi") && File.Exists(f.DosFilePath))
                            .ToArray();

                        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                        {
                            Array.ForEach(
                                pbixPathCandidates, 
                                h => Log.Debug("Found PBIx handle: {Path}", h.DosFilePath)
                            );
                        }

                        pbixPathCandidates = pbixPathCandidates.Where(p => !IsTempSavePath(p.DosFilePath)).ToArray();

                        result = new PowerBIProcess
                        {
                            ProductName = parent.MainModule.FileVersionInfo.ProductName,
                            ProductVersion = parent.MainModule.FileVersionInfo.ProductVersion,
                            ExePath = parent.MainModule.FileVersionInfo.FileName,
                            ProcessId = parent.Id,
                            Port = port,
                            WorkspaceName = args["-n"],
                            WorkspaceDir = args["-s"],
                            PbixPath = pbixPathCandidates.FirstOrDefault()?.DosFilePath
                        };
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Log.Verbose(ex, "Skipping process due to access violation. (PID: {ProcessID})", proc.Id);
                }

                if (result != null) yield return result;
            }
        }

        public static bool IsTempSavePath(string path) =>
            path != null
            && path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.InvariantCultureIgnoreCase)
            && path.Contains("TempSaves");

    }

    public class PowerBIProcess
    {
        public string ProductName { get; set; }
        public string ProductVersion { get; set; }
        public string ExePath { get; set; }
        public int ProcessId { get; set; }
        public int? Port { get; set; }
        public string WorkspaceName { get; set; }
        public string WorkspaceDir { get; set; }
        public string PbixPath { get; set; }
    }
}
#endif
