section Section1;

shared Query1 = let
    Source = #table(type table[ID = number, Label = text], {
    { 1, "Foo" },
    { 2, "Bar" }
})
in
    Source;