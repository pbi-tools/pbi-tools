/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

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
