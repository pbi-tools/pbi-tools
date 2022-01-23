// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using PowerArgs;

namespace PbiTools.Cli
{
    using PowerBI;

    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("compile"), ArgShortcut("compile-pbix")]
        [ArgDescription("Generates a PBIX/PBIT file from sources in the specified PbixProj folder. Currently, the PBIX output is supported only for report-only projects (\"thin\" reports), and PBIT for projects containing a data model.")]
        public void CompilePbix(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder to generate the PBIX from.")]
                string folder,
            [ArgDescription("The path for the output file. If not provided, creates the file in the current working directory, using the foldername. A directory or file name can be provided. The full output path is created if it does not exist.")]
                string outPath,
            [ArgDescription("The target file format.")]
            [ArgDefaultValue(PbiFileFormat.PBIX)]
                PbiFileFormat format,
            [ArgDescription("Overwrite the destination file if it already exists, fail otherwise.")]
            [ArgDefaultValue(false)]
                bool overwrite
        )
        {
            // SUCCESS
            // [x] PBIX from Report-Only
            // [x] PBIT from PBIT sources (incl Mashup)
            // [x] PBIT from PBIX sources (no mashup)
            //
            // TODO
            // [ ] PBIX from source with model
            // [ ] Merge into PBIX

            FileInfo outputFile;

            var filenameFromPbixProj = $"{new DirectoryInfo(folder).Name}.{(format == PbiFileFormat.PBIT ? "pbit" : "pbix")}";

            if (String.IsNullOrEmpty(outPath))
                outputFile = new FileInfo(filenameFromPbixProj);
            else 
            {
                var pathAsDirectory = new DirectoryInfo(outPath);
                var pathAsFile = new FileInfo(outPath);

                if (pathAsFile.Exists)
                    /* Existing File */
                    outputFile = pathAsFile;

                else if (pathAsDirectory.Exists)
                    /* Existing Directory: Use generated filename */
                    outputFile = new FileInfo(Path.Combine(pathAsDirectory.FullName, filenameFromPbixProj));
                
                else if (!String.IsNullOrEmpty(pathAsFile.Extension))
                    /* Path with extension provided: Use as file path */
                    outputFile = pathAsFile;
                
                else
                    /* Path w/o extension provided: Use as directory, generate filename */
                    outputFile = new FileInfo(Path.Combine(pathAsDirectory.FullName, filenameFromPbixProj));
            }

            if (outputFile.Exists && !overwrite)
                throw new PbiToolsCliException(ExitCode.FileExists, $"Destination file '{outputFile.FullName}' exists and the '-overwrite' option was not specified.");

            using (var proj = PbiTools.Model.PbixModel.FromFolder(folder))
            {
                outputFile.Directory.Create();

                proj.ToFile(outputFile.FullName, format, _dependenciesResolver);
            }

            Log.Information("{Format} file written to: {Path}", format, outputFile.FullName);
        }

    }

}