// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Linq;
using PowerArgs;

namespace PbiTools.Cli
{
    using System.Threading;
    using Model;
    using PowerBI;
    using ProjectSystem;
    using Utils;

    public partial class CmdLineActions
    {

#if NETFRAMEWORK
        [ArgActionMethod, ArgShortcut("extract")]
        [ArgDescription("Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.")]
        [ArgExample(
            @"pbi-tools extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw", 
            "Extracts the PBIX file into the specified extraction folder (relative to the current working dir), using the 'Auto' compatibility mode. The model part is serialialized using Raw mode.",
            Title = "Extract: Custom folder and settings")]
        [ArgExample(
            @"pbi-tools extract '.\data\Samples\Adventure Works DW 2020.pbix'", 
            "Extracts the specified PBIX file into the default extraction folder (relative to the PBIX file location), using the 'Auto' compatibility mode. Any settings specified in the '.pbixproj.json' file already present in the destination folder will be honored.",
            Title = "Extract: Default")]
        public void Extract(
            [ArgRequired(IfNot = "pid"), ArgExistingFile, ArgDescription("The path to an existing PBIX file.")]
                string pbixPath,
            [ArgRequired(IfNot = "pbixPath"), ArgDescription("The Power BI Desktop process ID to extract sources from (look up via 'pbi-tools info').")]
                int? pid,
            [ArgDescription("The port number from a running Power BI Desktop instance (look up via 'pbi-tools info'). When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files.")]
            [ArgCantBeCombinedWith("pid"), ArgRange(1024, 65535)]
                int? pbiPort,
            [ArgDescription("The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory.")]
                string extractFolder,
            [ArgDescription("The extraction mode."), ArgDefaultValue(ExtractActionCompatibilityMode.Auto)]
                ExtractActionCompatibilityMode mode,
            [ArgDescription("The model serialization mode.")]
                ModelSerializationMode modelSerialization,
            [ArgDescription("The mashup serialization mode.")]
                MashupSerializationMode mashupSerialization,
            [ArgDescription("Enables watch mode. Monitors the PBIX file open in a Power BI Desktop session, and extracts sources each time the file is saved.")]
                bool watch
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            // ReportSerializationMode: Default, Raw, Enhanced

            PowerBIProcess pbiProc = default;

            if (pid.HasValue) {
                pbiProc = PowerBIProcesses.EnumerateProcesses().FirstOrDefault(p => p.ProcessId == pid);
                if (pbiProc == null)
                    throw new PbiToolsCliException(ExitCode.InvalidArgs, $"No running Power BI Desktop instance with PID = {pid} found.");
                if (pbiProc.PbixPath == null)
                    throw new PbiToolsCliException(ExitCode.PathNotFound, $"Could not detect PBIX file path for Power BI Desktop instance with PID = {pid}.");

                Log.Information("Extracting sources from file at {PbixPath} and local AS instance at Port: {Port}.", pbiProc.PbixPath, pbiProc.Port);

                pbixPath = pbiProc.PbixPath;
                pbiPort = pbiProc.Port;
            }

            var targetFolder = String.IsNullOrEmpty(extractFolder) 
                ? null 
                : new DirectoryInfo(extractFolder).FullName;

            if (watch && pid.HasValue) {
                if (mode == ExtractActionCompatibilityMode.Legacy)
                    throw new PbiToolsCliException(ExitCode.InvalidArgs, "The 'Legacy' extraction mode is not supported in watch mode.");
                ExtractWatchSession(pbiProc, targetFolder, modelSerialization, mashupSerialization);
                return;
            }
            else if (watch) {
                throw new NotImplementedException("Watch mode is currently only supported with a Process ID specified.");
            }

            using (var reader = new PbixReader(pbixPath, _dependenciesResolver))
            {
                if (mode == ExtractActionCompatibilityMode.Legacy)
                {
                    // TODO Check -pbiPort not specified
                    using (var extractor = new Actions.PbixExtractAction(reader))
                    {
                        extractor.ExtractAll();
                    }
                }
                else
                {
                    try
                    {
                        using (var model = PbixModel.FromReader(reader, targetFolder, pbiPort))
                        {
                            if (modelSerialization != default)
                                model.PbixProj.Settings.Model.SerializationMode = modelSerialization;

                            if (mashupSerialization != default)
                                model.PbixProj.Settings.Mashup.SerializationMode = mashupSerialization;

                            model.ToFolder(path: targetFolder);
                        }
                    }
                    catch (NotSupportedException) when (mode == ExtractActionCompatibilityMode.Auto)
                    {
                        using (var extractor = new Actions.PbixExtractAction(reader))
                        {
                            extractor.ExtractAll();
                        }
                    }
                }
            }

            Console.WriteLine($"Completed in {_stopWatch.Elapsed}.");
        }

