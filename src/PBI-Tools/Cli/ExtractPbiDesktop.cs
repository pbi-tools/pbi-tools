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
using PowerArgs;
using Spectre.Console;

namespace PbiTools.Cli
{
    using System;
    using Utils;

    public partial class CmdLineActions
    {

#if NETFRAMEWORK
        [ArgActionMethod, ArgShortcut("extract-pbidesktop"), ArgShortcut(ArgShortcutPolicy.ShortcutsOnly)]
        [ArgDescription("Extracts binaries from a PBIDesktopSetup.exe|.msi installer bundle (silent/x-copy install).")]
        public void ExtractPbiDesktop(
            [ArgRequired, ArgExistingFile, ArgDescription("The path to an existing PBIDesktopSetup.exe|PBIDesktopSetup.msi file.")]
                string installerPath,
            [ArgDescription("The destination folder. '-overwrite' must be specified if folder is not empty.")]
                string targetFolder,
            [ArgDescription("Overwrite any contents in the destination folder. Default: false"), ArgDefaultValue(false)]
                bool overwrite
        )
        {
            var targetDir = new DirectoryInfo(targetFolder);
            if (targetDir.Exists && targetDir.EnumerateFiles("*.*", SearchOption.AllDirectories).Any())
            {
                if (overwrite)
                    targetDir.Delete(recursive: true);
                else
                    throw new PbiToolsCliException(ExitCode.OverwriteNotAllowed, "The target directory is not empty, and '-overwrite' was not specified.");
            }

            targetDir.Create();
            var tmpDir = new DirectoryInfo(Path.Combine(targetDir.FullName, Guid.NewGuid().ToString("N")));

            var extension = Path.GetExtension(installerPath).ToLowerInvariant();

            if (extension != ".exe" && extension != ".msi")
                throw new PbiToolsCliException(ExitCode.UnsupportedFileType, "Only .exe and .msi files are supported.");

            var extractTool = EmbeddedTool.Create(EmbeddedTool.WixExtract);

            if (extension == ".exe")
            { 
                // Extract bundle
                AnsiConsole.Status()
                    .Start("Extracting EXE...", ctx => 
                    {
                        var exitCode = extractTool.Run($"extract-exe \"{installerPath}\" \"{tmpDir.FullName}\"");

                        if (exitCode != 0)
                            throw new PbiToolsCliException(ExitCode.UnspecifiedError, "EXE extraction failed.");
                    });

                AnsiConsole.MarkupLine("[green]EXE extracted.[/]");
            }

            var msiPath = extension == ".msi"
                ? new FileInfo(installerPath).FullName
                : Directory.EnumerateFiles(Path.Combine(tmpDir.FullName, "AttachedContainer"), "*.msi").FirstOrDefault();

            if (msiPath == null)
                throw new PbiToolsCliException(ExitCode.PathNotFound, "Cannot find .msi file to install.");

            // Install MSI
            AnsiConsole.Status()
                .Start("Expanding MSI (This might take a while)...", ctx => 
                {
                    var exitCode = extractTool.Run($"expand-msi \"{msiPath}\" \"{targetDir.FullName}\"");

                    if (exitCode != 0)
                        throw new PbiToolsCliException(ExitCode.UnspecifiedError, "MSI expansion failed.");
                });

            AnsiConsole.MarkupLine("[green]MSI expanded.[/]");

            // Clean up
            if (tmpDir.Exists)
                tmpDir.Delete(recursive: true);

            // Find PBIDesktop.exe
            var pbiDesktopPath = targetDir.EnumerateFiles("PBIDesktop.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (pbiDesktopPath != null)
                Log.Information("Custom install location: {PBIDesktopPath}", pbiDesktopPath.FullName);

        }
#endif

    }

}
