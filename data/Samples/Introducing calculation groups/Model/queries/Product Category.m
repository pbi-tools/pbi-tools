let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    #"DaxBook_Product Category" = Source{[Schema="DaxBook",Item="Product Category"]}[Data]
in
    #"DaxBook_Product Category"