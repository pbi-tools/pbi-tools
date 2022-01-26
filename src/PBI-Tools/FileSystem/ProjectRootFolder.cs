// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polly;
using Serilog;

namespace PbiTools.FileSystem
{
    /// <summary>
    /// Represents the root folder containing all files belonging to a <c>PbixProject</c>.
    /// </summary>
    public interface IProjectRootFolder : IDisposable
    {
        IProjectFolder GetFolder(string name = null);

        IProjectFile GetFile(string relativePath);

        bool Exists();

        /// <summary>
        /// Marks all modifications applied to the folder as successful, allowing for clean-up operations to run upon disposal.
        /// </summary>
        void Commit();
    }


    public class ProjectRootFolder : IProjectRootFolder
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ProjectRootFolder>();

        // central registry over all files written:
        private readonly HashSet<string> _filesWritten;
        private bool _committed;

        /// <summary>
        /// Handles <see cref="IOException"/>, waits, and retries twice.
        /// </summary>
        private static readonly Policy IOException_Retry_Policy = Policy
            .Handle<IOException>()
            .WaitAndRetry(new[] {
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(100),
            }, (ex, _) => 
                Log.Warning(ex, "Retrying after IOException.")
            );

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

        /// <summary>
        /// Gets a subfolder of the root folder, or the root folder itself if no argument is provided.
        /// </summary>
        /// <param name="name">The subfolder name, or <c>null</c>.</param>
        public IProjectFolder GetFolder(string name = null)
        {
            return String.IsNullOrEmpty(name)
                ? new ProjectFolder(this, BasePath)
                : new ProjectFolder(this, Path.Combine(BasePath, name));
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
                    IOException_Retry_Policy.Execute(() =>
                        Directory.Delete(BasePath, recursive: true)
                    );
                    Log.Information("No files written. Removed base folder: {Path}", BasePath);
                }

                return;
            }

            // Protect the PbixProj file in root
            _filesWritten.Add(ProjectSystem.PbixProject.GetDefaultPath(BasePath));

            // Remove any existing files that have not been updated
            foreach (var path in Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories))
            {
                if (!_filesWritten.Contains(path))
                {
                    IOException_Retry_Policy.Execute(() =>
                        File.Delete(path)
                    );
                    Log.Information("Removed file: {Path}", path);
                }
            }

            // Remove empty dirs:
            foreach (var dir in Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories).ToArray())
            {
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault() == null)
                {
                    IOException_Retry_Policy.Execute(() =>
                        Directory.Delete(dir, recursive: true) // Could be root of a series of empty folders
                    );
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