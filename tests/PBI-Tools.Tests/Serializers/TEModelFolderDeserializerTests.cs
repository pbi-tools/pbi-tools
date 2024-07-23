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
