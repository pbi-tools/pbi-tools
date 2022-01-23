let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Sales = Source{[Schema="DaxBook",Item="Sales"]}[Data],
    #"Removed Columns" = Table.RemoveColumns(DaxBook_Sales,{"OnlineSalesKey", "Due Date", "DueDateKey", "DeliveryDateKey"})
in
    #"Removed Columns"