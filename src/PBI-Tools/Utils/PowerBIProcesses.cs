// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using PbiTools.Win32;
using System.Text;

namespace PbiTools.Utils
{
    public class PowerBIProcesses
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PowerBIProcesses>();

        public static IEnumerable<PowerBIProcess> EnumerateProcesses()
        {
            foreach (var proc in Process.GetProcessesByName("msmdsrv"))
            {
                using (proc)
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
                        : new Nullable<int>();

                    using (var parent = proc.GetParent())
                    {
                        var pbixPathCandidates = SystemUtility.GetHandles(new[] { parent.Id })
                            .Where(f => Path.GetExtension(f.DosFilePath ?? "").StartsWith(".pbi"))
                            .ToArray();

                        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                        {
                            Array.ForEach(
                                pbixPathCandidates, 
                                h => Log.Debug("Found PBIx handle: {Path}; Is File: {IsFile}", h.DosFilePath, File.Exists(h.DosFilePath))
                            );
                        }

                        yield return new PowerBIProcess
                        {
                            ProductName = parent.MainModule.FileVersionInfo.ProductName,
                            ProductVersion = parent.MainModule.FileVersionInfo.ProductVersion,
                            ExePath = parent.MainModule.FileVersionInfo.FileName,
                            ProcessId = parent.Id,
                            Port = port,
                            WorkspaceName = args["-n"],
                            WorkspaceDir = args["-s"],
                            PbixPath = pbixPathCandidates.FirstOrDefault(h => File.Exists(h.DosFilePath))?.DosFilePath
                        };
                    }
                }
            }

        }
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
