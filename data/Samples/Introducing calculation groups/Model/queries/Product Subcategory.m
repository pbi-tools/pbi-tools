let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    #"DaxBook_Product Subcategory" = Source{[Schema="DaxBook",Item="Product Subcategory"]}[Data]
in
    #"DaxBook_Product Subcategory"