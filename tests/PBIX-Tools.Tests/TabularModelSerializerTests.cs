using System.IO;
using System.Xml;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbixTools.Tests
{
    public class TabularModelSerializerTests : HasTempFolder
    {

        #region Tables

        [Fact]
        public void ProcessTables__RemovesTablesArrayFromOriginalJson()
        {
            var folder = new MockProjectFolder();
            var db = JObject.Parse(@"
{
    ""model"": {
        ""tables"": [],
        ""relationships"": []
    }
}");
            var db2 = TabularModelSerializer.ProcessTables(db, folder, new TabularModelIdCache(TestFolder.Path, new JArray()));

            Assert.Null(db2.Value<JObject>("model").Property("tables"));
        }

        [Fact]
        public void ProcessTables__CreatesFolderForEachTableUsingName()
        {
            var folder = new MockProjectFolder();
            var db = JObject.Parse(@"
{
    ""model"": {
        ""tables"": [
            { ""name"" : ""table1"" } ,
            { ""name"" : ""table2"" }
        ]
    }
}");
            TabularModelSerializer.ProcessTables(db, folder, new TabularModelIdCache(TestFolder.Path, new JArray()));

            Assert.True(folder.ContainsPath(@"tables\table1\table1.json"));
            Assert.True(folder.ContainsPath(@"tables\table2\table2.json"));
        }
        

            #endregion

        #region Measures

        [Fact]
        public void ProcessMeasures__DoesNothingIfThereAreNoMeasures()
        {
            var table = new JObject {};
            var folder = new MockProjectFolder();
            var result =
                TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1\");

            Assert.Equal(table.ToString(), result.ToString());
            Assert.Equal(0, folder.NumberOfFilesWritten);
        }

        [Fact]
        public void ProcessMeasures__CreatesFileForEachMeasure()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""measures"": [
        { ""name"" : ""measure1"" } ,
        { ""name"" : ""measure2"" }
    ]
}");
            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1\");

            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure1.xml"));
            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure2.xml"));
        }

        [Fact]
        public void ProcessMeasures__RemovesMeasuresFromTableJson()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""measures"": [
        { ""name"" : ""measure1"" } ,
        { ""name"" : ""measure2"" }
    ]
}");
            var folder = new MockProjectFolder();
            var result =
                TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1\");

            Assert.Null(result.Property("measure"));
        }

        [Fact]
        public void ProcessMeasures__ConvertsAnnotationXmlToNativeXml()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""measures"": [
        {
            ""name"" : ""measure1"",
            ""expression"" : ""COUNTROWS(Date)"",
            ""annotations"" : [
                {
                    ""name"" : ""Format"",
                    ""value"" : ""<Format Format=\""Text\"" />""
                }
            ]
        }
    ]
}");
            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1");

            var xml = folder.GetAsXml(@"tables\table1\measures\measure1.xml");
            Assert.Equal("Text", xml.XPathSelectElement("Measure/Annotation[@Name='Format']/Format").Attribute("Format").Value);
        }

        [Fact]
        public void ProcessMeasures__ConvertsExpressionToCDATA()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""measures"": [
        {
            ""name"" : ""measure1"",
            ""expression"" : [
                ""CALCULATE ("",
                ""    [SalesAmount],"",
                ""    ALLSELECTED ( Customer[Occupation] )"",
                "")""
            ]
        }
    ]
}");
            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1");

            var xml = folder.GetAsXml(@"tables\table1\measures\measure1.xml");
            var expression = xml.XPathSelectElement("Measure/Expression");
            Assert.Equal(XmlNodeType.CDATA, expression.FirstNode.NodeType); 
            Assert.Equal("CALCULATE (\n    [SalesAmount],\n    ALLSELECTED ( Customer[Occupation] )\n)", expression.Value);
        }

        [Fact]
        public void ProcessMeasures__ConvertsSingleLineExpression()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""measures"": [
        {
            ""name"" : ""measure1"",
            ""expression"" : ""CALCULATE ([SalesAmount], ALLSELECTED ( Customer[Occupation] ) )""
        }
    ]
}");
            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessMeasures(table, folder, @"tables\table1");

            var xml = folder.GetAsXml(@"tables\table1\measures\measure1.xml");
            var expression = xml.XPathSelectElement("Measure/Expression").Value;
            Assert.Equal("CALCULATE ([SalesAmount], ALLSELECTED ( Customer[Occupation] ) )", expression);
        }
        

            #endregion

        #region Hierarchies

        [Fact]
        public void ProcessHierarchies__CreatesFileForEachHierarchy()
        {
            var table = JObject.Parse(@"
{
    ""name"": ""table1"",
    ""hierarchies"": [
        { ""name"" : ""hierarchy1"" } ,
        { ""name"" : ""hierarchy2"" }
    ]
}");
            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessHierarchies(table, folder, @"tables\table1\");

            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy1.json"));
            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy2.json"));
        }



        #endregion



        [Fact]
        public void ProcessDataSources__ReplaceDatasourceNamePropertyWithStaticLookupValue()
        {
            // We've got an existing pbixproj file with dataSource mappings:
            File.WriteAllText(
                Path.Combine(TestFolder.Path, TabularModelIdCache.FileName), 
                new JObject
                {
                    { "dataSources", new JObject
                    {
                        { "Table1", "09a6f778-cfbd-4153-9354-15085bdbf371" },
                        { "Table2", "d85453b6-f15f-4116-9a65-cad48a4bc967" },
                    }}
                }.ToString());

            var db = new JObject
            {
                { "model", new JObject
                {
                    { "dataSources", new JArray(
                        new JObject
                        {
                            { "name", "1b4f67fe-4f1c-4828-9971-b258a79f5b79" },
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table1" ) }
                        },
                        new JObject
                        {
                            { "name", "a6312d03-f6eb-4455-bfd4-04f472995193" },
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table2" ) }
                        }
                    )}
                }}
            };

            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessDataSources(db, folder, new TabularModelIdCache(TestFolder.Path, db.SelectToken("model.dataSources") as JArray));

            Assert.True(folder.ContainsPath(@"dataSources\Table1\dataSource.json"));
            Assert.True(folder.ContainsPath(@"dataSources\Table2\dataSource.json"));
        }

        [Fact]
        public void ProcessDataSources__UsesLocationNameForDatasourceFolder()
        {
            // We've got an existing pbixproj file with dataSource mappings:
            File.WriteAllText(
                Path.Combine(TestFolder.Path, TabularModelIdCache.FileName),
                new JObject
                {
                    { "dataSources", new JObject
                    {
                        { "Table1", "09a6f778-cfbd-4153-9354-15085bdbf371" },
                        { "Table2", "d85453b6-f15f-4116-9a65-cad48a4bc967" },
                    }}
                }.ToString());

            var db = new JObject
            {
                { "model", new JObject
                {
                    { "dataSources", new JArray(
                        new JObject
                        {
                            { "name", "1b4f67fe-4f1c-4828-9971-b258a79f5b79" }, // those dataSource names are volatile and will be dropped completely
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table1" ) }
                        },
                        new JObject
                        {
                            { "name", "a6312d03-f6eb-4455-bfd4-04f472995193" },
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table2" ) }
                        }
                    )}
                }}
            };

            var folder = new MockProjectFolder();
            TabularModelSerializer.ProcessDataSources(db, folder, new TabularModelIdCache(TestFolder.Path, db.SelectToken("model.dataSources") as JArray));

            var ds1 = folder.GetAsJson(@"dataSources\Table1\dataSource.json");
            var ds2 = folder.GetAsJson(@"dataSources\Table2\dataSource.json");

            // Expecting 
            Assert.Equal("09a6f778-cfbd-4153-9354-15085bdbf371", ds1["name"].Value<string>());
            Assert.Equal("d85453b6-f15f-4116-9a65-cad48a4bc967", ds2["name"].Value<string>());
        }

        // Test: Sanitize filenames
        // Test: Table partitions (dataSource id replace)
        //
        // *** Data Sources
        // Mashup extraction
        // non-PBI connstr's are kept as-is
        // using idCache lookup for {name}
        // #(cr,lf) conversion .. start simple -- PBID only using (tab) and (cr,lf)
        // Global Pipe drop
    }

}
