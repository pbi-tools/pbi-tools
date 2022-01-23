let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Customer = Source{[Schema="DaxBook",Item="Customer"]}[Data],
    #"Removed Columns" = Table.RemoveColumns(DaxBook_Customer,{"GeographyKey"})
in
    #"Removed Columns"