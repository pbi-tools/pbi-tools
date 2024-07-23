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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Serilog;

namespace PbiTools.Utils
{
    public class EmbeddedTool
    {
        public const string WixExtract = "wix-extract";

        private static readonly ILogger Log = Serilog.Log.ForContext<EmbeddedTool>();

        public static EmbeddedTool Create(string label, string exeName = null)
        {
            var toolPath = new DirectoryInfo(Path.Combine(ApplicationFolders.AppDataFolder, label));
            toolPath.Create();

            var currentVersionPath = new DirectoryInfo(Path.Combine(toolPath.FullName, AssemblyVersionInformation.AssemblyInformationalVersion));
            var resourceName = $"PbiTools.EmbeddedTool.{label}.zip";

            if (!Resources.ContainsName(resourceName))
            {
                throw new ArgumentException($"Embedded resource '{resourceName}' does not exist.", nameof(resourceName));
            }

            if (!currentVersionPath.Exists)
            {
                foreach (var dir in toolPath.EnumerateDirectories().ToList())
                {
                    Log.Verbose("Deleting AppData tool path for previous version: {Path}", dir.FullName);
                    dir.Delete(recursive: true);
                }

                currentVersionPath.Create();

                using var zipStream = Resources.GetEmbeddedResourceStream(resourceName);
                using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                zipArchive.ExtractToDirectory(currentVersionPath.FullName);
            }

            if (exeName != null)
                return new EmbeddedTool(new FileInfo(Path.Combine(currentVersionPath.FullName, exeName)).FullName);
            else
                return new EmbeddedTool(currentVersionPath.EnumerateFiles("*.exe").FirstOrDefault().FullName);
        }


        private readonly string exePath;

        private EmbeddedTool(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                throw new ArgumentException($"'{nameof(exePath)}' cannot be null or empty.", nameof(exePath));
            if (!File.Exists(exePath))
                throw new FileNotFoundException("Cannot find specified tool exe.", exePath);

            this.exePath = exePath;
        }

        public int Run(string arguments, Action<ProcessStartInfo> configureStartInfo = null)
        {
            using var process = new Process();
            var startInfo = new ProcessStartInfo(exePath, arguments) {
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            configureStartInfo?.Invoke(startInfo);

            process.StartInfo = startInfo;
            process.Start();

            process.WaitForExit();

            return process.ExitCode;
        }

    }
}
