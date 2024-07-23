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
