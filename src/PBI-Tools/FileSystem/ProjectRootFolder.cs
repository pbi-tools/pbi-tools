// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace PbiTools.FileSystem
{
    /// <summary>
    /// Represents the root folder containing all files belonging to a <c>PbixProject</c>.
    /// </summary>
    public interface IProjectRootFolder : IDisposable
    {
        IProjectFolder GetFolder(string name);

        IProjectFile GetFile(string relativePath);

        bool Exists();

        /// <summary>
        /// Marks all modifications applied to the folder as successful, allowing for clean-up operations to run upon disposal.
        /// </summary>
        void Commit();
    }


    public class ProjectRootFolder : IProjectRootFolder
    {
        // central registry over all files written:
        private readonly HashSet<string> _filesWritten;
        private bool _committed;

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
            // Do not delete anything if an unhandled error has occurred
            if (!_committed) return;

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

        public void Commit()
        {
            _committed = true;
        }

        public bool Exists() => Directory.Exists(BasePath);
        
    }

}