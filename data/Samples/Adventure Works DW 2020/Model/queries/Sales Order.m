let
    Source = Sql.Database(SqlServerInstance, SqlServerDatabase),
    dbo_vFactSales = Source{[Schema="dbo",Item="vFactSales"]}[Data],
    #"Removed Other Columns" = Table.SelectColumns(dbo_vFactSales,{"Channel", "SalesOrderLineKey", "SalesOrderNumber", "SalesOrderLineNumber"}),
    #"Renamed Columns" = Table.RenameColumns(#"Removed Other Columns",{{"SalesOrderNumber", "Sales Order"}}),
    #"Added Custom" = Table.AddColumn(#"Renamed Columns", "Sales Order Line", each [Sales Order] & " - " & Text.PadStart(Number.ToText([SalesOrderLineNumber]), 2, "0")),
    #"Changed Type" = Table.TransformColumnTypes(#"Added Custom",{{"Sales Order Line", type text}}),
    #"Removed Columns" = Table.RemoveColumns(#"Changed Type",{"SalesOrderLineNumber"})
in
    #"Removed Columns"