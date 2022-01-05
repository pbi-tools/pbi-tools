// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using PowerArgs;

namespace PbiTools.Cli
{
    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("deploy")]
        [ArgDescription("Deploys artifacts to Power BI Service or Azure Analysis Services.")]
        public void Deploy(
            [ArgRequired, ArgExistingDirectory, ArgDescription("The PbixProj folder containing the deployment manifest.")]
                string folder,
            [ArgDescription("Name of a section in the deployment manifest.")]
                string label,
            [ArgDescription("The target deployment environment."), ArgDefaultValue("Development")]
                string environment
        )
        {
            using (var rootFolder = new FileSystem.ProjectRootFolder(folder))
            {
                var proj = ProjectSystem.PbixProject.FromFolder(rootFolder);
                var deploymentManager = new Deployments.DeploymentManager();

                // TODO Support `-whatIf` option?
                deploymentManager.DeployAsync(proj, environment, label).Wait();
            }
        }
        
    }

}