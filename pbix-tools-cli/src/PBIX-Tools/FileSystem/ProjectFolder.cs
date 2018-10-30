using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Formatting = Newtonsoft.Json.Formatting;

namespace PbixTools.FileSystem
{
    /// <summary>
    /// Represents a sub-folder inside the PBIXPROJ directory, containing the artifacts for one PBIX part (e.g., /Mashup, /Report, etc.)
    /// </summary>
    public interface IProjectFolder
    {
        /// <summary>
        /// Gets the full path of the folder.
        /// </summary>
        string BasePath { get; }

        IProjectFolder GetSubfolder(params string[] segments);

        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a file if it exists.
        /// </summary>
        bool TryGetFile(string path, out Stream stream);

        bool ContainsFile(string path);

        void DeleteFile(string path);
        
        /// <summary>
        /// Writes a binary file at the specified path by providing a <see cref="Stream"/> to write to.
        /// </summary>
        void WriteFile(string path, Action<Stream> onStreamAvailable);
        
        /// <summary>
        /// Writes a binary file at the specified path by providing a <see cref="TextWriter"/> to write to.
        /// </summary>
        void WriteText(string path, Action<TextWriter> onTextWriterAvailable);
    }


    /// <summary>
    /// Represents a single file inside the PBIXPROJ directory (e.g., version.txt, connections.json, etc.)
    /// </summary>
    public interface IProjectFile
    {
        /// <summary>
        /// Gets the full path of the file
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a file if it exists.
        /// </summary>
        bool TryGetFile(out Stream stream);

        /// <summary>
        /// Provides a <see cref="Stream"/> to write the file to.
        /// </summary>
        void WriteFile(Action<Stream> onStreamAvailable);

        /// <summary>
        /// Provides a <see cref="TextWriter"/> to write the file to.
        /// </summary>
        void WriteText(Action<TextWriter> onTextWriterAvailable);
    }

    public class ProjectFolder : IProjectFolder
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFolder>();

        private readonly ProjectRootFolder _root;

        public ProjectFolder(ProjectRootFolder root, string baseDir)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            var dir = new DirectoryInfo(baseDir);
            BasePath = dir.FullName;
        }

        public string BasePath { get; }

        private string GetFullPath(string path) => new FileInfo(Path.Combine(BasePath, SanitizePath(path))).FullName;

        public IProjectFolder GetSubfolder(params string[] segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            return segments.Length == 0 
                ? this 
                : new ProjectFolder(_root, Path.Combine(this.BasePath, Path.Combine(segments)));
        }

        public bool TryGetFile(string path, out Stream stream)
        {
            var fullPath = GetFullPath(path);
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

        public bool ContainsFile(string path)
        {
            var fullPath = GetFullPath(path);
            return File.Exists(fullPath);
        }

        public void DeleteFile(string path)
        {
            var fullPath = GetFullPath(path);
            File.Delete(fullPath);
            Log.Information("Removed file: {Path}", fullPath);
        }

        public void WriteFile(string path, Action<Stream> onStreamAvailable)
        {
            WriteFile(path, File.Create, onStreamAvailable);
        }

        public void WriteText(string path, Action<TextWriter> onTextWriterAvailable)
        {
            WriteFile(path, File.CreateText, onTextWriterAvailable);
        }

        private void WriteFile<T>(string path, Func<string, T> factory, Action<T> callback) where T : IDisposable
        {
            var fullPath = GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            Log.Verbose("Writing file: {Path}", fullPath);
            using (var writer = factory(fullPath))
            {
                callback(writer);
            }

            _root.FileWritten(fullPath); // keeps track of files added or updated
        }

        private static string SanitizePath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.StartsWith("/") || path.StartsWith("\\")) return path.Substring(1);
            return path;
        }
    }

    public class ProjectFile : IProjectFile
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectFile>();

        private readonly ProjectRootFolder _root;

        public ProjectFile(ProjectRootFolder root, string path)
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

        public void WriteFile(Action<Stream> onStreamAvailable)
        {
            WriteFile(File.Create, onStreamAvailable);
        }

        public void WriteText(Action<TextWriter> onTextWriterAvailable)
        {
            WriteFile(File.CreateText, onTextWriterAvailable);
        }

        private void WriteFile<T>(Func<string, T> factory, Action<T> callback) where T : IDisposable
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));

            Log.Verbose("Writing file: {Path}", Path);
            using (var writer = factory(Path))
            {
                callback(writer);
            }

            _root.FileWritten(Path); // keeps track of files added or updated
        }
    }

    public interface IProjectRootFolder : IDisposable
    {
        IProjectFolder GetFolder(string name);
        IProjectFile GetFile(string relativePath);
    }


    public class ProjectRootFolder : IProjectRootFolder
    {
        // central registry over all files written:
        private readonly HashSet<string> _filesWritten;

        public ProjectRootFolder(string basePath)
        {
            var dir = new DirectoryInfo(basePath);
            BasePath = dir.FullName;

            _filesWritten = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string BasePath { get; }


        internal void FileWritten(string fullPath)
        {
            _filesWritten.Add(fullPath); // keeps track of files added or updated
            Log.Verbose("File written: {Path}", fullPath);
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
                {
                    Directory.Delete(BasePath, recursive: true);
                    Log.Information("No files written. Removed base folder: {Path}", BasePath);
                }

                return;
            }

            // Remove any existing files that have not been updated
            foreach (var path in Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories))
            {
                if (!_filesWritten.Contains(path))
                {
                    File.Delete(path);
                    Log.Information("Removed file: {Path}", path);
                }
            }

            // Remove empty dirs:
            foreach (var dir in Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories).ToArray())
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault() == null)
                {
                    Directory.Delete(dir, recursive: true); // Could be root of a series of empty folders
                    Log.Information("Removed empty directory: {Path}", dir);
                }
            }

            // TODO Check if nested empty dirs need to be removed explicitly
            // ./ROOT
            // ./ROOT/dir1
            // ./ROOT/dir1/file.txt
            // ./ROOT/dir1/empty/ ***
        }

    }

    public static class ProjectFolderExtensions
    {

        public static void Write(this IProjectFolder folder, string text, string path)
        {
            folder.WriteText(path, writer =>
            {
                writer.Write(text);
            });
        }

        public static void Write(this IProjectFolder folder, JToken json, string path)
        {
            folder.WriteText(path, writer =>
            {
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    json.WriteTo(jsonWriter);
                }
            });
        }

        public static void Write(this IProjectFolder folder, XDocument xml, string path, bool omitXmlDeclaration = true)
        {
            folder.WriteText(path, writer =>
            {
                using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = omitXmlDeclaration }))
                {
                    xml.WriteTo(xmlWriter);
                }
            });
        }

        public static void Write(this IProjectFile file, JToken json)
        {
            file.WriteText(writer =>
            {
                using (var jsonWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented })
                {
                    json.WriteTo(jsonWriter);
                }
            });
        }

        public static void Write(this IProjectFile file, string text)
        {
            file.WriteText(writer =>
            {
                writer.Write(text);
            });
        }

        public static void Write(this IProjectFile file, XDocument xml, bool omitXmlDeclaration = true)
        {
            file.WriteText(writer =>
            {
                using (var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = omitXmlDeclaration }))
                {
                    xml.WriteTo(xmlWriter);
                }
            });
        }

    }
}