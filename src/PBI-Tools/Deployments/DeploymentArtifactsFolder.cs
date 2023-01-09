// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Linq;
using Serilog;

namespace PbiTools.Deployments
{
    public class DeploymentArtifactsFolder
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<DeploymentArtifactsFolder>();

        public const string FolderName = ".pbixproj";

        private readonly DirectoryInfo _artifactsDirectory;

        public DeploymentArtifactsFolder(string basePath, string profileName)
        {
            _artifactsDirectory = new DirectoryInfo(Path.Combine(basePath, FolderName, profileName));
        }

        public bool Exists => _artifactsDirectory.Exists;

        public DirectoryInfo GetSubfolder(params string[] segments)
            => new(Path.Combine(new[] { _artifactsDirectory.FullName }.Concat(segments).ToArray()));

    }
}