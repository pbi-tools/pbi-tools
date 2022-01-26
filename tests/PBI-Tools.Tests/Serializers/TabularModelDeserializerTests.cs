// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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
    /// Extracts the embedded 'Adventure Works DW 2020' Pbix Project,
    /// and deserializes the model into a TOM.Model to run tests against.
    /// </summary>
    public class TabularModelDeserializerFixture : HasTempFolder
    {

        public TabularModelDeserializerFixture()
        {
            using (var zip = new ZipArchive(Resources.GetEmbeddedResourceStream("Adventure Works DW 2020.zip")))
            {
                zip.ExtractToDirectory(TestFolder.Path);
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

        public TOM.Model Model { get; private set; }

    }

    public class TabularModelDeserializerTests : AdventureWorksDeserializerTestsBase, IClassFixture<TabularModelDeserializerFixture>
    {
        public TabularModelDeserializerTests(TabularModelDeserializerFixture fixture) : base(fixture.Model)
        {
        }

    }

}