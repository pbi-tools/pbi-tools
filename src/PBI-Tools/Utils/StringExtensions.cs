// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    }
}