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
