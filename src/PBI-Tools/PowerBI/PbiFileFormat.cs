// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.


namespace PbiTools.PowerBI
{
    public enum PbiFileFormat
    {
        [PowerArgs.ArgDescription("Creates a file using the PBIX format. Only supported for \"thin\" reports - use the PBIT format if the project contains a data model. This is the default format.")]
        PBIX = 1,
        [PowerArgs.ArgDescription("Creates a file using the PBIT format. Use for data models. When opened in Power BI Desktop, parameters and/or credentials need to be provided and a refresh is triggered.")]
        PBIT = 2
    }
}