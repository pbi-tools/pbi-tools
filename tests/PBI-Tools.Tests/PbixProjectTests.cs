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
    using FileSystem;
    using ProjectSystem;

    public class PbixProjectTests : HasTempFolder
    {
        private readonly string _testFilePath;

        public PbixProjectTests()
        { 
            _testFilePath = Path.Combine(TestFolder.Path, PbixProject.Filename);
        }

        [Fact]
        public void CustomSettings_are_retained_when_saving_file_back()
        {
            var projFile = new JObject {
                { "version", "0.10" },
                { "custom", new JRaw(Utils.Resources.GetEmbeddedResourceString("custom_project_settings.json")) }
            };

            // Write PbixProj file with custom settings
            using (var writer = new JsonTextWriter(File.CreateText(_testFilePath)))
            {
                projFile.WriteTo(writer);
            }

            // Read file using PbixProject API
            // Modify custom settings
            // Save file back
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
            var projFile2 = JObject.Parse(File.ReadAllText(_testFilePath));
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

        [Fact]
        public void ModelSettings_are_retained_when_saving_file_back()
        {
            var projFile = new JObject {
                { "version", "0.10" },
                { "settings", new JObject {
                    { "model", new JObject {
                        { "ignoreProperties", new JArray("foo", "bar") }
                    } }
                } }
            };

            // Write PbixProj file with custom settings
            using (var writer = new JsonTextWriter(File.CreateText(_testFilePath)))
            {
                projFile.WriteTo(writer);
            }

            // Read file using PbixProject API
            // Modify custom settings
            // Save file back
            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                var project = PbixProject.FromFolder(rootFolder);

                // Values read from embedded resource
                Assert.NotNull(project.Settings);
                Assert.NotNull(project.Settings.Model);
                Assert.NotNull(project.Settings.Model.IgnoreProperties);
                Assert.Collection(project.Settings.Model.IgnoreProperties
                    , x => Assert.Equal("foo", x)
                    , x => Assert.Equal("bar", x)
                );

                // Modify settings
                project.Settings.Model.IgnoreProperties = new[] { "baz" };

                // Save file
                project.Save(rootFolder);
            }

            // Read modified file directly
            var projFile2 = JObject.Parse(File.ReadAllText(_testFilePath));
            var modelSettings = projFile2["settings"]["model"];

            Assert.NotNull(modelSettings);
            Assert.Collection(modelSettings["ignoreProperties"].ToObject<string[]>()
                , x => Assert.Equal("baz", x)
            );

        }

        [Fact]
        public void Settings_are_accessible_when_missing_in_file()
        {
            var projFile = new JObject {
                { "version", "0.10" },
            };

            using (var writer = new JsonTextWriter(File.CreateText(_testFilePath)))
            {
                projFile.WriteTo(writer);
            }

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                var project = PbixProject.FromFolder(rootFolder);

                Assert.NotNull(project.Settings);
                Assert.NotNull(project.Settings.Mashup);
                Assert.NotNull(project.Settings.Model);
                Assert.NotNull(project.Settings.Report);
            }
        }

        [Fact]
        public void ModelSettings_are_not_serialized_when_default()
        {
            var project = new PbixProject {
                Settings = new PbixProjectSettings { 
                    Model = new ModelSettings { 
                        SerializationMode = ModelSerializationMode.Default,
                        IgnoreProperties = default,
                        Annotations = new ModelAnnotationSettings { }
                    },
                    Mashup = new MashupSettings { SerializationMode = MashupSerializationMode.Raw }
                }
            };

            // Ensure the above Model settings are indeed Default values
            Assert.True(project.Settings.Model.IsDefault);

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                project.Save(rootFolder);
            }

            // Ensure the Save() method has not wiped the Model object
            Assert.NotNull(project.Settings.Model);

            var projJson = JObject.Parse(File.ReadAllText(_testFilePath));

            // settings.model is not serialized
            Assert.NotNull(projJson["settings"]);
            Assert.Null(projJson["settings"]["model"]);
        }

        [Fact]
        public void Model_Annotations_are_not_serialized_when_default()
        {
            var project = new PbixProject {
                Settings = new PbixProjectSettings { 
                    Model = new ModelSettings { 
                        SerializationMode = ModelSerializationMode.Raw, // NOT using default mode
                        IgnoreProperties = default,
                        Annotations = new ModelAnnotationSettings { }
                    },
                    Mashup = new MashupSettings { SerializationMode = MashupSerializationMode.Raw }
                }
            };

            // Ensure the above Model settings are indeed Default values
            Assert.False(project.Settings.Model.IsDefault);
            Assert.True(project.Settings.Model.Annotations.IsDefault);

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                project.Save(rootFolder);
            }

            // Ensure the Save() method has not wiped the Model object
            Assert.NotNull(project.Settings.Model);
            Assert.NotNull(project.Settings.Model.Annotations);

            var projJson = JObject.Parse(File.ReadAllText(_testFilePath));

            // settings.model is not serialized
            Assert.NotNull(projJson["settings"]);
            Assert.NotNull(projJson["settings"]["model"]);
            Assert.Null(projJson["settings"]["model"]["annotations"]);
        }

        [Fact]
        public void ModelSettings_default_enum_value_is_always_serialized()
        {
            var project = new PbixProject {
                Settings = new PbixProjectSettings { 
                    Model = new ModelSettings { 
                        // SerializationMode not specified
                        Annotations = new ModelAnnotationSettings { Exclude = new[] { "a" } }
                    }
                }
            };

            // Ensure the above Model settings are indeed Default values
            Assert.False(project.Settings.Model.IsDefault);
            Assert.Equal(ModelSerializationMode.Default, project.Settings.Model.SerializationMode);

            using (var rootFolder = new ProjectRootFolder(TestFolder.Path))
            {
                project.Save(rootFolder);
            }

            var projJson = JObject.Parse(File.ReadAllText(_testFilePath));

            Assert.Equal(nameof(ModelSerializationMode.Default), projJson["settings"]["model"]["serializationMode"]);
        }
    }
}
