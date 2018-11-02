using System;
using System.IO;

namespace PbiTools.Utils
{
    public class TempFolder : IDisposable
    {
        public string Path { get; }
        public bool Delete { get; set; } = true;

        public TempFolder()
        {
            var tempPath = System.IO.Path.GetTempFileName();
            File.Delete(tempPath);
            Directory.CreateDirectory(tempPath);
            Path = tempPath;
        }

        void IDisposable.Dispose()
        {
            if (!Delete) return;
            Directory.Delete(Path, recursive: true);
            // Provide failsafe Dispose() -- catch and log any errors, rather than re-throw
        }
    }
}
