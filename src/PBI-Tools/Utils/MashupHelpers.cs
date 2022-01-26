// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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