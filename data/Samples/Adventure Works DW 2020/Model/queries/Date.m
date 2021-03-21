let
    Source = Sql.Database(SqlServerInstance, SqlServerDatabase),
    dbo_DimDate = Source{[Schema="dbo",Item="DimDate"]}[Data],
    #"Removed Other Columns" = Table.SelectColumns(dbo_DimDate,{"DateKey", "FullDateAlternateKey", "DayNumberOfMonth", "MonthNumberOfYear", "CalendarYear", "FiscalQuarter", "FiscalYear"}),
    #"Renamed Columns" = Table.RenameColumns(#"Removed Other Columns",{{"FullDateAlternateKey", "Date"}, {"FiscalYear", "Fiscal Year"}}),
    #"Added FY Prefix" = Table.TransformColumns(#"Renamed Columns", {{"Fiscal Year", each "FY" & Text.From(_, "en-US"), type text}}),
    #"Added Fiscal Quarter" = Table.AddColumn(#"Added FY Prefix", "Fiscal Quarter", each [Fiscal Year] & " Q" & Number.ToText([FiscalQuarter])),
    #"Added Month" = Table.AddColumn(#"Added Fiscal Quarter", "Month", each (Number.ToText([CalendarYear]) & " " & Date.ToText([Date], "MMM", Culture)), type text),
    #"Added Full Date" = Table.AddColumn(#"Added Month", "Full Date", each [Month] & ", " & Text.PadStart(Number.ToText([DayNumberOfMonth]), 2, "0")),
    #"Added MonthKey" = Table.AddColumn(#"Added Full Date", "MonthKey", each ([CalendarYear] * 100) + [MonthNumberOfYear]),
    #"Removed Other Columns1" = Table.SelectColumns(#"Added MonthKey",{"DateKey", "Date", "Fiscal Year", "Fiscal Quarter", "Month", "Full Date", "MonthKey"}),
    #"Changed Type" = Table.TransformColumnTypes(#"Removed Other Columns1",{{"Fiscal Quarter", type text}, {"Month", type text}, {"Full Date", type text}, {"MonthKey", Int64.Type}})
in
    #"Changed Type"