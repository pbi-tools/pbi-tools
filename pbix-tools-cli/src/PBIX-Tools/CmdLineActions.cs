using System;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerArgs;
using Serilog.Events;

namespace PbixTools
{
#if !DEBUG
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]  // PowerArgs will print the user friendly error message as well as the auto-generated usage documentation for the program.
#endif
    [ArgDescription(AssemblyVersionInformation.AssemblyProduct + ", " + AssemblyVersionInformation.AssemblyInformationalVersion)]
    [ApplyDefinitionTransforms]
    public class CmdLineActions
    {

        private readonly IDependenciesResolver _dependenciesResolver = new DependenciesResolver(); // TODO allow to init this with a set path from config
        private readonly AppSettings _appSettings;

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
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX file")] string path
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            using (var extractor = new PbixExtractAction(path, _dependenciesResolver))
            {
                extractor.ExtractAll();
            }

            Console.WriteLine("Completed.");
        }


        [ArgActionMethod, ArgShortcut("info"), ArgDescription("Collects diagnostic information about the local system and writes a JSON object to StdOut.")]
        public void Info()
        {
            _appSettings.LevelSwitch.MinimumLevel = LogEventLevel.Warning;
            
            var pbiInstalls = PowerBILocator.FindInstallations();
            var json = new JObject
            {
                { "effectivePowerBiFolder", _dependenciesResolver.GetEffectivePowerBiInstallDir() },
                { "pbiInstalls", JArray.Parse(JsonConvert.SerializeObject(pbiInstalls)) }
            };
            using (var writer = new JsonTextWriter(Console.Out))
            {
                writer.Formatting = Environment.UserInteractive ? Formatting.Indented : Formatting.None;
                json.WriteTo(writer);
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
    }

}