let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Date = Source{[Schema="DaxBook",Item="Date"]}[Data]
in
    DaxBook_Date