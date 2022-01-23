// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
        [ArgDescription("Deploys artifacts to Power BI Service or Azure Analysis Services using a deployment manifest. Currently, only 'Report' deployment, from .pbix files or PbixProj folders, is supported.")]
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