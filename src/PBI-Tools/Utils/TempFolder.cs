// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
