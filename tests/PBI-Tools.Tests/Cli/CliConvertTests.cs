// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using PbiTools.Serialization;
using Xunit;
using static PbiTools.Tests.TestData;
using static PbiTools.Utils.Resources;

namespace PbiTools.Tests
{
    using Cli;
    using ProjectSystem;

    public class CliConvertTests : HasTempFolder
    {
        private readonly CmdLineActions _cli = new();


        [Fact]
        public void Throws_when_source_does_not_exist()
        {
            var ex = Assert.Throws<PbiToolsCliException>(() =>
            {
                _cli.Convert(
                    source: TestFolder.GetPath("missing"),
                    outPath: default,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );
            });

            Assert.Equal(ExitCode.PathNotFound, ex.ErrorCode);
        }


        /// <summary>
        /// Tests the 'convert' CLI action with a TMSL json file as the source.
        /// </summary>
        public class FileSource : HasTempFolder
        {
            private readonly string _sourceDir;
            private readonly string _sourceFile;
            private readonly CmdLineActions _cli = new();

            public FileSource()
            {
                _sourceDir = TestFolder.GetPath("Source");
                _sourceFile = TestFolder.GetPath("Source", "AdventureWorks.json");

                Directory.CreateDirectory(_sourceDir);
                AdventureWorks.WriteDatabaseJsonTo(_sourceFile);
            }


            [Fact]
            public void Throws_when_file_is_not_bim_or_json()
            {
                string sourceFile = Path.Combine(_sourceDir, "db.txt");
                File.WriteAllText(sourceFile, default);

                var ex = Assert.Throws<PbiToolsCliException>(() => 
                {
                    _cli.Convert(
                        source: sourceFile,
                        outPath: default,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: default,
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.UnsupportedFileType, ex.ErrorCode);
            }

            [Fact]
            public void Uses_default_output_folder_if_not_specified()
            {
                _cli.Convert(
                    source: _sourceFile,
                    outPath: default,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                Assert.True(Directory.Exists(Path.Combine(_sourceDir, "Model")));
                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
            }

            [Fact]
            public void Uses_specified_output_folder()
            {
                var outPath = Path.Combine(_sourceDir, "out");

                _cli.Convert(
                    source: _sourceFile,
                    outPath: outPath,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                Assert.True(Directory.Exists(outPath));
                Assert.True(File.Exists(Path.Combine(outPath, "database.json")));
            }

            [Fact]
            public void Throws_when_output_file_exists_and_overwrite_is_not_specified()
            {
                string outPath = Path.Combine(_sourceDir, "output.bim");
                File.WriteAllText(outPath, default);

                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceFile,
                        outPath: outPath,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: default,
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.OverwriteNotAllowed, ex.ErrorCode);
            }

            [Fact]
            public void Throws_when_output_dir_exists_and_overwrite_is_not_specified()
            {
                var outPath = Path.Combine(_sourceDir, "out");
                Directory.CreateDirectory(outPath);

                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceFile,
                        outPath: outPath,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: default,
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.OverwriteNotAllowed, ex.ErrorCode);
            }

            [Fact]
            public void Overwrites_entire_output_folder()
            {
                var outPath = Path.Combine(_sourceDir, "out");
                var externalFile = Path.Combine(outPath, "README.txt");

                Directory.CreateDirectory(outPath);
                File.WriteAllText(externalFile, default);

                Assert.True(File.Exists(externalFile));

                _cli.Convert(
                    source: _sourceFile,   // input is file
                    outPath: outPath,      // output is (tabular model) folder
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.False(File.Exists(externalFile));
            }

            [Fact]
            public void Throws_when_settings_file_does_not_exist()
            {
                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceFile,
                        outPath: default,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: Path.Combine(_sourceDir, "missing.json"),
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.PathNotFound, ex.ErrorCode);
            }

            [Fact]
            public void Uses_serialization_settings_from_external_file_if_provided()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Raw;

                string settingsFile = Path.Combine(_sourceDir, "settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceFile,
                    outPath: default,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(Path.Combine(_sourceDir, "Model"), "*.*", SearchOption.AllDirectories));
            }

            [Fact]
            public void Cli_serialization_settings_take_precedence_over_external_file()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Default;

                string settingsFile = Path.Combine(_sourceDir, "settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceFile,
                    outPath: default,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(Path.Combine(_sourceDir, "Model"), "*.*", SearchOption.AllDirectories));
            }


            [Fact]
            public void External_settings_file_is_updated_when_flag_is_set()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Default;

