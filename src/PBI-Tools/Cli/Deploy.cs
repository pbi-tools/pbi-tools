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
using PowerArgs;

namespace PbiTools.Cli
{
    using Deployments;
    using FileSystem;
    using ProjectSystem;

    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("deploy")]
        [ArgDescription("Deploys artifacts (reports, datasets) to Power BI Service using a deployment manifest.")]
        public void Deploy(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder containing the deployment manifest.")]
                string folder,
            [ArgRequired, ArgDescription("Name of a profile in the deployment manifest.")]
                string label,
            [ArgDescription("The target deployment environment.")]
            [ArgDefaultValue("Development")]
                string environment,
            [ArgDescription("When specified, resolves all deployment source paths relative to this path (and basePath relative to the current working directory), instead of the location of the PbixProj manifest.")]
                string basePath,
            [ArgDescription("When enabled, simulates the deployment actions and provides diagnostic output. Useful to test source path expressions and parameters. Authentication credentials are validated.")]
            [ArgDefaultValue(false)]
                bool whatIf
        )
        {
            using (var rootFolder = new ProjectRootFolder(folder))
            {
                var proj = PbixProject.FromFolder(rootFolder);
                var deploymentManager = new DeploymentManager(proj) { WhatIf = whatIf };
                
                if (!string.IsNullOrEmpty(basePath))
                    deploymentManager.BasePath = new DirectoryInfo(basePath).FullName;

                deploymentManager.DeployAsync(label, environment).Wait();
            }
        }
        
    }

}
