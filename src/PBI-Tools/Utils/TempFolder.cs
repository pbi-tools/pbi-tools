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
using System.Linq;
using Polly;
using Serilog;

namespace PbiTools.Utils
{
    public class TempFolder : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<TempFolder>();

        public string Path { get; }
        public bool Delete { get; set; } = true;

        public TempFolder()
        {
            var path = System.IO.Path.GetTempFileName();
            File.Delete(path);
            Path = EnsureFolder(path);
        }

        public TempFolder(string path)
        {
            Path = EnsureFolder(path);
        }

        private string EnsureFolder(string path)
        {
            var tempPath = System.IO.Path.GetFullPath(path);
            Directory.CreateDirectory(tempPath);
            Log.Verbose("Created TEMP folder at {Path}", tempPath);
            return tempPath;
        }

        /// <summary>
        /// Combines the path segments specified with the base path of this instance and returns the resulting path.
        /// </summary>
        public string GetPath(params string[] paths) => System.IO.Path.Combine(new[] { this.Path }.Concat(paths).ToArray());

        void IDisposable.Dispose()
        {
            if (!Delete) return;

            Policy
                .Handle<IOException>()
                .Fallback(() => { }, ex => 
                {
                    Log.Warning(ex, "Failed to delete temp folder at {Path}.", this.Path);
                })
                .Execute(() =>
                {
                    Directory.Delete(Path, recursive: true);
                    Log.Verbose("Deleted TEMP folder at {Path}", Path);
                });
        }
    }
}
