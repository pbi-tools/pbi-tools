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

using System;

namespace PbiTools.Utils
{
    public static class MashupHelpers
    {

        // ReSharper disable once InconsistentNaming
        public static string BuildPowerBIConnectionString(string globalPipe, byte[] mashup, string location)
        {
            var bldr = new System.Data.Common.DbConnectionStringBuilder
            {
                { "Provider", "Microsoft.PowerBI.OleDb" },
                { "Global Pipe", globalPipe },
                { "Mashup", Convert.ToBase64String(mashup) },
                { "Location", location }
            };

            return bldr.ConnectionString;
        }

        public static string ReplaceEscapeSeqences(string m)
        {
            // TODO Make this recognize all possible M escape sequences
            // M character escape sequences: https://msdn.microsoft.com/en-us/library/mt807488.aspx
            // #( .. )
            //   #(cr,lf)
            //   #(cr)
            //   #(tab)
            //   #(#)
            //   #(000D)
            //   #(0000000D)
            return m
                .Replace("#(lf)", "\n")
                .Replace("#(tab)", "\t");
        }
    }
}
