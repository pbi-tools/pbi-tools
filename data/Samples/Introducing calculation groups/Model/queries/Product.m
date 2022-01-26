let
    Source = Sql.Database("Demo", "ContosoRetailDW"),
    DaxBook_Product = Source{[Schema="DaxBook",Item="Product"]}[Data],
    #"Removed Columns" = Table.RemoveColumns(DaxBook_Product,{"Class", "Style", "Size", "Stock Type Code", "Stock Type", "Status", "Product Description"}),
    #"Merged Queries" = Table.NestedJoin(#"Removed Columns",{"ProductSubcategoryKey"},#"Product Subcategory",{"ProductSubcategoryKey"},"Product Subcategory",JoinKind.LeftOuter),
    #"Expanded Product Subcategory" = Table.ExpandTableColumn(#"Merged Queries", "Product Subcategory", {"Subcategory", "ProductCategoryKey"}, {"Subcategory", "ProductCategoryKey"}),
    #"Removed Columns1" = Table.RemoveColumns(#"Expanded Product Subcategory",{"ProductSubcategoryKey"}),
    #"Merged Queries1" = Table.NestedJoin(#"Removed Columns1",{"ProductCategoryKey"},#"Product Category",{"ProductCategoryKey"},"Product Category",JoinKind.LeftOuter),
    #"Expanded Product Category" = Table.ExpandTableColumn(#"Merged Queries1", "Product Category", {"Category"}, {"Category"}),
    #"Removed Columns2" = Table.RemoveColumns(#"Expanded Product Category",{"ProductCategoryKey", "Available Date", "Unit Price", "Unit Cost", "Weight Unit Measure", "Weight" })
in
    #"Removed Columns2"