let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Store = Source{[Schema="DaxBook",Item="Store"]}[Data],
    #"Removed Columns" = Table.RemoveColumns(DaxBook_Store,{"GeographyKey", "Store Manager", "StoreFax"})
in
    #"Removed Columns"