                string settingsFile = Path.Combine(_sourceDir, "settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceFile,
                    outPath: default,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: true,
                    modelOnly: default,
                    overwrite: true
                );

                var proj2 = PbixProject.FromFile(settingsFile);
                Assert.Equal(ModelSerializationMode.Raw, proj2.Settings.Model.SerializationMode);
            }
        }


        /// <summary>
        /// Tests the 'convert' CLI action with a PbixProj folder as the source.
        /// </summary>
        public class PbixProjSource : FolderSourceBase
        {
            public PbixProjSource() : base(extractDir => extractDir)
            {
            }


            [Fact]
            public void Uses_specified_output_folder()
            {
                var outPath = TestFolder.GetPath("out");

                _cli.Convert(
                    source: _sourceDir,
                    outPath: outPath,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                Assert.True(Directory.Exists(outPath));
                Assert.True(File.Exists(TestFolder.GetPath("out", "Model", "database.json")));
            }

            [Fact]
            public void Uses_serialization_settings_from_external_file_if_provided()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Raw;

                string settingsFile = TestFolder.GetPath("settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceDir,
                    outPath: default,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(Path.Combine(_sourceDir, "Model"), "*.*", SearchOption.AllDirectories));
            }

            [Fact]
            public void Cli_serialization_settings_take_precedence_over_external_file()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Default;

                string settingsFile = TestFolder.GetPath("settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceDir,
                    outPath: default,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(Path.Combine(_sourceDir, "Model"), "*.*", SearchOption.AllDirectories));
            }

            [Fact]
            public void Performs_local_modelonly_conversion_when_flag_is_set()
            {
                // Given
                var additionalFile = new FileInfo(Path.Combine(_sourceDir, "Folder", "test.txt"));
                additionalFile.Directory.Create();
                File.WriteAllText(additionalFile.FullName, "");

                Assert.True(additionalFile.Exists);

                // When
                _cli.Convert(
                    source: _sourceDir,   // Source is PbixProj
                    outPath: default,     // No output specified
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: true,      // 'ModelOnly' enabled
                    overwrite: true       // Overwrite allowed
                );

                // Then
                // Other PbixProj folders/files are not touched
                // Test implicitly: Unknown files are not removed

                Assert.True(File.Exists(Path.Combine(_sourceDir, "Model", "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(Path.Combine(_sourceDir, "Model"), "*.*", SearchOption.AllDirectories));

                Assert.True(additionalFile.Exists);
            }

            [TestNotImplemented]
            public void Output_folder_only_contains_model_with_modelonly_flag_set()
            {
                // Given

                // When

                // Then
            }

        }


        /// <summary>
        /// Tests the 'convert' CLI action with a Model folder as the source.
        /// </summary>
        public class ModelFolderSource : FolderSourceBase
        {
            public ModelFolderSource() : base(extractDir => Path.Combine(extractDir, "Model"))
            {
            }


            [Fact]
            public void Uses_specified_output_folder()
            {
                var outPath = TestFolder.GetPath("out");

                _cli.Convert(
                    source: _sourceDir,
                    outPath: outPath,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                Assert.True(Directory.Exists(outPath));
                Assert.True(File.Exists(TestFolder.GetPath("out", "database.json")));
            }

            [Fact]
            public void Uses_settings_file_from_parent_pbixproj_folder_if_present()
            {
                // Change model serialization mode in parent folder to 'Raw'
                using (var parentFolder = new ProjectRootFolder(_extractDir))
                {
                    var pbixProj = PbixProject.FromFolder(parentFolder);

                    pbixProj.Settings.Model.SerializationMode = ModelSerializationMode.Raw;

                    pbixProj.Save(parentFolder);
                }

                var outPath = TestFolder.GetPath("out");

                // Assume Converter reads settings from parent PbixProj
                _cli.Convert(
                    source: _sourceDir,
                    outPath: outPath,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                // out folder has single file: 'database.json'
                Assert.True(File.Exists(TestFolder.GetPath("out", "database.json")));
                Assert.Single(Directory.GetFiles(outPath, "*.*", SearchOption.AllDirectories));
            }

            [Fact]
            public void Uses_serialization_settings_from_external_file_if_provided()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Raw;

                string settingsFile = TestFolder.GetPath("settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceDir,
                    outPath: default,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(_sourceDir, "*.*", SearchOption.AllDirectories));
            }

            [Fact]
            public void Cli_serialization_settings_take_precedence_over_external_file()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Default;

                string settingsFile = TestFolder.GetPath("settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceDir,
                    outPath: default,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.True(File.Exists(Path.Combine(_sourceDir, "database.json")));
                // 'Model' folder contains only a single file
                Assert.Single(Directory.EnumerateFiles(_sourceDir, "*.*", SearchOption.AllDirectories));
            }
        }


        public abstract class FolderSourceBase : HasTempFolder
        {
            /// <summary>
            /// Directory contains full AdventureWorks PbixProj sources.
            /// </summary>
            protected readonly string _extractDir;
            /// <summary>
            /// The source directory used when invoking the 'convert' action.
            /// </summary>
            protected readonly string _sourceDir;
            protected readonly CmdLineActions _cli = new();

            protected FolderSourceBase(Func<string, string> getSourceDir)
            {
                _extractDir = TestFolder.GetPath("Source");

                Directory.CreateDirectory(_extractDir);

                using var zip = new ZipArchive(GetEmbeddedResourceStream("Adventure Works DW 2020.zip"));
                zip.ExtractToDirectory(_extractDir);

                _sourceDir = getSourceDir(_extractDir);
            }


            [Fact]
            public void Throws_when_output_file_is_not_bim_or_json()
            {
                string outputFile = TestFolder.GetPath("db.txt");
                File.WriteAllText(outputFile, default);

                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceDir,
                        outPath: outputFile,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: default,
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.UnsupportedFileType, ex.ErrorCode);
            }

            [Fact]
            public void Performs_modelonly_Raw_conversion_when_output_is_file()
            {
                var outPath = TestFolder.GetPath("out", "database.bim");

                _cli.Convert(
                    source: _sourceDir,
                    outPath: outPath,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: default
                );

                Assert.True(File.Exists(outPath));
            }


            [TestNotImplemented]
            public void Implicit_PbixProj_settings_file_is_updated_if_flag_is_set()
            {
                // Given

                // When

                // Then
            }


            [Fact]
            public void Throws_when_output_dir_exists_and_overwrite_is_not_specified()
            {
                var outPath = Path.Combine(_sourceDir, "out");
                Directory.CreateDirectory(outPath);

                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceDir,
                        outPath: outPath,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: default,
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default  // default is FALSE
                    );
                });

                Assert.Equal(ExitCode.OverwriteNotAllowed, ex.ErrorCode);
            }

