using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerArgs;

namespace PbixTools
{
#if !DEBUG
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]  // PowerArgs will print the user friendly error message as well as the auto-generated usage documentation for the program.
#endif
    [ArgDescription(AssemblyVersionInformation.AssemblyProduct + ", " + AssemblyVersionInformation.AssemblyInformationalVersion)]
    public class CmdLineActions
    {

        private readonly IDependenciesResolver _dependenciesResolver = new DependenciesResolver(); // TODO allow to init this with a set path from config

        public CmdLineActions() : this(Program.AppSettings)
        {
        }

        public CmdLineActions(AppSettings appSettings)
        {
            if (appSettings == null) throw new ArgumentNullException(nameof(appSettings));
        }



        [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
        public bool Help { get; set; }




        [ArgActionMethod, ArgDescription("Extracts the contents of a PBIX/PBIT file into a folder structure suitable for source control. By default, this will create a sub-folder in the directory of the *.pbix file with the same name without the extension.")]
        public void Extract(
            [ArgRequired, /*ArgPosition(1),*/ ArgExistingFile, ArgDescription("The path to an existing PBIX file")] string path
        )
        {
            var extractor = new PbixExtractAction(path, _dependenciesResolver);

            extractor.ExtractMashup();
            Console.WriteLine("Mashup extracted");

            extractor.ExtractModel();
            Console.WriteLine("Model extracted");

            extractor.ExtractReport();
            Console.WriteLine("Report extracted");

            extractor.ExtractResources();
            Console.WriteLine("Resources extracted");

            Console.WriteLine("Completed.");
        }


        [ArgActionMethod, ArgDescription("Collects diagnostic information about the local system and writes a JSON object to StdOut.")]
        public void Info()
        {
            var pbiInstalls = PowerBILocator.FindInstallations();
            var json = new JObject
            {
                { "pbiInstalls", JArray.Parse(JsonConvert.SerializeObject(pbiInstalls)) }
            };
            using (var writer = new JsonTextWriter(Console.Out))
            {
                writer.Formatting = Environment.UserInteractive ? Formatting.Indented : Formatting.None;
                json.WriteTo(writer);
            }
        }
    }
}