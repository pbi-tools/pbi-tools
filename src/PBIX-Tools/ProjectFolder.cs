using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace PbixTools
{
    public interface IProjectFolder : IDisposable
    {
        bool TryGetFile(string path, out Stream stream);
        void WriteFile(string path, Stream stream);
        void WriteText(string path, Action<TextWriter> writer);
    }

    public class ProjectFolder : IProjectFolder
    {
        // represents a PBIX (source controlled) folder, like: Model, Mashup, Report
        // each extract action maintains one instance
        // ctor with basePath
        // create list of all existing files
        // use to add/modify files: Write(path)
        // Dispose: Remove deleted
        // Log changes

        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFolder>();

        private readonly string _baseDir;
        private readonly HashSet<string> _filesWritten;

        public ProjectFolder(string baseDir)
        {
            _baseDir = baseDir;
            _filesWritten = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public bool TryGetFile(string path, out Stream stream)
        {
            var fullPath = Path.Combine(_baseDir, path);
            if (File.Exists(fullPath))
            {
                stream = File.OpenRead(fullPath);
                return true;
            }
            else
            {
                stream = null;
                return false;
            }
        }

        public void WriteFile(string path, Stream stream)
        {
            var fullPath = Path.Combine(_baseDir, path);
            _filesWritten.Add(fullPath); // keeps track of files added or updated
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using (var file = File.Create(fullPath))
            {
                stream.CopyTo(file);
            }
        }

        public void WriteText(string path, Action<TextWriter> writerCallback)
        {
            var fullPath = Path.Combine(_baseDir, path);
            _filesWritten.Add(fullPath); // keeps track of files added or updated
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            using (var writer = File.CreateText(fullPath))
            {
                writerCallback(writer);
            }
        }

        public void Dispose()
        {
            // TODO Remove base folder if empty
            if (_filesWritten.Count == 0)
            {

            }

            // Remove any existing files that have not been updated

            foreach (var path in Directory.GetFiles(_baseDir, "*.*", SearchOption.AllDirectories))
            {
                if (!_filesWritten.Contains(path)) File.Delete(path);
                // TODO Log the things
            }

            // Remove empty dirs:
            foreach (var dir in Directory.GetDirectories(_baseDir, "*", SearchOption.TopDirectoryOnly))
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault() == null)
                {
                    Directory.Delete(dir, recursive: true); // Could be root of a series of empty folders
                    // TODO log
                }
            }

        }

    }
}