using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PbiTools.Actions;
using PbiTools.PowerBI;
using PbiTools.Rpc;
using PbiTools.Utils;
using PowerArgs;
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




        [ArgActionMethod, ArgShortcut("extract"), ArgDescription("Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.")]
        public void Extract(
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX file.")] string path,
            [ArgDescription("The extraction mode."), ArgDefaultValue(ExtractActionCompatibilityMode.Auto)] ExtractActionCompatibilityMode mode
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            using (var reader = new PbixReader(path, DependenciesResolver.Default))
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
                        using (var model = Model.PbixModel.FromReader(reader))
                        {
                            model.ToFolder();
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

        [ArgActionMethod, ArgShortcut("export-bim"), ArgDescription("Converts the Model artifacts to a TMSL/BIM file.")]
        public void ExportBim(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder to export the BIM file from.")] string folder,
            [ArgDescription("")] bool generateDataSources
        )
        {
            using (var rootFolder = new FileSystem.ProjectRootFolder(folder))
            {
                var serializer = new Serialization.TabularModelSerializer(rootFolder);
                if (serializer.TryDeserialize(out var db))
                {
                    if (generateDataSources)
                    {
                        var dataSources = TabularModel.TabularModelConversions.GenerateDataSources(db);
                        db["model"]["dataSources"] = dataSources;
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
                    Console.WriteLine("A BIM file could not be exported.");
                }
            }
        }

        [ArgActionMethod, ArgShortcut("info"), ArgDescription("Collects diagnostic information about the local system and writes a JSON object to StdOut.")]
        public void Info()
        {
            using (_appSettings.SetScopedLogLevel(LogEventLevel.Warning))  // Suppresses Informational logs
            {
                var pbiInstalls = PowerBILocator.FindInstallations();
                var json = new JObject
                {
                    { "build", AssemblyVersionInformation.AssemblyFileVersion },
                    { "effectivePowerBiFolder", DependenciesResolver.Default.GetEffectivePowerBiInstallDir() },
                    { "pbiInstalls", JArray.Parse(JsonConvert.SerializeObject(pbiInstalls)) },
                    { "amoVersion", typeof(Microsoft.AnalysisServices.Tabular.Server).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion }
                };
                using (var writer = new JsonTextWriter(Console.Out))
                {
                    writer.Formatting = Environment.UserInteractive ? Formatting.Indented : Formatting.None;
                    json.WriteTo(writer);
                }
            }            
        }

        [ArgActionMethod, ArgShortcut("start-server"), HideFromUsage]
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

        /* Further actions to add
         * - ClearCache (%LOCALAPPDATA%\pbi-tools)
         * - DownloadPBIDesktop -targetDir -removePriorVersions
         * - Compile|Write
         */
    }

    public enum ExtractActionCompatibilityMode
    {
        Auto, 
        V3,
        Legacy
    }
}