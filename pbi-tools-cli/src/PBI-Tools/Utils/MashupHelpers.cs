using System;

namespace PbiTools.Utils
{
    public static class MashupHelpers
    {

        // ReSharper disable once InconsistentNaming
        public static string BuildPowerBIConnectionString(string globalPipe, byte[] mashup, string location)
        {
            var bldr = new System.Data.OleDb.OleDbConnectionStringBuilder
            {
                Provider = "Microsoft.PowerBI.OleDb",
            };
            bldr.Add("Global Pipe", globalPipe);
            bldr.Add("Mashup", Convert.ToBase64String(mashup));
            bldr.Add("Location", location);

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
            return m.Replace("#(lf)", "\n").Replace("#(tab)", "\t");
        }
    }
}