// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

namespace PbiTools.FileSystem
{

    /// <summary>
    /// Represents a single file inside the PBIXPROJ directory (e.g., version.txt, connections.json, etc.)
    /// </summary>
    public interface IProjectFile
    {
        /// <summary>
        /// Gets the full path of the file
        /// </summary>
        string Path { get; }

        bool Exists();

        /// <summary>
        /// Provides access to the <see cref="Stream"/> of a file if it exists.
        /// </summary>
        bool TryReadFile(out Stream stream);

        /// <summary>
        /// Provides a <see cref="Stream"/> to write the file to.
        /// </summary>
        void WriteFile(Action<Stream> onStreamAvailable);

        /// <summary>
        /// Provides a <see cref="TextWriter"/> to write the file to.
        /// </summary>
        void WriteText(Action<TextWriter> onTextWriterAvailable);
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

        public bool Exists() => File.Exists(this.Path);
        

        public bool TryReadFile(out Stream stream)
        {
            Log.Verbose("Attempting to read file: {Path}", Path);
            if (File.Exists(Path))
            {
                stream = File.OpenRead(Path);
                Log.Debug("File successfully opened: {Path}", Path);
                return true;
            }

            Log.Debug("File not found: {Path}", Path);
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

}