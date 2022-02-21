// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;

namespace PbiTools.Utils
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the file name of the specified path string without the extension.
        /// </summary>
        public static string WithoutExtension(this string path) =>
            Path.GetFileNameWithoutExtension(path ?? throw new ArgumentNullException(nameof(path)));

    }
}