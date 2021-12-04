// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbiTools.Tests
{
    using PbiTools.FileSystem;
    using PbiTools.ProjectSystem;

    public class PbixProjectTests : HasTempFolder
    {
        [Fact]
        public void CustomSettings_are_retained_when_saving_file_back()
        {
            var projFile = new JObject {
                { "version", "0.10" },
                { "custom", new JRaw(Utils.Resources.GetEmbeddedResourceString("custom_project_settings.json")) }
            };

            using (var writer = new JsonTextWriter(File.CreateText(Path.Combine(TestFolder.Path, PbixProject.Filename))))
            {
                projFile.WriteTo(writer);
            }

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                var project = PbixProject.FromFolder(rootFolder);

                // Values read from embedded resource
                Assert.NotNull(project.Custom);
                Assert.Equal("string", project.Custom["property1"]);
                Assert.Equal(123, project.Custom["property2"]);
                Assert.Collection(project.Custom["array"]
                    , t1 => Assert.Equal("item1", t1)
                    , t2 => Assert.Equal(0.1, t2)
                    , t3 => Assert.IsType<JObject>(t3)
                );

                // Modify settings
                (project.Custom as JObject).Add("inserted", "string2");

                // Save file
                project.Save(rootFolder);
            }

            // Read modified file directly
            var projFile2 = JObject.Parse(File.ReadAllText(Path.Combine(TestFolder.Path, PbixProject.Filename)));
            var custom2 = projFile2["custom"];

            Assert.NotNull(custom2);
            Assert.Equal("string", custom2["property1"]);
            Assert.Equal(123, custom2["property2"]);
            Assert.Collection(custom2["array"]
                , t1 => Assert.Equal("item1", t1)
                , t2 => Assert.Equal(0.1, t2)
                , t3 => Assert.IsType<JObject>(t3)
            );
            Assert.Equal("string2", custom2["inserted"]);

        }
    }
}
