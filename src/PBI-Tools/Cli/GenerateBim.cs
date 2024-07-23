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
