// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using Xunit;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Tests
{
    using Serialization;
    using Utils;

    public class TabularModelSerializerTests : HasTempFolder
    {
        private readonly MockProjectFolder folder = new MockProjectFolder();


        #region Tables

        [Fact]
        public void SerializeTables__RemovesTablesArrayFromOriginalJson()
        {
            var db = JObject.Parse(@"
{
    ""model"": {
        ""tables"": [],
        ""relationships"": []
    }
}");
            var db2 = TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup(), new());

            Assert.Null(db2.Value<JObject>("model").Property("tables"));
        }

        [Fact]
        public void SerializeTables__CreatesFolderForEachTableUsingName()
        {
            var db = JObject.Parse(@"
{
    ""model"": {
        ""tables"": [
            { ""name"" : ""table1"" } ,
            { ""name"" : ""table2"" }
        ]
    }
}");
            TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup(), new());

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

            TabularModelSerializer.SerializeTables(db, folder, new MockQueriesLookup(), new());

            Assert.True(folder.ContainsPath($@"tables\{expectedFolderName}\table.json"));
        }


        [Fact]
        public void SerializeTables__CreatesDaxFileForCalculatedTables()
        {
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
            var result = TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\", new());

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

            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\", new());

            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure1.json"));
            Assert.True(folder.ContainsPath(@"tables\table1\measures\measure2.json"));
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

            var result = TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1\", new());

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

            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1", new() { Format = ProjectSystem.ModelMeasureSerializationFormat.Xml });

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
            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1", new());

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

            TabularModelSerializer.SerializeMeasures(table, folder, @"tables\table1", new());

            var expression = folder.GetAsString(@"tables\table1\measures\measure1.dax");
            Assert.Equal("CALCULATE ([SalesAmount], ALLSELECTED ( Customer[Occupation] ) )", expression);
        }

        [Fact]
        public void DeserializeMeasures__ExtendedProperties()
        {
            var table = new TOM.Table { Name = "Table1" };
            table.Measures.Add(
                TOM.JsonSerializer.DeserializeObject<TOM.Measure>(
                    Resources.GetEmbeddedResourceString("measure--extendedProperties.json")));

            var tableJsonSrc = JObject.Parse(TOM.JsonSerializer.SerializeObject(table));

            var tableJsonOut = new JObject {
                { "name", "Table1" },
            };

            using (var testFolder = new FileSystem.ProjectRootFolder(TestFolder.Path))
            {
                var modelFolder = testFolder.GetFolder(TabularModelSerializer.FolderName);

                TabularModelSerializer.SerializeMeasures(tableJsonSrc, modelFolder, @"table", new());

                TabularModelSerializer.DeserializeMeasures(modelFolder.GetSubfolder("table"), tableJsonOut);
            }

            // Assert expectations via TOM API
            var tableOut = TOM.JsonSerializer.DeserializeObject<TOM.Table>(
                tableJsonOut.ToString()
            );

            var measure = tableOut.Measures[0];
            Assert.Collection(measure.ExtendedProperties,
                x1 => Assert.Equal(TOM.ExtendedPropertyType.Json, x1.Type)
            );

            var prop = measure.ExtendedProperties[0] as TOM.JsonExtendedProperty;
            Assert.NotNull(prop);
            Assert.Equal("MeasureTemplate", prop.Name);

            var propValue = JObject.Parse(prop.Value);
            Assert.Equal("FilteredMeasure", propValue["daxTemplateName"]);
            Assert.Equal(0, propValue["version"]);
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

            TabularModelSerializer.SerializeHierarchies(table, folder, @"tables\table1\");

            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy1.json"));
            Assert.True(folder.ContainsPath(@"tables\table1\hierarchies\hierarchy2.json"));
        }



        #endregion

        #region Partitions

        public class Partitions
        { 
            [TestNotImplemented]
            public void Serializes_M_Partitions()
            {
                //Given

                //When

                //Then
            }

            [TestNotImplemented]
            public void Serializes_CalculationGroup_Partitions()
            {
                //Given

                //When

                //Then
            }

            [TestNotImplemented]
            public void Serializes_Calculated_Partitions()
            {
                //Given

                //When

                //Then
            }

            [TestNotImplemented]
            public void Serializes_PolicyRange_Partitions()
            {
                //Given

                //When

                //Then
            }

            [TestNotImplemented]
            public void Serializes_Entity_Partitions()
            {
                //Given

                //When

                //Then
            }

            [TestNotImplemented]
            public void Serializes_Query_Partitions()
            {
                //Given

                //When

                //Then
            }

        }

        #endregion

        #region Perspectives
        #endregion

        #region Roles
        #endregion

        #region Expressions
        #endregion

        #region Annotations

        public class AnnotationRules
        {
            private static JArray CreateAnnotations(params string[] names) =>
                new(names.Select(name => new JObject {
                    { "name", name },
                    { "value", $"{Guid.NewGuid()}" } 
                }));

            [Fact]
            public void DoesNothingWhenRulesAreNull() 
            {
                new JObject().ApplyAnnotationRules(default);
            }

            [Fact]
            public void RemovesAnnotationsPropertyWhenAllAnnotationsAreExcluded()
            {
                var result = JsonTransforms.ApplyAnnotationRules(
                    new JObject {
                        { "annotations", CreateAnnotations("Foo1", "foo_2") }
                    }, 
                    new ProjectSystem.ModelAnnotationSettings {
                        Exclude = new[] { "foo*" }
                    }
                );

                // 'annotations' property removed since both annotions were excluded
                Assert.Empty(result.Properties());
            }

            [Fact]
            public void AnnotationRulesAreCaseInsensitive()
            {
                var result = JsonTransforms.ApplyAnnotationRules(
                    new JObject {
                        { "annotations", CreateAnnotations(
                            "PBI_Annotation1", 
                            "PBI__VERSION"
                        ) }
                    },
                    new ProjectSystem.ModelAnnotationSettings
                    {
                        Exclude = new[] { "pbi_*" },
                        Include = new[] { "pbi__version" }
                    }
                );

                // Both annotations matched ignoring casing
                // One annotation remains because of Include rule
                Assert.Collection(result.SelectTokens("$.annotations[*].name"),
                    t => Assert.Equal("PBI__VERSION", t));
            }

            [Fact]
            public void AllowsWildcardsInIncludeRules()
            {
                var result = JsonTransforms.ApplyAnnotationRules(
                    new JObject {
                        { "annotations", CreateAnnotations(
                            "PBI_Annotation_1",
                            "PBI_Annotation_10",
                            "PBI__VERSION"
                        ) }
                    },
                    new ProjectSystem.ModelAnnotationSettings
                    {
                        Exclude = new[] { "pbi_*" },
                        Include = new[] { "pbi_annotation_?" }
                    }
                );

                // Both annotations matched ignoring casing
                // One annotation remains because of Include rule
                Assert.Collection(result.SelectTokens("$.annotations[*].name"),
                    t => Assert.Equal("PBI_Annotation_1", t));
            }

            [Fact]
            public void DoesNothingWithoutExcludeRules()
            {
                var result = JsonTransforms.ApplyAnnotationRules(
                    new JObject {
                        { "annotations", CreateAnnotations(
                            "PBI_Annotation1",
                            "PBI__VERSION"
                        ) }
                    },
                    new ProjectSystem.ModelAnnotationSettings
                    {
                        Include = new[] { "pbi__version" }
                    }
                );

                // Only 'Include' rules provided: Json object is not modified
                Assert.Collection(result.SelectTokens("$.annotations[*].name"),
                    t => Assert.Equal("PBI_Annotation1", t),
                    t => Assert.Equal("PBI__VERSION", t)
                );
            }

        }

        #endregion

#if NETFRAMEWORK
        public class SerializeLegacyDataSources
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

            public SerializeLegacyDataSources()
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

                var connStr1 = new DbConnectionStringBuilder { ConnectionString = table1["connectionString"].Value<string>() };
                Assert.False(connStr1.ContainsKey("Global Pipe"));
                var connStr2 = new DbConnectionStringBuilder { ConnectionString = table2["connectionString"].Value<string>() };
                Assert.False(connStr2.ContainsKey("Global Pipe"));
            }

            [Fact]
            public void RemovesMashupFromConnectionString()
            {
                var table1 = _folder.GetAsJson(@"dataSources\Table1\dataSource.json");
                var table2 = _folder.GetAsJson(@"dataSources\Table2\dataSource.json");

                var connStr1 = new DbConnectionStringBuilder { ConnectionString = table1["connectionString"].Value<string>() };
                Assert.False(connStr1.ContainsKey("Mashup"));
                var connStr2 = new DbConnectionStringBuilder { ConnectionString = table2["connectionString"].Value<string>() };
                Assert.False(connStr2.ContainsKey("Mashup"));
            }

        }
#endif
    }

}
