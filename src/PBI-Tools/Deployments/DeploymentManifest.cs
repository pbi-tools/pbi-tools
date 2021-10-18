// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{
    using ProjectSystem;

    public class PbiDeploymentManifest
    {
        public PbiDeploymentMode Mode { get; set; }
        public PbiDeploymentSource Source { get; set; }
        public PbiDeploymentAuthentication Authentication { get; set; }
        public PbiDeploymentOptions Options { get; set; }
        public JToken Parameters { get; set; }
        public JToken Logging { get; set; }
        public IDictionary<string, PbiDeploymentEnvironment> Environments { get; set; }

        public static IDictionary<string, PbiDeploymentManifest> FromProject(PbixProject project) =>
            project.Deployments
                .Select(x => new { Name = x.Key, Manifest = x.Value.ToObject<PbiDeploymentManifest>() })
                .ToDictionary(x => x.Name, x => x.Manifest);

    }

    public enum PbiDeploymentMode
    {
        Report = 1,
        Model
    }

    public class PbiDeploymentSource
    {
        public PbiDeploymentSourceType Source { get; set; }
        public string Path { get; set; }
    }

    public enum PbiDeploymentSourceType
    {
        Folder = 1,
        File
    }

    public class PbiDeploymentAuthentication
    {
        public PbiDeploymentAuthenticationType Type { get; set; }
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    public enum PbiDeploymentAuthenticationType
    {
        ServicePrincipal = 1
    }

    public class PbiDeploymentOptions
    {
        public Microsoft.PowerBI.Api.Models.ImportConflictHandlerMode NameConflict { get; set; }
        public string TempDir { get; set; }
    }

    public class PbiDeploymentEnvironment
    {
        public bool Disabled { get; set; }
        public Guid WorkspaceId { get; set; } // TODO Support Workspace Name
        public string WorkspaceName { get; set; }
    }
}