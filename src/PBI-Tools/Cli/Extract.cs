// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using PowerArgs;

namespace PbiTools.Cli
{
    using PowerBI;

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
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIX file.")]
                string pbixPath,
            [ArgDescription("The port number from a running Power BI Desktop instance. When specified, the model will not be read from the PBIX file, and will instead be retrieved from the PBI instance. Only supported for V3 PBIX files.")]
            [ArgRange(1024, 65535)]
                int pbiPort,
            [ArgDescription("The folder to extract the PBIX file to. Only needed to override the default location. Can be relative to current working directory.")]
                string extractFolder,
            [ArgDescription("The extraction mode."), ArgDefaultValue(ExtractActionCompatibilityMode.Auto)]
                ExtractActionCompatibilityMode mode,
            [ArgDescription("The model serialization mode.")]
                ProjectSystem.ModelSerializationMode modelSerialization,
            [ArgDescription("The mashup serialization mode.")]
                ProjectSystem.MashupSerializationMode mashupSerialization
        )
        {
            // TODO Support '-parts' parameter, listing specifc parts to extract only
            // ReportSerializationMode: Full, ExtractObjets, Raw

            using (var reader = new PbixReader(pbixPath, _dependenciesResolver))
            {
                if (mode == ExtractActionCompatibilityMode.Legacy)
                {
                    using (var extractor = new Actions.PbixExtractAction(reader))
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
                        using (var extractor = new Actions.PbixExtractAction(reader))
                        {
                            extractor.ExtractAll();
                        }
                    }
                }
            }

            Console.WriteLine($"Completed in {_stopWatch.Elapsed}.");
        }
#endif

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
}