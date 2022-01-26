// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerArgs;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Cli
{
    using FileSystem;
    using Model;
    using ProjectSystem;
    using Serialization;

    public partial class CmdLineActions
    {
        /* Scenarios supported:
           - [x] /PbixProj       -> /PbixProj --modelOnly
           - [x] /PbixProj       -> /PbixProj
           - [x] /PbixProj/Model -> /PbixProj/Model
           - [x] /PbixProj       -> *.bim
           - [x] /PbixProj/Model -> *.bim
           - [x] /Model          -> *.bim
           =========================================
           - [x] *.bim           -> /Model
        */

        [ArgActionMethod, ArgShortcut("convert")]
        [ArgDescription("Performs an offline conversion of PbixProj or Tabular model sources into another format, either in-place or into another destination.")]
        public void Convert(
            [ArgRequired, ArgDescription("The source(s) to convert. Can be a PbixProj folder, a Model/TE folder, or a TMSL json file.")]
                string source,
            [ArgDescription("The (optional) destination. Can be a folder or a file, depending on the conversion mode. Must be a folder if the source is a TMSL json file.")]
                string outPath,
            [ArgDescription("The model serialization mode.")]
                ModelSerializationMode modelSerialization,
            [ArgDescription("The mashup serialization mode.")]
                MashupSerializationMode mashupSerialization,
            [ArgDescription("An external .pbixproj.json file containing serialization settings. Serialization modes specified as command-line arguments take precedence.")]
                string settingsFile,
            [ArgDescription("If set, updates the effective PbixProj settings file used for this conversion.")]
            [ArgDefaultValue(false)]
                bool updateSettings,
            [ArgDescription("If set, converts the model only and leaves other artifacts untouched. Only effective in combination with a PbixProj source folder.")]
            [ArgDefaultValue(false)]
                bool modelOnly,
            [ArgDescription("Allows overwriting of existing files in the destination. The conversion fails if the destination is not empty and this flag is not set.")]
            [ArgDefaultValue(false)]
                bool overwrite
        )
        {
            var sourceAsFile = new FileInfo(source);
            var sourceAsDirectory = new DirectoryInfo(source);

            var modelConverter = source switch {
                _ when sourceAsFile.Exists      => PbixModelConverter.FromFile(sourceAsFile),
                _ when sourceAsDirectory.Exists => PbixModelConverter.FromFolder(sourceAsDirectory),
                _ => throw new PbiToolsCliException(ExitCode.PathNotFound, $"The specified 'source' path does not refer to an existing file or folder: {source}.") 
            };

            // SettingsFile
            modelConverter.ProjectSettings = settingsFile switch
            { 
                // Missing or empty -> default
                var s when string.IsNullOrWhiteSpace(s)
                    => modelConverter.Model.PbixProj,
                // Invalid chars -> throw
                var path when path.IndexOfAny(Path.GetInvalidPathChars()) >= 0
                    => throw new PbiToolsCliException(ExitCode.InvalidArgs, "'settingsFile' contains invalid path characters."),
                // Missing file -> throw
                var asFile when !(new FileInfo(asFile).Exists)
                    => throw new PbiToolsCliException(ExitCode.PathNotFound, "'settingsFile' is not an existing file."),
                // Read PbixProj from file
                _ => PbixProject.FromFile(settingsFile)
            };

            // ModelSerialization
            if (modelSerialization != default)
                modelConverter.ProjectSettings.Settings.Model.SerializationMode = modelSerialization;

            // MashupSerialization
            if (mashupSerialization != default)
                modelConverter.ProjectSettings.Settings.Mashup.SerializationMode = mashupSerialization;

            modelConverter.AllowOverwrite = overwrite;
            modelConverter.UpdateSettings = updateSettings;

            if (modelConverter.Model.Type == PbixModelSource.PbixProjFolder && modelOnly)
                modelConverter.TabularModelOnly = true;

            modelConverter.SaveAs(outPath);
        }

    }

}