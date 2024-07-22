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
using System.IO;
using System.Text;

namespace PbiTools.Utils
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the file name of the specified path string without the extension.
        /// </summary>
        public static string WithoutExtension(this string path) =>
            Path.GetFileNameWithoutExtension(path ?? throw new ArgumentNullException(nameof(path)));

        /// <summary>
        /// Returns the file extension converted to lowercase.
        /// </summary>
        public static string GetExtension(this string path) =>
            Path.GetExtension(path ?? throw new ArgumentNullException(nameof(path)))?.ToLowerInvariant();

        public static string ToPascalCase(this string s)
        {
            var sb = new StringBuilder(s);
            if (sb.Length > 0) sb[0] = Char.ToUpper(s[0]);
            return sb.ToString();
        }

        public static string ToCamelCase(this string s)
        {
            var sb = new StringBuilder(s);
            if (sb.Length > 0) sb[0] = Char.ToLower(s[0]);
            return sb.ToString();
        }

        /// <summary>
        /// Returns <c>true</> if the value is not null or whitespace, otherwise <c>false</c>.
        /// </summary>
        public static bool HasValue(this string value) => !String.IsNullOrWhiteSpace(value);

    }
}
