using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace PbixTools
{
    /// <summary>
    /// Represents a sub-folder inside the PBIXPROJ directory, containing the artifacts for one PBIX part (e.g., Mashup, Report, etc.)
    /// </summary>
    public interface IProjectFolder
    {
        /// <summary>
        /// Gets the path of the folder.
        /// </summary>
        string BasePath { get; }

        ///// <summary>
        ///// When set to <c>true</c>, deletes all files/folders that were not added or updated 
        ///// during the current lifetime of the <see cref="IProjectFolder"/> when <c>Dispose()</c> is called.
        ///// This is to ensure that unhandled exceptions do not cause the entire folder to be removed
        ///// when an action has only been performed partially.
        ///// </summary>
        //bool CommitDelete { get; set; }
        
        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a file if it exists.
        /// </summary>
        bool TryGetFile(string path, out Stream stream);
        
        /// <summary>
        /// Writes a binary file to the folder.
        /// </summary>
        void WriteFile(string path, Stream stream);
        
        /// <summary>
        /// Writes a text file to the folder.
        /// </summary>
        void WriteText(string path, Action<TextWriter> writer);
    }


    /// <summary>
    /// Represents a single file inside the PBIXPROJ directory (e.g., version.txt, connections.json, etc.)
    /// </summary>
    public interface IProjectFile
    {
        string Path { get; }

        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a file if it exists.
        /// </summary>
        bool TryGetFile(out Stream stream);

        /// <summary>
        /// Writes a binary file to the folder.
        /// </summary>
        void WriteFile(Stream stream);

        /// <summary>
        /// Writes a text file to the folder.
        /// </summary>
        void WriteText(Action<TextWriter> writer);
    }

    public class ProjectFolder : IProjectFolder
    {
        // represents a PBIX (source controlled) component folder, like: Model, Mashup, Report
        // each extract action maintains one instance
        // ctor with basePath
        // create list of all existing files
        // use to add/modify files: Write(path)
        // Dispose: Remove deleted
        // Log changes

        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFolder>();

        private readonly IProjectRootFolder _root;

        public ProjectFolder(IProjectRootFolder root, string baseDir)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            var dir = new DirectoryInfo(baseDir);
            BasePath = dir.FullName;
        }

        public string BasePath { get; }


        public bool TryGetFile(string path, out Stream stream)
        {
            var fullPath = Path.Combine(BasePath, path);
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
            var fullPath = Path.Combine(BasePath, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            Log.Verbose("Writing file: {Path}", fullPath);
            using (var file = File.Create(fullPath))
            {
                stream.CopyTo(file);
            }

            _root.FileWritten(fullPath); // keeps track of files added or updated
        }

        public void WriteText(string path, Action<TextWriter> writerCallback)
        {
            var fullPath = Path.Combine(BasePath, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            Log.Verbose("Writing file: {Path}", fullPath);
            using (var writer = File.CreateText(fullPath))
            {
                writerCallback(writer);
            }

            _root.FileWritten(fullPath); // keeps track of files added or updated
        }

    }

    public class ProjectFile : IProjectFile
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFile>();

        private readonly IProjectRootFolder _root;

        public ProjectFile(IProjectRootFolder root, string path)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            var file = new FileInfo(path); // will throw for invalid paths
            Path = file.FullName;
        }

        public string Path { get; }

        public bool TryGetFile(out Stream stream)
        {
            if (File.Exists(Path))
            {
                stream = File.OpenRead(Path);
                return true;
            }

            stream = null;
            return false;
        }

        public void WriteFile(Stream stream)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

            Log.Verbose("Writing file: {Path}", Path);
            using (var file = File.Create(Path))
            {
                stream.CopyTo(file);
            }

            _root.FileWritten(Path);
        }

        public void WriteText(Action<TextWriter> writerCallback)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

            Log.Verbose("Writing file: {Path}", Path);
            using (var writer = File.CreateText(Path))
            {
                writerCallback(writer);
            }

            _root.FileWritten(Path);
        }
    }

    public interface IProjectRootFolder
    {
        void FileWritten(string fullPath);
        IProjectFolder GetFolder(string name);
        IProjectFile GetFile(string relativePath);
    }

    public class ProjectRootFolder : IProjectRootFolder, IDisposable
    {
        // central registry over all files written
        private readonly HashSet<string> _filesWritten;

        public ProjectRootFolder(string basePath)
        {
            var dir = new DirectoryInfo(basePath);
            BasePath = dir.FullName;

            _filesWritten = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string BasePath { get; }

        // GetFolder("Mashup")
        // GetFile("version.txt")
        //
        // keep track of all folders/files written
        // on Dispose - fetch list of all actually written files
        // that way, ProjectFiles can be nested within ProjectFolders, and object creation is irrelevant



        public void FileWritten(string fullPath)
        {
            _filesWritten.Add(fullPath); // keeps track of files added or updated
        }

        public IProjectFolder GetFolder(string name)
        {
            return new ProjectFolder(this, Path.Combine(BasePath, name));
        }

        public IProjectFile GetFile(string relativePath)
        {
            return new ProjectFile(this, Path.Combine(BasePath, relativePath));
        }

        public void Dispose()
        {
            if (_filesWritten.Count == 0)
            {
                if (Directory.Exists(BasePath))
                    Directory.Delete(BasePath, recursive: true);
                return;
            }

            // Remove any existing files that have not been updated

            foreach (var path in Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories))
            {
                if (!_filesWritten.Contains(path)) File.Delete(path);
                // TODO Log the things
            }

            // Remove empty dirs:
            foreach (var dir in Directory.GetDirectories(BasePath, "*", SearchOption.TopDirectoryOnly))
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault() == null)
                {
                    Directory.Delete(dir, recursive: true); // Could be root of a series of empty folders
                    // TODO log
                }
            }

            // TODO Check if nested empty dirs need to be removed explicitly
        }

    }
}