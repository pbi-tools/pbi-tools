let
    Source = Sql.Database(SqlServerInstance, SqlServerDatabase),
    dbo_DimProduct = Source{[Schema="dbo",Item="DimProduct"]}[Data],
    #"Filtered Rows" = Table.SelectRows(dbo_DimProduct, each ([FinishedGoodsFlag] = true)),
    #"Removed Other Columns" = Table.SelectColumns(#"Filtered Rows",{"ProductKey", "ProductAlternateKey", "EnglishProductName", "StandardCost", "Color", "ListPrice", "ModelName", "DimProductSubcategory"}),
    #"Expanded DimProductSubcategory" = Table.ExpandRecordColumn(#"Removed Other Columns", "DimProductSubcategory", {"EnglishProductSubcategoryName", "DimProductCategory"}, {"EnglishProductSubcategoryName", "DimProductCategory"}),
    #"Expanded DimProductCategory" = Table.ExpandRecordColumn(#"Expanded DimProductSubcategory", "DimProductCategory", {"EnglishProductCategoryName"}, {"EnglishProductCategoryName"}),
    #"Renamed Columns" = Table.RenameColumns(#"Expanded DimProductCategory",{{"EnglishProductName", "Product"}, {"StandardCost", "Standard Cost"}, {"ListPrice", "List Price"}, {"ModelName", "Model"}, {"EnglishProductSubcategoryName", "Subcategory"}, {"EnglishProductCategoryName", "Category"}, {"ProductAlternateKey", "SKU"}})
in
    #"Renamed Columns"