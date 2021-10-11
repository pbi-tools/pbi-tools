// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Data.OleDb;
using System.Xml;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using PbiTools.Serialization;
using PbiTools.Utils;
using Xunit;

#if NETFRAMEWORK
namespace PbiTools.Tests
{
    public class TabularModelSerializerTests : HasTempFolder
    {

        #region Tables

        [Fact]
        public void SerializeTables__RemovesTablesArrayFromOriginalJson()
        {
            var folder = new MockProjectFolder();
            var db = JObject.Parse(@"
{
    ""model"": {
        ""tables"": [],
        ""relationships"": []
    }
}");
            var db2 = TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup());

            Assert.Null(db2.Value<JObject>("model").Property("tables"));
        }

        [Fact]
        public void SerializeTables__CreatesFolderForEachTableUsingName()
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
            TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup());

            Assert.True(folder.ContainsPath(@"tables\table1\table.json"));
            Assert.True(folder.ContainsPath(@"tables\table2\table.json"));
        }

        [Theory]
        [InlineData(@"foo/bar", "foo%2Fbar")]
        [InlineData(@"foo\bar", "foo%5Cbar")]
        [InlineData(@"foo""bar", "foo%22bar")]
        [InlineData(@"foo<bar", "foo%3Cbar")]
        [InlineData(@"foo>bar", "foo%3Ebar")]
        [InlineData(@"foo|bar", "foo%7Cbar")]
        [InlineData(@"foo:bar", "foo%3Abar")]
        [InlineData(@"foo*bar", "foo%2Abar")]
        [InlineData(@"foo?bar", "foo%3Fbar")]
        public void SerializeTables__SanitizesTableName(string tableName, string expectedFolderName)
        {
            var folder = new MockProjectFolder();
            var db = new JObject 
            {
                { "model", new JObject {
                    { "tables", new JArray(
                        new JObject {
                            { "name", tableName} 
                        }
                    )}
                }}
            };

            TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup());

            Assert.True(folder.ContainsPath($@"tables\{expectedFolderName}\table.json"));
        }


        [Fact]
        public void SerializeTables__CreatesDaxFileForCalculatedTables()
        {
            var folder = new MockProjectFolder();
            var table = Resources.GetEmbeddedResourceFromString("table--calculated.json", JObject.Parse);
            var table2 = TabularModelSerializer.SerializeTablePartitions(table, folder, @"tables\calculated", new MockQueriesLookup());

            Assert.NotNull(table.SelectToken("partitions[0].source.expression"));
            Assert.Null(table2.SelectToken("partitions[0].source.expression"));

            // folder.ContainsFile(@"tables\calculated\table.dax");
            Assert.Equal(Resources.GetEmbeddedResourceString("table--calculated.dax"), folder.GetAsString(@"tables\calculated\table.dax"));
        }

        #endregion

        #region Columns

        public class SerializeColumnsTests
        {
            public SerializeColumnsTests()
            {
                var table = Resources.GetEmbeddedResourceFromString("table--with-calc-column.json", JObject.Parse);
                _tableJsonWithoutColumns = TabularModelSerializer.SerializeColumns(table, _folder, @"tables\calculated");
            }

            private readonly MockProjectFolder _folder = new MockProjectFolder();
            private readonly JObject _tableJsonWithoutColumns;
            private readonly string[] _columnNames = new[] {
                "DateKey",
                "Date",
                "Fiscal Year",
                "Fiscal Quarter",
                "Month",
                "MonthKey",
                "Full Date",
                "Is Current Month"
            };

            [Fact]
            public void RemovesColumnsArrayFromTable() 
            {
                Assert.Null(_tableJsonWithoutColumns["columns"]);
            }

            [Fact]
            public void CreatesJsonFileForEachColumn() 
            {
                Assert.All(_columnNames,
                    name => Assert.True(_folder.ContainsFile(@$"tables\calculated\columns\{name}.json"))
                );
            }

            [Fact]
            public void ColumnFilesContainValidJson() 
            {
                Assert.All(_columnNames,
                    name => _folder.GetAsJson(@$"tables\calculated\columns\{name}.json")
                );
            }

            [Fact]
            public void CreatesDaxFileForCalculatedColumns() 
            {
                var path = @"tables\calculated\columns\Is Current Month.dax";

                Assert.True(_folder.ContainsFile(path));
                Assert.Equal("MONTH([Date]) = MONTH(TODAY()) && YEAR([Date]) = YEAR(TODAY())", _folder.GetAsString(path));
            }

        }

        #endregion

        #region Measures

        [Fact]
        public void SerializeMeasures__DoesNothingIfThereAreNoMeasures()
        {
            var table = new JObject {};
            var folder = new MockProjectFolder();
            var result =
                TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\");

            Assert.Equal(table.ToString(), result.ToString());
            Assert.Equal(0, folder.NumberOfFilesWritten);
        }

        [Fact]
        public void SerializeMeasures__CreatesFileForEachMeasure()
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
            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\");

            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure1.xml"));
            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure2.xml"));
        }

        [Fact]
        public void SerializeMeasures__RemovesMeasuresFromTableJson()
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
                TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\");

            Assert.Null(result.Property("measure"));
        }

        [Fact]
        public void SerializeMeasures__ConvertsAnnotationXmlToNativeXml()
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
            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1");

            var xml = folder.GetAsXml(@"tables\table1\measures\measure1.xml");
            Assert.Equal("Text", xml.XPathSelectElement("Measure/Annotation[@Name='Format']/Format").Attribute("Format").Value);
        }

        [Fact]
        public void SerializeMeasures__ConvertsExpressionToDAXFile()
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
            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1");

            var expression = folder.GetAsString(@"tables\table1\measures\measure1.dax");
            Assert.Equal(
                "CALCULATE (\n    [SalesAmount],\n    ALLSELECTED ( Customer[Occupation] )\n)",
                expression.Replace("\r\n", "\n"));
        }

        [Fact]
        public void SerializeMeasures__ConvertsSingleLineExpression()
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
            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1");

            var expression = folder.GetAsString(@"tables\table1\measures\measure1.dax");
            Assert.Equal("CALCULATE ([SalesAmount], ALLSELECTED ( Customer[Occupation] ) )", expression);
        }
        

            #endregion

        #region Hierarchies

        [Fact]
        public void SerializeHierarchies__CreatesFileForEachHierarchy()
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
            TabularModelSerializer.SerializeHierarchies(table, folder, @"tables\table1\");

            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy1.json"));
            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy2.json"));
        }



        #endregion


        public class SerializeDataSources
        {
            private readonly JObject _databaseJson = new JObject
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

            private readonly IQueriesLookup _queriesLookup = new MockQueriesLookup(new Dictionary<string, string>
            {
                { "1b4f67fe-4f1c-4828-9971-b258a79f5b79", "09a6f778-cfbd-4153-9354-15085bdbf371" }, /* Table1 */
                { "a6312d03-f6eb-4455-bfd4-04f472995193", "d85453b6-f15f-4116-9a65-cad48a4bc967" }  /* Table2 */
            });

            private readonly MockProjectFolder _folder;

            public SerializeDataSources()
            {
                _folder = new MockProjectFolder();
                TabularModelSerializer.SerializeDataSources(_databaseJson, _folder, _queriesLookup);
            }

            [Fact]
            public void UsesLocationNameForDatasourceFolder()
            {
                Assert.True(_folder.ContainsPath(@"dataSources\Table1\dataSource.json"));
                Assert.True(_folder.ContainsPath(@"dataSources\Table2\dataSource.json"));
            }

            [Fact]
            public void ReplaceDatasourceNamePropertyWithStaticLookupValue()
            {
                var table1 = _folder.GetAsJson(@"dataSources\Table1\dataSource.json");
                var table2 = _folder.GetAsJson(@"dataSources\Table2\dataSource.json");

                Assert.Equal("09a6f778-cfbd-4153-9354-15085bdbf371", table1["name"].Value<string>());
                Assert.Equal("d85453b6-f15f-4116-9a65-cad48a4bc967", table2["name"].Value<string>());
            }

            [Fact]
            public void RemovesGlobalPipeFromConnectionString()
            {
                var table1 = _folder.GetAsJson(@"dataSources\Table1\dataSource.json");
                var table2 = _folder.GetAsJson(@"dataSources\Table2\dataSource.json");

                var connStr1 = new OleDbConnectionStringBuilder(table1["connectionString"].Value<string>());
                Assert.False(connStr1.ContainsKey("global pipe"));
                var connStr2 = new OleDbConnectionStringBuilder(table2["connectionString"].Value<string>());
                Assert.False(connStr2.ContainsKey("global pipe"));
            }

            [Fact]
            public void RemovesMashupFromConnectionString()
            {
                var table1 = _folder.GetAsJson(@"dataSources\Table1\dataSource.json");
                var table2 = _folder.GetAsJson(@"dataSources\Table2\dataSource.json");

                var connStr1 = new OleDbConnectionStringBuilder(table1["connectionString"].Value<string>());
                Assert.False(connStr1.ContainsKey("mashup"));
                var connStr2 = new OleDbConnectionStringBuilder(table2["connectionString"].Value<string>());
                Assert.False(connStr2.ContainsKey("mashup"));
            }

        }

    }

}
#endif