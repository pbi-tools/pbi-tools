using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Serilog;

[assembly: InternalsVisibleTo("pbix-tools.tests")]

namespace PbixTools
{
    class Program
    {
        static Program()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        }

        static void Main(string[] args)
        {
            var dependenciesResolver = new DependenciesResolver(); // TODO allow to init this with a set path from config

            if (args == null || args.Length == 0)
            {
                Console.Error.WriteLine("Missing argument. Must specify path to *.PBIX file.");
                return;
            }

            var path = args[0];
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("Invalid argument. Cannot find file specified.");
                return;
            }

            var extractor = new PbixExtractAction(path, dependenciesResolver);

            extractor.ExtractMashup();
            Console.WriteLine("Mashup extracted");

            extractor.ExtractModel();
            Console.WriteLine("Model extracted");

            extractor.ExtractReport();
            Console.WriteLine("Report extracted");

            extractor.ExtractResources();
            Console.WriteLine("Resources extracted");

            Console.WriteLine("Completed.");

            if (Debugger.IsAttached)
            {
                Console.Write("ENTER...");
                Console.ReadLine();
            }
        }
    }
}
