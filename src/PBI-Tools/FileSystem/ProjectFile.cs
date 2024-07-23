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
using Serilog;

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

            this.MarkWritten();
        }

        /// <summary>
        /// Explicitly marks the file as written.
        /// </summary>
        internal void MarkWritten()
        {
            _root.FileWritten(Path); // keeps track of files added or updated
        }
    }

}
