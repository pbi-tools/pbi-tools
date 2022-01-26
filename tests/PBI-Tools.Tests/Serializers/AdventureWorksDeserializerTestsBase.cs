// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using TOM = Microsoft.AnalysisServices.Tabular;
using Xunit;

namespace PbiTools.Tests
{
    public abstract class AdventureWorksDeserializerTestsBase
    {
        protected AdventureWorksDeserializerTestsBase(TOM.Model model)
        {
            this._model = model;
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

        [TestNotImplemented]
        public void Deserialize_Hierarchy()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Measure()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Shared_Expression()
        {
            //Given

            //When

            //Then
        }

        [Fact]
        public void Deserialize_Column_Annotations()
        {
            var table = _model.Tables.Find("Customer");
            var column = table.Columns.Find("City");
            var annotation = column.Annotations.Find("SummarizationSetBy");

            Assert.Equal("Automatic", annotation.Value);
        }

        [TestNotImplemented]
        public void Deserialize_Table_Partitions()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Table_Partition_QueryGroup()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Cultures()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Relationships()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Roles()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Translations()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Table_CalculationItems()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_Perspectives()
        {
            //Given

            //When

            //Then
        }

        [TestNotImplemented]
        public void Deserialize_DataSources()
        {
            //Given

            //When

            //Then
        }
    }

}