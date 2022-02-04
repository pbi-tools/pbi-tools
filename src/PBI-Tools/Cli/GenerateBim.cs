// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json;
using PowerArgs;

namespace PbiTools.Cli
{
    using Tabular;

    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("generate-bim"), ArgAltShortcut("export-bim")]
        [ArgDescription("Generates a TMSL/BIM file from Model sources in a folder. The output path is derived from the source folder.")]
        public void GenerateBim(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder to export the BIM file from.")]
                string folder,
#if NETFRAMEWORK
            [ArgDescription("Generate model data sources. Only required for deployment to Azure Analysis Services, but not for Power BI Premium via the XMLA endpoint.")]
                bool generateDataSources,
#endif
            [ArgDescription("List transformations to be applied to TMSL document.")]
                ExportTransforms transforms
        )
        {
            using (var rootFolder = new FileSystem.ProjectRootFolder(folder))
            {
                var serializer = new Serialization.TabularModelSerializer(rootFolder, ProjectSystem.PbixProject.FromFolder(rootFolder).Settings.Model);
                if (serializer.TryDeserialize(out var db))  // throws for V1 models
                {
#if NETFRAMEWORK
                    if (generateDataSources)
                    {
                        var dataSources = TabularModelConversions.GenerateDataSources(db);
                        db["model"]["dataSources"] = dataSources;
                    }
#endif

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
                    throw new PbiToolsCliException(ExitCode.UnspecifiedError, "A BIM file could not be exported.");
                }
            }
        }

    }

    [Flags]
    public enum ExportTransforms
    {
        [ArgDescription("Removes the 'defaultPowerBIDataSourceVersion' model property, making the exported BIM file compatible with Azure Analysis Services.")]
        RemovePBIDataSourceVersion = 1
    }
    
}