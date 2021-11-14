// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using PbiTools.FileSystem;
using PbiTools.Serialization;
using Xunit;
using static PbiTools.Utils.Resources;

namespace PbiTools.Tests
{
    public class MashupSerializerTests
    {
        private readonly MockProjectFolder _folder;
        private readonly MashupSerializer _serializer;

        public MashupSerializerTests()
        {
            _serializer = new MashupSerializer(new MockRootFolder(), new ProjectSystem.MashupSettings { SerializationMode = ProjectSystem.MashupSerializationMode.Expanded });
            _folder = _serializer.MashupFolder as MockProjectFolder;
        }

        private JObject SerializeMetadataFromResource(string name)
        {
            var xml = GetEmbeddedResource(name, XDocument.Load);
            _serializer.SerializeMetadata(xml);
            return _folder.GetAsJson("Metadata/metadata.json");
        }

        [Fact]
        public void Emits_empty_items_as_null()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Null, json["Formulas"]["Section1/Query1/Source"].Type);
        }

        [Fact]
        public void Emits_long_values_as_JValue_long()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(new JValue(0L), json["Formulas"]["Section1/Query1"]["IsPrivate"]);
        }

        [Fact]
        public void Emits_simple_string_values_as_JValue_string()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(new JValue("Table"), json["Formulas"]["Section1/Query1"]["ResultType"]);
        }

        [Fact]
        public void Skips_QueryGroups()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_WithQueryGroups.xml");
            Assert.Null(json["AllFormulas"]["QueryGroups"]);
            Assert.NotNull(json["AllFormulas"]["Relationships"]);
        }

        [Fact]
        public void Detects_and_emits_JArray()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Array, json["Formulas"]["Section1/Query1"]["FillColumnNames"].Type);
            Assert.Equal(new [] {"ID","Label"}, json["Formulas"]["Section1/Query1"]["FillColumnNames"].ToObject<string[]>());
        }

        [Fact]
        public void Detects_and_emits_JObject()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Object, json["Formulas"]["Section1/Query1"]["RelationshipInfoContainer"].Type);
            Assert.Equal(2, json["Formulas"]["Section1/Query1"]["RelationshipInfoContainer"].Value<int>("columnCount"));
        }

        [Fact]
        public void Extracts_RootFormulaText_into_file()
        {
            SerializeMetadataFromResource("MashupMetadata_Simple.xml");

            Assert.True(_folder.ContainsPath("Metadata/Section1/Query1/RootFormulaText.m"));
            Assert.Equal("let\n    Source = #table(type table[ID = number, Label = text], {\n    { 1, \"Foo\" },\n    { 2, \"Bar\" }\n})\nin\n    Source"
                , _folder.GetAsString("Metadata/Section1/Query1/RootFormulaText.m"));
        }

        [Fact]
        public void Removes__RootFormulaText_and_ReferencedQueriesFormulaText__from_LastAnalysisServicesFormulaText_entry()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");

            var lastAnalysisServicesFormulaText = json["Formulas"]["Section1/Query1"]["LastAnalysisServicesFormulaText"] as JObject;
            Assert.Single(lastAnalysisServicesFormulaText.Properties());
            Assert.NotNull(lastAnalysisServicesFormulaText["IncludesReferencedQueries"]);
        }

        [Fact]
        public void Escapes_invalid_directoryname_characters()
        {
            SerializeMetadataFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

            // Section1/Change%20Log
            Assert.True(_folder.ContainsPath("Metadata/Section1/Change Log/RootFormulaText.m"));
            // Section1/Revenue/Added%20%5BRevenue%20Type%5D%20%28Recognized%29
            Assert.True(_folder.ContainsPath("Metadata/Section1/Revenue/Added [Revenue Type] (Recognized)/RootFormulaText.m"));
            // Section1/Special%20Characters/%22%20%3C%20%3E%20%7C%20%3A%20*%20%3F%20%2F%20%5C
            Assert.True(_folder.ContainsPath("Metadata/Section1/Special Characters/%22 %3C %3E %7C %3A %2A %3F %2F %5C/RootFormulaText.m"));
        }

        [Fact]
        public void Extracts_ReferencedQueries()
        {
            SerializeMetadataFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

            // Section1/Revenue/Added%20%5BRevenue%20Type%5D%20%28Recognized%29
            Assert.True(_folder.ContainsPath("Metadata/Section1/Revenue/Added [Revenue Type] (Recognized)/ReferencedQueries/Query0.m"));
            Assert.Equal("42", _folder.GetAsString("Metadata/Section1/Revenue/Added [Revenue Type] (Recognized)/ReferencedQueries/Query0.m"));
        }

        [Fact]
        public void Escapes_invalid_filename_characters_in_ReferencedQueries()
        {
            SerializeMetadataFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

            // &quot;Has / Special \u0026 Characters&quot;
            Assert.True(_folder.ContainsPath("Metadata/Section1/Change Log/ReferencedQueries/Has %2F Special & Characters.m"));
        }

        [Fact]
        public void Replaces_escape_sequences_in_ItemPaths()
        {
            var json = SerializeMetadataFromResource("MashupMetadata_Simple.xml");

            Assert.Equal(JTokenType.Object, json["Formulas"]["Section1/Sample File"].Type);     // original path is: "Section1/Sample%20File"
            Assert.Equal(JValue.CreateNull(), json["Formulas"]["Section1/Sample File/Source"]);
        }

        [Fact]
        public void MashupPackage__Extracts_empty_section_document()
        {
            using (var archive = new ZipArchive(new MemoryStream(TestData.MinimalMashupPackageBytes)))
            {
                _serializer.SerializePackage(archive);

                Assert.Equal("section Section1;", _folder.GetAsString("Package/Formulas/Section1.m"));
            }
        }

        [Fact]
        public void SerializePackage__Deletes_section_file_before_writing_to_section_folder()
        {
            var mockFolder = new Mock<IProjectFolder>();
            mockFolder.Setup(folder => folder.ContainsFile("Formulas/Section1.m")).Returns(true);
            mockFolder.Setup(folder => folder.WriteText(It.IsAny<string>(), It.IsAny<Action<TextWriter>>()))
                .Callback<string, Action<TextWriter>>((_, onWriter) => // this is necessary to avoid NRE in test code
                {
                    using (var writer = new StringWriter())
                    {
                        onWriter(writer);
                    }
                });
            mockFolder.Setup(folder => folder.GetSubfolder(It.IsAny<string[]>())).Returns(mockFolder.Object);

            var serializer = new MashupSerializer(new MockRootFolder(() => mockFolder.Object), new ProjectSystem.MashupSettings { SerializationMode = ProjectSystem.MashupSerializationMode.Expanded  });

            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var packageXml = archive.CreateEntry("Config/Package.xml");
                    using (var writer = new StreamWriter(packageXml.Open()))
                    {
                        writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?><Package xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""><Version>2.55.5010.641</Version><MinVersion>1.5.3296.0</MinVersion><Culture>en-GB</Culture></Package>");
                    }

                    var contentTypesXml = archive.CreateEntry("[Content_Types].xml"); // must have written previous entry before opening new one, see https://stackoverflow.com/a/37533305/736263
                    using (var writer = new StreamWriter(contentTypesXml.Open()))
                    {
                        writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""xml"" ContentType=""text/xml"" /><Default Extension=""m"" ContentType=""application/x-ms-m"" /></Types>");
                    }

                    var section1 = archive.CreateEntry("Formulas/Section1.m");
                    using (var writer = new StreamWriter(section1.Open()))
                    {
                        writer.Write("section Section1;\n\nshared Version = \"1.0\";");
                    }
                }

                stream.Position = 0;

                using (var archive = new ZipArchive(stream))
                {
                    serializer.SerializePackage(archive);
                }
            }

            mockFolder.Verify(folder => folder.DeleteFile("Formulas/Section1.m"));
        }
    }
}
#endif