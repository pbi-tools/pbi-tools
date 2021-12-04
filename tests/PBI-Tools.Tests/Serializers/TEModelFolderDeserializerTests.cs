// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TOM = Microsoft.AnalysisServices.Tabular;
using Xunit;

namespace PbiTools.Tests
{
    using FileSystem;
    using ProjectSystem;
    using Serialization;
    using Utils;

    /// <summary>
    /// Extracts the embedded 'Adventure Works DW 2020 - TE' Tabular Editor model folder,
    /// and deserializes the model into a TOM.Model to run tests against.
    /// </summary>
    public class TEModelFolderDeserializerFixture : HasTempFolder
    {

        public TEModelFolderDeserializerFixture()
        {
            using (var zip = new ZipArchive(Resources.GetEmbeddedResourceStream("Adventure Works DW 2020 - TE.zip")))
            {
                var modelFolder = new DirectoryInfo(Path.Combine(TestFolder.Path, "Model"));
                modelFolder.Create();
                zip.ExtractToDirectory(modelFolder.FullName);
            }

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                var project = PbixProject.FromFolder(rootFolder);
                var serializer = new TabularModelSerializer(
                    rootFolder,
                    project.Settings.Model
                );
                
                if (!serializer.TryDeserialize(out var dbJson))
                {
                    throw new Xunit.Sdk.XunitException("TabularModel Deserialization failed.");
                }

                this.Model = TOM.JsonSerializer.DeserializeDatabase(dbJson.ToString(), new TOM.DeserializeOptions { }).Model;
            }
        }

        public TOM.Model Model { get; }

    }

    public class TEModelFolderDeserializerTests : AdventureWorksDeserializerTestsBase, IClassFixture<TEModelFolderDeserializerFixture>
    {
        public TEModelFolderDeserializerTests(TEModelFolderDeserializerFixture fixture) : base(fixture.Model)
        {
        }

    }

}