        private void ExtractWatchSession(
            PowerBIProcess pbiProc,
            string targetFolder,
            ModelSerializationMode modelSerialization,
            MashupSerializationMode mashupSerialization
        )
        {
            // Get session details: path, port
            // Monitor process lifetime
            // Start watcher
            // OnChange: Extract

            void ExtractAction() {
                try
                {
                    using (var reader = new PbixReader(pbiProc.PbixPath, _dependenciesResolver))
                    using (var model = PbixModel.FromReader(reader, targetFolder, pbiProc.Port))
                    {
                        if (modelSerialization != default)
                            model.PbixProj.Settings.Model.SerializationMode = modelSerialization;

                        if (mashupSerialization != default)
                            model.PbixProj.Settings.Mashup.SerializationMode = mashupSerialization;

                        model.ToFolder(path: targetFolder);
                    }
                }
                catch (Exception ex) {
                    Log.Warning(ex, "An unhandled error occurred.");
                }
                Log.Information("Watching for changes... (Exit via CTRL+C)");
            };

            Log.Information("Entering WATCH mode... (Exit via CTRL+C)");

            using (var cts = new CancellationTokenSource())
            using (var waitToken = new ManualResetEventSlim())
            using (var proc = System.Diagnostics.Process.GetProcessById(pbiProc.ProcessId))
            using (var watcher = new FileWatcher(pbiProc.PbixPath, ExtractAction, cts.Token))
            {
                proc.Exited += (sender, e) => {
                    Log.Information("The Power BI Desktop session has ended. Stopping watch mode.");
                    cts.Cancel();
                    waitToken.Set();
                };
                proc.EnableRaisingEvents = true;

                watcher.FileDeleted += (sender, e) => {
                    Log.Information("The PBIX file was deleted. Stopping watch mode.");
                    cts.Cancel();
                    waitToken.Set();
                };
                watcher.FileRenamed += (sender, e) => {
                    Log.Information("The PBIX file was renamed. Stopping watch mode.");
                    cts.Cancel();
                    waitToken.Set();
                };

                Console.CancelKeyPress += (sender, e) => {
                    Log.Information("CTRL+C detected. Stopping watch mode.");
                    e.Cancel = true;
                    cts.Cancel();
                    waitToken.Set();
                };

                waitToken.Wait();
            }
        }

        private void ExtractWatchPath(
            string pbixPath,
            int? pbiPort,
            string extractFolder,
            ModelSerializationMode modelSerialization,
            MashupSerializationMode mashupSerialization
        )
        {
            // Monitor CTRL+C
            // Start watcher
            // OnChange: Extract

            throw new NotImplementedException();
        }
#endif

    }

    public enum ExtractActionCompatibilityMode
    {
        [ArgDescription("Attempts extraction using the V3 model, and falls back to Legacy mode in case the PBIX file does not have V3 format.")]
        Auto = 0, 
        [ArgDescription("Extracts V3 PBIX files only. Fails if the file provided has a legacy format.")]
        V3,
        [ArgDescription("Extracts legacy PBIX files only. Fails if the file provided has the V3 format.")]
        Legacy
    }
}