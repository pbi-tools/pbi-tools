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
    /// Extracts the embedded 'Adventure Works DW 2020' Pbix Project, and deserializes the model into a TOM.Model to run tests against.
    /// </summary>
    public class TabularModelDeserializerFixture : HasTempFolder
    {

        public TabularModelDeserializerFixture()
        {
            using (var zip = new ZipArchive(Resources.GetEmbeddedResourceStream("Adventure Works DW 2020.zip")))
            {
                zip.ExtractToDirectory(this.TestFolder.Path);
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

    public class TabularModelDeserializerTests : IClassFixture<TabularModelDeserializerFixture>
    {
        public TabularModelDeserializerTests(TabularModelDeserializerFixture fixture)
        {
            this._model = fixture.Model;
        }

        private readonly TOM.Model _model;

        [Fact]
        public void ContainsAllTables()
        {
            var tablesExpected = new[] { "Customer", "Date", "Product", "Reseller", "Sales", "Sales by Color and Business Type", "Sales Order", "Sales Territory" };
            Assert.All(
                tablesExpected,
                table => Assert.True(_model.Tables.Contains(table))
            );
        }

        [Theory]
        [MemberData(nameof(TablesAndColumns))]
        public void ContainsColumns(string tableName, string[] columns)
        {
            var table = _model.Tables.Find(tableName);
            Assert.All(
                columns,
                column => Assert.True(table.Columns.Contains(column))
            );
        }

        public static IEnumerable<object[]> TablesAndColumns =>
            new List<object[]> {
                new object[] { "Customer", new [] { "City", "Country-Region", "Customer ID", "Customer", "CustomerKey", "Postal Code", "State-Province" } },
                new object[] { "Date", new [] { "Date", "DateKey", "Fiscal Quarter", "Fiscal Year", "Full Date", "Is Current Month", "Month", "MonthKey" } },
            };

        [Fact]
        public void Deserialize_Calculated_Column()
        {
            var table = _model.Tables.Find("Date");
            var column = table.Columns.Find("Is Current Month");

            Assert.Equal(TOM.ColumnType.Calculated, column.Type);

            var calculatedColumn = column as TOM.CalculatedColumn;
            Assert.Equal("MONTH([Date]) = MONTH(TODAY()) && YEAR([Date]) = YEAR(TODAY())", calculatedColumn.Expression);
        }

        [Fact]
        public void Deserialize_Calculated_Table()
        {
            var table = _model.Tables.Find("Sales by Color and Business Type");
            var partition = table.Partitions.Single();

            Assert.Equal(TOM.ModeType.Import, partition.Mode);
            Assert.Equal(TOM.PartitionSourceType.Calculated, partition.SourceType);

            var source = partition.Source as TOM.CalculatedPartitionSource;
            var expectedExpr = @"SUMMARIZECOLUMNS (
    Product[Color],
    Reseller[Business Type],
    FILTER ( ALL ( Product[List Price] ), Product[List Price] > 150.00 ),
    TREATAS ( { ""Accessories"", ""Bikes"" }, 'Product'[Category] ),
    ""Total Sales"", SUM ( Sales[Sales Amount] )
)";
            Assert.Equal(
                expectedExpr.Replace("\r\n", "\n"),
                source.Expression.Replace("\r\n", "\n")
            );
        }

        [Fact]
        public void Deserialize_Hierarchy()
        {
            //Given

            //When

            //Then
        }

        [Fact]
        public void Deserialize_Measure()
        {
            //Given

            //When

            //Then
        }

        [Fact]
        public void Deserialize_Shared_Expression()
        {
            //Given

            //When

            //Then
        }

        public class V0_5__DeserializerTests
        {
            [Fact]
            public void Deserializes_expression_from_xml()
            {
            }
        }

    }

}