using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbixTools.Tests
{
    public class TabularModelSerializerTests : IDisposable
    {
        private readonly TempFolder _tmp = new TempFolder();

        public void Dispose()
        {
            ((IDisposable)_tmp).Dispose();
        }

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
            var db2 = TabularModelSerializer.ProcessTables(db, folder, new TabularModelIdCache(_tmp.Path, new JArray()));

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
            TabularModelSerializer.ProcessTables(db, folder, new TabularModelIdCache(_tmp.Path, new JArray()));

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
        public void ProcessDataSources__CreatesSubfolderForEachDataSource()
        {
            var db = new JObject
            {
                { "model", new JObject
                {
                    { "dataSources", new JArray(
                        new JObject
                        {
                            { "name", "ds1" },
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table1" ) }
                        },
                        new JObject
                        {
                            { "name", "ds2" },
                            { "connectionString", MashupHelpers.BuildPowerBIConnectionString(TestData.GlobalPipe, TestData.MinimalMashupPackageBytes, "Table2" ) }
                        }
                    ) }
                }}
            };

            var folder = new MockProjectFolder();
            var db2 = TabularModelSerializer.ProcessDataSources(db, folder, new TabularModelIdCache(_tmp.Path, db.SelectToken("model.dataSources") as JArray));

            Assert.True(folder.ContainsPath(@"dataSources\ds1\ds1.json"));
            Assert.True(folder.ContainsPath(@"dataSources\ds2\ds2.json"));
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
