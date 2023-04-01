// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
