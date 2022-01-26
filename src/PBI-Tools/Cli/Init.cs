// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using PowerArgs;
using static Microsoft.PowerBI.Api.Models.ImportConflictHandlerMode;

namespace PbiTools.Cli
{
    using Deployments;
    using ProjectSystem;

    public partial class CmdLineActions
    {

        [ArgActionMethod, ArgShortcut("init")]
        [ArgDescription("Initializes a PbixProj workpace.")]
        public void Init(
            [ArgRequired, ArgDescription("The initialize action to perform.")]
                InitAction action,
            [ArgExistingDirectory, ArgDescription("The PbixProj folder to operation in. Uses current working directory if not specified.")]
                string folder
        )
        {
            var projFolder = new DirectoryInfo(folder ?? ".").FullName;
            var pbixProj = PbixProject.FromFolder(projFolder);

            // TODO Iterate through possible flags, Handle "All" special case

            if (action.HasFlag(InitAction.Deployments))
            { 
                if (pbixProj.Deployments != null && pbixProj.Deployments.Count > 0)
                    Log.Warning("The PbixProj file at {Path} already contains a deployment manifest. Exiting.", pbixProj.OriginalPath);
                else
                {
                    pbixProj.Deployments = new Dictionary<string, JToken> {
                        { "Report from File with folder wildcard", new PbiDeploymentManifest {
                            Description = "This profile deploys a number of reports from .pbix files. One of the path segments, {{WORKSPACE}}, is used as a deployment parameter for a destination workspace name.",
                            Mode = PbiDeploymentMode.Report,
                            Source = new() {
                                Type = PbiDeploymentSourceType.File,
                                Path = "./Reports/{{WORKSPACE}}/*.pbix"
                            },
                            Parameters = new Dictionary<string, string> {
                                { "WORKSPACE_PREFIX", "Team-A" }
                            },
                            Options = new() {
                                PbiBaseUri = DeploymentManager.DefaultPowerBIApiBaseUri,
                                TempDir = @"D:\TEMP",
                                Import = new() {
                                    NameConflict = CreateOrOverwrite,
                                    SkipReport = false,
                                    OverrideModelLabel = true,
                                    OverrideReportLabel = true
                                }
                            },
                            Authentication = new() {
                                Type = PbiDeploymentAuthenticationType.ServicePrincipal,
                                Authority = "https://login.microsoftonline.com/your-tenant-name-or-id",
                                ValidateAuthority = true,
                                TenantId = "Service Principal Tenant ID/Name. Use as shortcut instead of providing full AuthorityUrl.",
                                ClientId = "Service Principal ClientId",
                                ClientSecret = "%ENV_VARIABLE_NAME%"
                            },
                            Environments = new Dictionary<string, PbiDeploymentEnvironment> {
                                { "Development", new() {
                                    Workspace = "{{WORKSPACE_PREFIX}} - {{WORKSPACE}}",
                                    Disabled = false
                                }},
                                { "UAT", new() {
                                    Workspace = "Workspace-Name",
                                    Disabled = false
                                }},
                                { "Production", new() {
                                    Workspace = "00000000-0000-0000-0000-000000000000",
                                    Disabled = true
                                }}
                            }
                        }.AsJson() },
                        { "Report from PbixProj folders wildcard", new PbiDeploymentManifest {
                            Description = "This profile deploys a number of reports from PbixProj folders. The .pbix files are compiled as part of the deployment process. Folders without a .pbixproj.json file are ignored.",
                            Mode = PbiDeploymentMode.Report,
                            Source = new() {
                                Type = PbiDeploymentSourceType.Folder,
                                Path = "./Reports/{{WORKSPACE}}/*"
                            },
                            Parameters = new Dictionary<string, string> {
                            },
                            Options = new() {
                                PbiBaseUri = DeploymentManager.DefaultPowerBIApiBaseUri,
                                TempDir = "%PATH_FROM_ENV%",
                                Import = new() {
                                    NameConflict = Ignore,
                                }
                            },
                            Authentication = new() {
                                Type = PbiDeploymentAuthenticationType.ServicePrincipal,
                                TenantId = "Service Principal Tenant ID/Name. The default AAD authority is used.",
                                ClientId = "Service Principal ClientId",
                                ClientSecret = "%ENV_VARIABLE_NAME%"
                            },
                            Environments = new Dictionary<string, PbiDeploymentEnvironment> {
                                { "Development", new() {
                                    Workspace = "{{WORKSPACE_PREFIX}} - {{WORKSPACE}}",
                                    Disabled = false
                                }},
                                { "UAT", new() {
                                    Workspace = "Workspace-Name",
                                    DisplayName = "{{PBIXPROJ_NAME}} (UAT).pbix",
                                    Disabled = false
                                }},
                                { "Production", new() {
                                    Workspace = Guid.NewGuid().ToString(),
                                    Disabled = true
                                }}
                            }
                        }.AsJson() }
                    };

                    pbixProj.Save(setModified: true);
                    Log.Information("Exported sample deployment manifest to : {Path}", pbixProj.OriginalPath);
                }
            }
            else
            { 
                throw new NotImplementedException();
            }
        }

    }

    [Flags]
    public enum InitAction
    {
        // [ArgDescription("***********")]
        // PbixProj = 1,
        [ArgDescription("Generates a sample deployment manifest in the specified location if none exists.")]
        Deployments = 2,
        // GitIgnore = 3,
        // Full = 10,
    }
}