            [TestNotImplemented]
            public void Throws_when_output_file_exists_and_overwrite_is_not_specified()
            {
                // Given
            
                // When
            
                // Then
            }

            [Fact]
            public void Overwrites_entire_output_folder()
            {
                var outPath = Path.Combine(_sourceDir, "out");
                Directory.CreateDirectory(outPath);

                var externalFile = Path.Combine(outPath, "README.txt");
                File.WriteAllText(externalFile, default);

                Assert.True(File.Exists(externalFile));

                _cli.Convert(
                    source: _sourceDir,
                    outPath: outPath,
                    modelSerialization: default,
                    mashupSerialization: default,
                    settingsFile: default,
                    updateSettings: default,
                    modelOnly: default,
                    overwrite: true
                );

                Assert.False(File.Exists(externalFile));
            }

            [Fact]
            public void Throws_when_settings_file_does_not_exist()
            {
                var ex = Assert.Throws<PbiToolsCliException>(() =>
                {
                    _cli.Convert(
                        source: _sourceDir,
                        outPath: default,
                        modelSerialization: default,
                        mashupSerialization: default,
                        settingsFile: Path.Combine(_sourceDir, "missing.json"),
                        updateSettings: default,
                        modelOnly: default,
                        overwrite: default
                    );
                });

                Assert.Equal(ExitCode.PathNotFound, ex.ErrorCode);
            }


            [Fact]
            public void External_settings_file_is_updated_when_flag_is_set()
            {
                var proj = new PbixProject();
                proj.Settings.Model.SerializationMode = ModelSerializationMode.Default;

                string settingsFile = TestFolder.GetPath("settings.json");
                proj.Save(settingsFile);

                _cli.Convert(
                    source: _sourceDir,
                    outPath: default,
                    modelSerialization: ModelSerializationMode.Raw,
                    mashupSerialization: default,
                    settingsFile: settingsFile,
                    updateSettings: true,
                    modelOnly: default,
                    overwrite: true
                );

                var proj2 = PbixProject.FromFile(settingsFile);
                Assert.Equal(ModelSerializationMode.Raw, proj2.Settings.Model.SerializationMode);
            }
        }


    }
}
