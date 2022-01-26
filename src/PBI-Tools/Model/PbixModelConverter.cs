// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbiTools.Model
{
    using FileSystem;
    using PowerBI;
    using ProjectSystem;
    using Serialization;
    using Utils;

    public class PbixModelConverter
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixModelConverter>();

        public PbixModel Model { get; }

        public PbixModelConverter(PbixModel model)
        {
            this.Model = model ?? throw new ArgumentNullException(nameof(model));
        }


        public static PbixModelConverter FromFile(FileInfo file)
        {
            if (!file.Exists) throw new FileNotFoundException($"File does not exist: '{file.FullName}'");

            // Must be *.bim/*.json TMSL file
            switch (file.Extension.ToLowerInvariant())
            { 
                case ".json":
                case ".bim":
                    break;
                default:
                    throw new PbiToolsCliException(ExitCode.UnsupportedFileType, "Only .json/.bim source files are supported.");
            }

            using var reader = new JsonTextReader(file.OpenText());
            var model = PbixModel.FromTabularModelJson(JObject.Load(reader), file.FullName);

            return new PbixModelConverter(model) { TabularModelOnly = true };
        }

        public static PbixModelConverter FromFolder(DirectoryInfo folder)
        {
            if (!folder.Exists) throw new DirectoryNotFoundException($"Folder does not exist: '{folder.FullName}'");

            var isPbixProjFolder = PbixProject.IsPbixProjFolder(folder.FullName);
            var pbixProj = !isPbixProjFolder && folder.Parent != null && PbixProject.IsPbixProjFolder(folder.Parent.FullName)
                ? PbixProject.FromFolder(folder.Parent.FullName)
                : null;

            var model = isPbixProjFolder
                ? PbixModel.FromFolder(folder.FullName)
                : PbixModel.FromTabularModelFolder(folder.FullName, pbixProj);

            return new PbixModelConverter(model) { TabularModelOnly = !isPbixProjFolder };
        }


        public bool AllowOverwrite { get; set; }
        public bool TabularModelOnly { get; set; }
        public bool UpdateSettings { get; set; }
        public PbixProject ProjectSettings { get; set; }


        /// <summary>
        /// 
        /// </summary>
        public void SaveAs(string outputPath = null)
        {
            var output = ResolveOutput(outputPath);

            // overwrite
            if ((new DirectoryInfo(output.Path).Exists || new FileInfo(output.Path).Exists)
                && !AllowOverwrite)
            { 
                throw new PbiToolsCliException(ExitCode.OverwriteNotAllowed, "The destination folder/file exists, however, the '-overwrite' has not been specified. Aborting conversion. Please retry with '-overwrite'.");
            }

            // settings
            var settings = this.ProjectSettings ?? Model.PbixProj;

            switch (output.Type)
            { 
                case PbixModelOutputType.PbixProj:
                    // LOG
                    this.Model.ToFolder(output.Path, settings.Settings);
                    break;
                case PbixModelOutputType.TabularModelFolder:
                    // LOG
                    using (var projFolder = new ProjectRootFolder(output.Path))
                    {
                        var serializer = new TabularModelSerializer(projFolder.GetFolder(), settings.Settings.Model);
                        serializer.Serialize(Model.DataModel);
                        projFolder.Commit();  // Ensures the folder is cleaned up
                    }
                    break;
                case PbixModelOutputType.TabularModelFile:
                    // LOG
                    var fileInfo = new FileInfo(output.Path);
                    using (var projFolder = new ProjectRootFolder(fileInfo.Directory.FullName))
                    {
                        var serializer = new TabularModelSerializer(projFolder.GetFolder(), settings.Settings.Model, fileInfo.Name);
                        serializer.Serialize(Model.DataModel);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported PbixModelOutputType: {output.Type}.");
            }

            // updateSettings
            if (UpdateSettings)
                settings.Save();

        }

        public PbixModelOutput ResolveOutput(string outputPath)
        {
            if (outputPath is null) {
                return Model.Type switch
                {
                    PbixModelSource.TabularModel when Path.HasExtension(Model.SourcePath) =>
                        // Model was created from a TMSL file: Default output '/Model' folder relative to source file
                        new PbixModelOutput {
                            Type = PbixModelOutputType.TabularModelFolder,
                            Path = Path.Combine(new FileInfo(Model.SourcePath).DirectoryName, TabularModelSerializer.FolderName)
                        },
                    PbixModelSource.TabularModel =>
                        // Model was created from a TMSL folder
                        new PbixModelOutput
                        {
                            Type = PbixModelOutputType.TabularModelFolder,
                            Path = Model.GetProjectFolder()
                        },
                    _ when TabularModelOnly =>
                        // Model was created from PbixProj folder with 'TabularModelOnly' option enabled
                        new PbixModelOutput {
                            Type = PbixModelOutputType.TabularModelFolder,
                            Path = Path.Combine(Model.GetProjectFolder(), TabularModelSerializer.FolderName)
                        },
                    _ =>
                        new PbixModelOutput {
                            Type = PbixModelOutputType.PbixProj,
                            Path = Model.GetProjectFolder()
                        }
                };
            }

            if (outputPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new PbiToolsCliException(ExitCode.InvalidArgs, "'outPath' contains invalid path characters.");

            var outputIsFile = IsFile(outputPath);

            if (outputIsFile && !(new[] {".json", ".bim"}.Contains(Path.GetExtension(outputPath).ToLowerInvariant())))
                throw new PbiToolsCliException(ExitCode.UnsupportedFileType, "Only .json/.bim source files are supported.");

            if (outputIsFile)
            {
                return new PbixModelOutput
                {
                    Type = PbixModelOutputType.TabularModelFile,
                    Path = new FileInfo(outputPath).FullName
                };
            }

            if (TabularModelOnly)            
            {
                return new PbixModelOutput
                {
                    Type = PbixModelOutputType.TabularModelFolder,
                    Path = new DirectoryInfo(outputPath).FullName
                };
            }

            return new PbixModelOutput
            {
                Type = PbixModelOutputType.PbixProj,
                Path = new DirectoryInfo(outputPath).FullName
            };
        }

        private static bool IsFile(string path) =>
            (new FileInfo(path).Exists || Path.HasExtension(path))
                && !(new DirectoryInfo(path).Exists);

    }

    public class PbixModelOutput
    { 
        public string Path { get; set; }
        public PbixModelOutputType Type { get; set; }
    }

    public enum PbixModelOutputType
    { 
        PbixProj,
        TabularModelFolder,
        TabularModelFile
    }
}
