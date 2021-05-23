// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PbiTools.Serialization
{ 
    public static class PathExtensions
    {

        private static readonly Dictionary<char, string> FilenameCharReplace = "\"<>|:*?/\\".ToCharArray().ToDictionary(c => c, c => $"%{((int)c):X}");
        // Note - This can be reversed via WebUtility.UrlDecode()

        public static string SanitizeFilename(this string name)
        {
            if (name == null) return default(string);
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if (FilenameCharReplace.TryGetValue(c, out var s))
                    sb.Append(s);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string UnsanitizeFilename(this string name) => System.Net.WebUtility.UrlDecode(name);

    }

}