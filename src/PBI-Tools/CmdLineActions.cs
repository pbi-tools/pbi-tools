// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PbiTools.Actions;
using PbiTools.PowerBI;
using PbiTools.Rpc;
using PbiTools.Utils;
using PowerArgs;
using Serilog;
using Serilog.Events;

namespace PbiTools
{
#if !DEBUG
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]  // PowerArgs will print the user friendly error message as well as the auto-generated usage documentation for the program.
#endif
    [ArgDescription(AssemblyVersionInformation.AssemblyProduct + ", " + AssemblyVersionInformation.AssemblyInformationalVersion)]
    [ApplyDefinitionTransforms]
    public class CmdLineActions
    {

        private static readonly ILogger Log = Serilog.Log.ForContext<CmdLineActions>();
        
        private readonly IDependenciesResolver _dependenciesResolver = DependenciesResolver.Default;
        private readonly AppSettings _appSettings;
        private readonly Stopwatch _stopWatch = Stopwatch.StartNew();

        public CmdLineActions() : this(Program.AppSettings)
        {
        }

        public CmdLineActions(AppSettings appSettings)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        }



        [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
        public bool Help { get; set; }




        [ArgActionMethod, ArgShortcut("extract")]
        [ArgDescription("Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.")]
        [ArgExample(
            @"pbi-tools.exe extract '.\data\Samples\Adventure Works DW 2020.pbix' -extractFolder '.\data\Samples\Adventure Works DW 2020 - Raw' -modelSerialization Raw", 
            "Extracts the PBIX file into the specified extraction folder (relative to the current working dir), using the 'Auto' compatibility mode. The model part is serialialized using Raw mode.",
            Title = "Extract: Custom folder and settings")]
        [ArgExample(
            @"pbi-tools.exe extract '.\data\Samples\Adventure Works DW 2020.pbix'", 
            "Extracts the specified PBIX file into the default extraction folder (relative to the PBIX file location), using the 'Auto' compatibility mode. Any settings specified in the '.pbixproj.json' file already present in the destination folder will be honored.",
            Title = "Extract: Default")]
        public void Extract(
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX file.")] string pbixPath,
            [ArgDescription("The port number from a running Power BI Desktop instance. When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files."), ArgRange(1024, 65535)] int pbiPort,
            [ArgDescription("The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory.")] string extractFolder,
            [ArgDescription("The extraction mode."), ArgDefaultValue(ExtractActionCompatibilityMode.Auto)] ExtractActionCompatibilityMode mode,
            [ArgDescription("The model serialization mode.")] ProjectSystem.ModelSerializationMode modelSerialization,
            [ArgDescription("The mashup serialization mode.")] ProjectSystem.MashupSerializationMode mashupSerialization
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            // ReportSerializationMode: Full,ExtractObjets, Raw

            using (var reader = new PbixReader(pbixPath, _dependenciesResolver))
            {
                if (mode == ExtractActionCompatibilityMode.Legacy)
                {
                    using (var extractor = new PbixExtractAction(reader))
                    {
                        extractor.ExtractAll();
                    }
                }
                else
                {
                    try
                    {
                        var targetFolder = String.IsNullOrEmpty(extractFolder) 
                            ? null 
                            : new DirectoryInfo(extractFolder).FullName;
                        
                        using (var model = Model.PbixModel.FromReader(reader, targetFolder, pbiPort >= 1024 ? pbiPort : null))
                        {
                            if (modelSerialization != default(ProjectSystem.ModelSerializationMode))
                                model.PbixProj.Settings.Model.SerializationMode = modelSerialization;

                            if (mashupSerialization != default(ProjectSystem.MashupSerializationMode))
                                model.PbixProj.Settings.Mashup.SerializationMode = mashupSerialization;

                            model.ToFolder(path: targetFolder);
                        }
                    }
                    catch (NotSupportedException) when (mode == ExtractActionCompatibilityMode.Auto)
                    {
                        using (var extractor = new PbixExtractAction(reader))
                        {
                            extractor.ExtractAll();
                        }
                    }
                }
            }

            Console.WriteLine($"Completed in {_stopWatch.Elapsed}.");
        }


        [ArgActionMethod, ArgShortcut("extract-data"), ArgDescription("Extract data from all tables in a tabular model, either from within a PBIX file, or from a live session.")]
        [ArgExample(
            "pbi-tools.exe extract-data -port 12345", 
            "Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory.",
            Title = "Extract data from local workspace instance")]
        [ArgExample(
            @"pbi-tools.exe extract-data -pbixPath '.\data\Samples\Adventure Works DW 2020.pbix'", 
            "Extracts all records from each table from the model embedded in the specified PBIX file. Each table is extracted into a UTF-8 CSV file with the same name into the current working directory.",
            Title = "Extract data from offline PBIX file")]
        public void ExtractData(
            [ArgCantBeCombinedWith("pbixPath"), ArgDescription("The port number of a local Tabular Server instance.")] int port,
            [ArgRequired(IfNot = "port"), ArgExistingFile, ArgDescription("The PBIX file to extract data from.")] string pbixPath,
            [ArgDescription("The output directory. Uses working directory if not provided.")] string outPath,
            [ArgDescription("The format to use for DateTime values. Must be a valid .Net format string."), ArgDefaultValue("s")] string dateTimeFormat
        )
        {
            if (outPath == null && pbixPath != null)
                outPath = Path.GetDirectoryName(pbixPath);
            else if (outPath == null)
                outPath = Environment.CurrentDirectory;

            Log.Verbose("Port: {Port}, Path: {PbixPath}, OutPath: {OutPath}", port, pbixPath, outPath);

            if (pbixPath != null)
            {
                using (var file = File.OpenRead(pbixPath))
                using (var package = Microsoft.PowerBI.Packaging.PowerBIPackager.Open(file, skipValidation: true))
                using (var msmdsrv = new AnalysisServicesServer(new ASInstanceConfig
                {
                    DeploymentMode = DeploymentMode.SharePoint,
                    DisklessModeRequested = true,
                    EnableDisklessTMImageSave = true,
                }, _dependenciesResolver))
                {
                    msmdsrv.HideWindow = true;

                    msmdsrv.Start();
                    msmdsrv.LoadPbixModel(package.DataModel, "Model", "Model");

                    using (var reader = new TabularModel.TabularDataReader(msmdsrv.OleDbConnectionString))
                    {
                        reader.ExtractTableData(outPath, dateTimeFormat);
                    }
                }
            }
            else
            {
                using (var reader = new TabularModel.TabularDataReader($"Provider=MSOLAP;Data Source=.:{port};"))
                {
                    reader.ExtractTableData(outPath, dateTimeFormat);
                }
            }
        }


        [ArgActionMethod, ArgShortcut("export-bim"), ArgDescription("Converts the Model artifacts to a TMSL/BIM file.")]
        public void ExportBim(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder to export the BIM file from.")] string folder,
            [ArgDescription("Do not generate model data sources. The is required for deployment to Power BI Premium via the XMLA endpoint.")] bool skipDataSources,
            [ArgDescription("List transformations to be applied to TMSL document.")] ExportTransforms transforms
        )
        {
            using (var rootFolder = new FileSystem.ProjectRootFolder(folder))
            {
                var serializer = new Serialization.TabularModelSerializer(rootFolder, ProjectSystem.PbixProject.FromFolder(rootFolder).Settings.Model);
                if (serializer.TryDeserialize(out var db))  // throws for V1 models
                {
                    if (!skipDataSources)
                    {
                        var dataSources = TabularModel.TabularModelConversions.GenerateDataSources(db);
                        db["model"]["dataSources"] = dataSources;
                    }

                    if (transforms.HasFlag(ExportTransforms.RemovePBIDataSourceVersion))
                    {
                        db["model"]["defaultPowerBIDataSourceVersion"]?.Parent.Remove();
                    }

                    var path = Path.GetFullPath(Path.Combine(folder, "..", $"{Path.GetFileName(folder)}.bim"));
                    using (var writer = new JsonTextWriter(File.CreateText(path)))
                    {
                        writer.Formatting = Formatting.Indented;
                        db.WriteTo(writer);
                    }

                    Console.WriteLine($"BIM file written to: {path}");
                }
                else
                {
                    // TODO Fail action?
                    Console.WriteLine("A BIM file could not be exported.");
                }
            }
        }


        [ArgActionMethod, ArgShortcut("compile-pbix"), ArgDescription("*EXPERIMENTAL* Generates a PBIX/PBIT file from sources in the specified PbixProj folder. Currently, only PBIX projects with a live connection are supported.")]
        public void CompilePbix(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder to generate the PBIX from.")] string folder,
            [ArgDescription("The path for the output file. If not provided, creates the file in the current working directory, using the foldername.")] string pbixPath,
            [ArgDescription("The target file format."), ArgDefaultValue(PbiFileFormat.Pbix)] PbiFileFormat format,
            [ArgDescription("Overwrite the destination file if it already exists, fail otherwise.")] bool overwrite
        )
        {
            // format: pbix, pbit
            // mode: Create, Merge
            // mashupHandling: Auto, Skip, GenerateFromModel, FromFolder

            // SUCCESS
            // [x] PBIX from Report-Only
            // [x] PBIT from PBIT sources (incl Mashup)
            //
            // TODO
            // [ ] PBIT from PBIX sources (no mashup)
            // [ ] PBIX from source with model
            // [ ] Merge into PBIX

            using (var proj = PbiTools.Model.PbixModel.FromFolder(folder))
            {
                if (String.IsNullOrEmpty(pbixPath))
                    pbixPath = $"{new DirectoryInfo(proj.SourcePath).Name}.{(format == PbiFileFormat.Pbit ? "pbit" : "pbix")}";

                if (File.Exists(pbixPath) && !overwrite)
                    throw new Exception($"Destination file '{pbixPath}' exists and the '-overwrite' was not specified.");

                proj.ToFile(pbixPath, format, _dependenciesResolver);
            }

            Console.WriteLine($"PBIX file written to: {new FileInfo(pbixPath).FullName}");
        }


        [ArgActionMethod, ArgShortcut("launch-pbi"), ArgDescription("Starts a new instance of Power BI Desktop with the PBIX/PBIT file specified. Does not support Windows Store installations.")]
        public void LaunchPbiDesktop(
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX or PBIT file.")] string pbixPath
        )
        {
            var defaultInstall = _dependenciesResolver.PBIInstalls.FirstOrDefault(x => x.Location != PowerBIDesktopInstallationLocation.WindowsStore);
            if (defaultInstall == null) {
                throw new Exception("No suitable installation found.");
            }
            var pbiExePath = Path.Combine(defaultInstall.InstallDir, "PBIDesktop.exe");
            Log.Verbose("Attempting to start PBI Desktop from: {Path}", pbiExePath);

            var proc = Process.Start(pbiExePath, $"\"{pbixPath}\""); // Note the enclosing quotes are required
            Log.Information("Launched Power BI Desktop, Process ID: {ProcessID}, Arguments: {Arguments}", proc.Id, proc.StartInfo.Arguments);
        }


        [ArgActionMethod, ArgShortcut("info"), ArgDescription("Collects diagnostic information about the local system and writes a JSON object to StdOut.")]
        [ArgExample(
            "pbi-tools.exe info check", 
            "Prints information about the active version of pbi-tools, all Power BI Desktop versions on the local system, any running Power BI Desktop instances, and checks the latest version of Power BI Desktop available from Microsoft Downloads.")]
        public void Info(
            [ArgDescription("When specified, checks the latest Power BI Desktop version available from download.microsoft.com.")] bool checkDownloadVersion
        )
        {
            using (_appSettings.SetScopedLogLevel(LogEventLevel.Warning))  // Suppresses Informational logs
            {
                var jsonResult = new JObject
                {
                    { "version", AssemblyVersionInformation.AssemblyInformationalVersion },
                    { "build", AssemblyVersionInformation.AssemblyFileVersion },
                    { "pbiBuildVersion", AssemblyVersionInformation.AssemblyMetadata_PBIBuildVersion },
                    { "amoVersion", typeof(Microsoft.AnalysisServices.Tabular.Server).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                    { "settings", new JObject {
                        { AppSettings.Environment.LogLevel, AppSettings.GetEnvironmentSetting(AppSettings.Environment.LogLevel) },
                        { AppSettings.Environment.PbiInstallDir, AppSettings.GetEnvironmentSetting(AppSettings.Environment.PbiInstallDir) },
                    }},
                    { "pbiInstalls", JArray.FromObject(_dependenciesResolver.PBIInstalls) },
                    { "effectivePbiInstallDir", _dependenciesResolver.GetEffectivePowerBiInstallDir() },
                    { "pbiSessions", JArray.FromObject(PowerBIProcesses.EnumerateProcesses().ToArray()) },
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


        [ArgActionMethod, ArgShortcut("cache"), ArgDescription("Manages the internal assembly cache.")]
        [ArgExample("pbi-tools.exe cache list", "Lists all cache folders present in the current user profile.")]
        public void Cache(
            [ArgRequired, ArgDescription("The cache action to perform.")] CacheAction action
        )
        {
            var folders = Directory.GetDirectories(ApplicationFolders.AppDataFolder);
            
            switch (action)
            {
                case CacheAction.List:
                    Array.ForEach(folders, f =>
                        Console.WriteLine($"- {Path.GetFileName(f)}")
                    );
                    break;
                case CacheAction.ClearAll:
                    Array.ForEach(folders, f => 
                    {
                        Directory.Delete(f, recursive: true);
                        Console.WriteLine($"Deleted: {Path.GetFileName(f)}");
                    });
                    break;
                case CacheAction.ClearOutdated:
                    Array.ForEach(folders.OrderByDescending(x => x).Skip(1).ToArray(), f => 
                    {
                        Directory.Delete(f, recursive: true);
                        Console.WriteLine($"Deleted: {Path.GetFileName(f)}");
                    });
                    break;
            }
        }


        [ArgActionMethod, ArgShortcut("start-server"), OmitFromUsageDocs]
        public void StartJsonRpcServer()
        {
            using (_appSettings.SuppressConsoleLogs())
            using (var cts = new CancellationTokenSource())
            {
                if (Environment.UserInteractive)
                {
                    Console.CancelKeyPress += (sender,e) => {
                        e.Cancel = true; // intercept Ctrl+C
                        cts.Cancel();
                    };
                }

                using (var rpcServer = RpcServer.Start(Console.OpenStandardOutput, Console.OpenStandardInput, cts))
                {
                    cts.Token.WaitHandle.WaitOne(); // waits until cancel key pressed, RpcServer disconnected, or exit message sent
                }
            }

            /* OmniSharp sample server:

            var server = new LanguageServer(Console.OpenStandardInput(), Console.OpenStandardOutput(), new LoggerFactory());

            server.AddHandler(new TextDocumentHandler(server));

            await server.Initialize();
            await server.WaitForExit;

             */
        }


        [ArgActionMethod, ArgShortcut("export-usage"), OmitFromUsageDocs]
        public void ExportUsage(
            [ArgDescription("The optional path to a file to write into. Prints to console if not provided.")] string outPath
        )
        {
            var sb = new StringBuilder();
            
            var definitions = CmdLineArgumentsDefinitionExtensions.For<CmdLineActions>().RemoveAutoAliases();

            sb.AppendLine("## Usage");
            sb.AppendLine();
            sb.AppendLine($"    {definitions.UsageSummary}");
            sb.AppendLine();
            sb.AppendLine($"_{definitions.Description}_");
            sb.AppendLine();
            sb.AppendLine("### Actions");
            sb.AppendLine();

            foreach (var action in definitions.UsageActions)
            {
                sb.AppendLine($"#### {action.DefaultAlias}");
                sb.AppendLine();
                sb.AppendLine($"    {action.UsageSummary}");
                sb.AppendLine();
                sb.AppendLine(action.Description);
                sb.AppendLine();

                if (action.HasArguments)
                { 
                    sb.AppendLine("| Option | Default Value | Is Switch | Description |");
                    sb.AppendLine("| --- | --- | --- | --- |");

                    foreach (var arg in action.UsageArguments.Where(a => !a.OmitFromUsage))
                    {
                        var enumValues = arg.EnumValuesAndDescriptions.Aggregate(new StringBuilder(), (sb, fullDescr) => {
                            var pos = fullDescr.IndexOf(" - ");
                            var value = fullDescr.Substring(0, pos);
                            var descr = fullDescr.Substring(pos);
                            sb.Append($" <br> `{value}` {descr}");
                            return sb;
                        });
                        sb.AppendLine($"| {arg.DefaultAlias}{(arg.IsRequired ? "*" : "")} | {(arg.HasDefaultValue ? $"`{arg.DefaultValue}`" : "")} | {(arg.ArgumentType == typeof(bool) ? "X" : "")} | {arg.Description}{enumValues} |");
                    }
                    sb.AppendLine();
                }

                if (action.HasExamples)
                {
                    foreach (var example in action.Examples)
                    {
                        if (example.HasTitle)
                        { 
                            sb.AppendLine($"**{example.Title}**");
                            sb.AppendLine();
                        }

                        sb.AppendLine($"    {example.Example}");
                        sb.AppendLine();
                        sb.AppendLine($"_{example.Description}_");
                        sb.AppendLine();
                    }
                }
            }

            if (String.IsNullOrEmpty(outPath))
            { 
                using (_appSettings.SuppressConsoleLogs())
                {
                    Console.WriteLine(sb.ToString());
                }
            }
            else
            {
                using (var writer = File.CreateText(outPath))
                {
                    writer.Write(sb.ToString());
                }
            }
        }

        /* Further actions to add
         * - DownloadPBIDesktop -targetDir -removePriorVersions
         * - Compile|Write
         */
    }

    public enum ExtractActionCompatibilityMode
    {
        [ArgDescription("Attempts extraction using the V3 model, and falls back to Legacy mode in case the PBIX file does not have V3 format.")]
        Auto, 
        [ArgDescription("Extracts V3 PBIX files only. Fails if the file provided has a legacy format.")]
        V3,
        [ArgDescription("Extracts legacy PBIX files only. Fails if the file provided has the V3 format.")]
        Legacy
    }

    [Flags]
    public enum ExportTransforms
    {
        [ArgDescription("Removes the 'defaultPowerBIDataSourceVersion' model property, making the exported BIM file compatible with Azure Analysis Services.")]
        RemovePBIDataSourceVersion = 1
    }

    public enum CacheAction
    {
        [ArgDescription("List all cache folders.")]
        List = 1,
        [ArgDescription("Clear all cache folders.")]
        ClearAll,
        [ArgDescription("Clear all cache folders except the most recent one.")]
        ClearOutdated
    }

    public enum ExternalToolAction
    { 
        Install,
        Uninstall,
        ExtractCurrentProject
    }
}