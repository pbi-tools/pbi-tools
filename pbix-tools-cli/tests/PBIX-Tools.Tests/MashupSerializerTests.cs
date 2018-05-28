using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using PbixTools.Serialization;
using Xunit;
using static PbixTools.Utils.Resources;

namespace PbixTools.Tests
{
    public class MashupSerializerTests
    {
        private readonly MockProjectFolder _folder;
        private readonly MashupSerializer _serializer;

        public MashupSerializerTests()
        {
            _folder = new MockProjectFolder();
            _serializer = new MashupSerializer(_folder);
        }

        private JObject SerializeFromResource(string name)
        {
            var xml = GetEmbeddedResource(name, XDocument.Load);
            _serializer.SerializeMetadata(xml);
            return _folder.GetAsJson("Metadata/metadata.json");
        }

        [Fact]
        public void Emits_empty_items_as_null()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Null, json["Formulas"]["Section1/Query1/Source"].Type);
        }

        [Fact]
        public void Emits_long_values_as_JValue_long()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(new JValue(0L), json["Formulas"]["Section1/Query1"]["IsPrivate"]);
        }

        [Fact]
        public void Emits_simple_string_values_as_JValue_string()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(new JValue("Table"), json["Formulas"]["Section1/Query1"]["ResultType"]);
        }

        [Fact]
        public void Skips_QueryGroups()
        {
            var json = SerializeFromResource("MashupMetadata_WithQueryGroups.xml");
            Assert.Null(json["AllFormulas"]["QueryGroups"]);
            Assert.NotNull(json["AllFormulas"]["Relationships"]);
        }

        [Fact]
        public void Detects_and_emits_JArray()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Array, json["Formulas"]["Section1/Query1"]["FillColumnNames"].Type);
            Assert.Equal(new [] {"ID","Label"}, json["Formulas"]["Section1/Query1"]["FillColumnNames"].ToObject<string[]>());
        }

        [Fact]
        public void Detects_and_emits_JObject()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");
            Assert.Equal(JTokenType.Object, json["Formulas"]["Section1/Query1"]["RelationshipInfoContainer"].Type);
            Assert.Equal(2, json["Formulas"]["Section1/Query1"]["RelationshipInfoContainer"].Value<int>("columnCount"));
        }

        [Fact]
        public void Extracts_RootFormulaText_into_file()
        {
            SerializeFromResource("MashupMetadata_Simple.xml");

            Assert.True(_folder.ContainsPath("Metadata/Section1/Query1/RootFormulaText.m"));
            Assert.Equal("let\n    Source = #table(type table[ID = number, Label = text], {\n    { 1, \"Foo\" },\n    { 2, \"Bar\" }\n})\nin\n    Source"
                , _folder.GetAsString("Metadata/Section1/Query1/RootFormulaText.m"));
        }

        [Fact]
        public void Removes__RootFormulaText_and_ReferencedQueriesFormulaText__from_LastAnalysisServicesFormulaText_entry()
        {
            var json = SerializeFromResource("MashupMetadata_Simple.xml");

            var lastAnalysisServicesFormulaText = json["Formulas"]["Section1/Query1"]["LastAnalysisServicesFormulaText"] as JObject;
            Assert.Single(lastAnalysisServicesFormulaText.Properties());
            Assert.NotNull(lastAnalysisServicesFormulaText["IncludesReferencedQueries"]);
        }

        [Fact]
        public void Escapes_invalid_directoryname_characters()
        {
            SerializeFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

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
            SerializeFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

            // Section1/Revenue/Added%20%5BRevenue%20Type%5D%20%28Recognized%29
            Assert.True(_folder.ContainsPath("Metadata/Section1/Revenue/Added [Revenue Type] (Recognized)/ReferencedQueries/Query0.m"));
            Assert.Equal("42", _folder.GetAsString("Metadata/Section1/Revenue/Added [Revenue Type] (Recognized)/ReferencedQueries/Query0.m"));
        }

        [Fact]
        public void Escapes_invalid_filename_characters_in_ReferencedQueries()
        {
            SerializeFromResource("MashupMetadata_LastAnalysisServicesFormulaText.xml");

            // &quot;Has / Special \u0026 Characters&quot;
            Assert.True(_folder.ContainsPath("Metadata/Section1/Change Log/ReferencedQueries/Has %2F Special & Characters.m"));
        }
    }
}
