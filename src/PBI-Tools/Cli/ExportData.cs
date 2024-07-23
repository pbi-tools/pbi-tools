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
using PowerArgs;

namespace PbiTools.Cli
{
    using PowerBI;
    using Tabular;

    public partial class CmdLineActions
    {
        
        [ArgActionMethod, ArgShortcut("export-data"), ArgAltShortcut("extract-data")]
#if NETFRAMEWORK
        [ArgDescription("Exports data from all tables in a tabular model, either from within a PBIX file, or from a live session.")]
        [ArgExample(
            "pbi-tools export-data -port 12345", 
            "Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory.",
            Title = "Export data from local workspace instance")]
        [ArgExample(
            @"pbi-tools export-data -pbixPath '.\data\Samples\Adventure Works DW 2020.pbix'", 
            "Extracts all records from each table from the model embedded in the specified PBIX file. Each table is extracted into a UTF-8 CSV file with the same name into the current working directory.",
            Title = "Export data from offline PBIX file")]
#else
        [ArgDescription("Exports data from all tables in a live Power BI Desktop session.")]
        [ArgExample(
            "pbi-tools.core export-data -port 12345", 
            "Extracts all records from each table from a local Power BI Desktop or SSAS Tabular instance running on port 12345 (get actual port via 'info' command). Each table is extracted into a UTF-8 CSV file with the same name into the current working directory.",
            Title = "Export data from local workspace instance")]
#endif
        public void ExportData(
            [ArgCantBeCombinedWith("pbixPath"), ArgDescription("The port number of a local Tabular Server instance.")]
                int port,
#if NETFRAMEWORK
            [ArgRequired(IfNot = "port"), ArgExistingFile, ArgDescription("The PBIX file to extract data from.")]
                string pbixPath,
#endif
            [ArgDescription("The output directory. Uses PBIX file directory if not provided, or the current working directory when connecting to Tabular Server instance.")]
                string outPath,
            [ArgDescription("The format to use for DateTime values. Must be a valid .Net format string, see: https://docs.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings."), ArgDefaultValue("s")]
                string dateTimeFormat
        )
        {
#if NET
            var pbixPath = default(string);
#endif
            if (outPath == null && pbixPath != null)
                outPath = Path.GetDirectoryName(pbixPath);
            else if (outPath == null)
                outPath = Environment.CurrentDirectory;

            Log.Verbose("Port: {Port}, Path: {PbixPath}, OutPath: {OutPath}", port, pbixPath, outPath);

#if NETFRAMEWORK
            if (pbixPath != null)
            {
                using (var file = File.OpenRead(pbixPath))
                using (var package = Microsoft.PowerBI.Packaging.PowerBIPackager.Open(file, skipValidation: true))
                using (var msmdsrv = new AnalysisServicesServer(new ASInstanceConfig
                {
                    DeploymentMode = DeploymentMode.SharePoint,
                    DisklessModeRequested = true,
                    EnableDisklessTMImageSave = true,
                }, _dependenciesResolver))
                {
                    msmdsrv.HideWindow = true;

                    msmdsrv.Start();
                    msmdsrv.LoadPbixModel(package.DataModel.GetStream(), "Model", "Model");

                    using (var reader = new TabularDataReader(msmdsrv.OleDbConnectionString))
                    {
                        reader.ExtractTableData(outPath, dateTimeFormat);
                    }
                }
            }
            else
#endif
            {
                using (var reader = new TabularDataReader($"Provider=MSOLAP;Data Source=.:{port};"))
                {
                    reader.ExtractTableData(outPath, dateTimeFormat);
                }
            }
        }

    }    

}
