using System;
using System.IO;
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

        void IDisposable.Dispose()
        {
            if (!Delete) return;
            Directory.Delete(Path, recursive: true);
            Log.Verbose("Deleted TEMP folder at {Path}", Path);
            // Provide failsafe Dispose() -- catch and log any errors, rather than re-throw
        }
    }
}
