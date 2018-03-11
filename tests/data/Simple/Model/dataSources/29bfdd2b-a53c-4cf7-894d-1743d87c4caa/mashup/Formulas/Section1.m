section Section1;

shared Query1 = let
    Source = #table(type table[ID = number, Label = text], {
    { 1, "Foo" },
    { 2, "Bar" }
}),
    AutoRemovedColumns1 = 
    let
        t = Table.FromValue(Source, [DefaultColumnName = "Query1"]),
        removed = Table.RemoveColumns(t, Table.ColumnsOfType(t, {type table, type record, type list}))
    in
        Table.TransformColumnNames(removed, Text.Clean)
in
    AutoRemovedColumns1;