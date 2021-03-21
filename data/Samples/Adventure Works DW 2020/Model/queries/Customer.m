let
    Source = Sql.Database(SqlServerInstance, SqlServerDatabase),
    dbo_DimCustomer = Source{[Schema="dbo",Item="DimCustomer"]}[Data],
    #"Removed Other Columns" = Table.SelectColumns(dbo_DimCustomer,{"CustomerKey", "CustomerAlternateKey", "FirstName", "LastName", "DimGeography"}),
    #"Expanded DimGeography" = Table.ExpandRecordColumn(#"Removed Other Columns", "DimGeography", {"City", "StateProvinceName", "EnglishCountryRegionName", "PostalCode"}, {"City", "StateProvinceName", "EnglishCountryRegionName", "PostalCode"}),
    #"Merged Columns" = Table.CombineColumns(#"Expanded DimGeography",{"FirstName", "LastName"},Combiner.CombineTextByDelimiter(" ", QuoteStyle.None),"Customer"),
    #"Add NA Row" = Table.InsertRows(#"Merged Columns", 0, {[CustomerKey = -1, CustomerAlternateKey = "[Not Applicable]", Customer = "[Not Applicable]", City = "[Not Applicable]", StateProvinceName ="[Not Applicable]", EnglishCountryRegionName ="[Not Applicable]", PostalCode ="[Not Applicable]"]}),
    #"Renamed Columns" = Table.RenameColumns(#"Add NA Row",{{"CustomerAlternateKey", "Customer ID"}, {"StateProvinceName", "State-Province"}, {"EnglishCountryRegionName", "Country-Region"}, {"PostalCode", "Postal Code"}})
in
    #"Renamed Columns"