let
    Source = Sql.Database(SqlServerInstance, SqlServerDatabase),
    dbo_DimSalesTerritory = Source{[Schema="dbo",Item="DimSalesTerritory"]}[Data],
    #"Removed Other Columns" = Table.SelectColumns(dbo_DimSalesTerritory,{"SalesTerritoryKey", "SalesTerritoryRegion", "SalesTerritoryCountry", "SalesTerritoryGroup"}),
    #"Renamed Columns" = Table.RenameColumns(#"Removed Other Columns",{{"SalesTerritoryRegion", "Region"}, {"SalesTerritoryCountry", "Country"}, {"SalesTerritoryGroup", "Group"}})
in
    #"Renamed Columns"