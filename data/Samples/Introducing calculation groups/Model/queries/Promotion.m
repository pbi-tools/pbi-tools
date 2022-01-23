let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Promotion = Source{[Schema="DaxBook",Item="Promotion"]}[Data]
in
    DaxBook_Promotion