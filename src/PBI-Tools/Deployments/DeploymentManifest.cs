// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{
    using ProjectSystem;

    public class PbiDeploymentManifest
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("mode")]
        public PbiDeploymentMode Mode { get; set; }

        [JsonProperty("source")]
        public PbiDeploymentSource Source { get; set; }

        [JsonProperty("authentication")]
        public PbiDeploymentAuthentication Authentication { get; set; }

        [JsonProperty("options")]
        public PbiDeploymentOptions Options { get; set; }

        [JsonProperty("parameters")]
        public IDictionary<string, string> Parameters { get; set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        [JsonProperty("logging")]
        public JToken Logging { get; set; }

        [JsonProperty("environments")]
        public IDictionary<string, PbiDeploymentEnvironment> Environments { get; set; }

        public static IDictionary<string, PbiDeploymentManifest> FromProject(PbixProject project) =>
            (project.Deployments ?? new Dictionary<string, JToken>())
                .Select(x => new { Name = x.Key, Manifest = x.Value.ToObject<PbiDeploymentManifest>().ExpandEnvironments() })
                .ToDictionary(x => x.Name, x => x.Manifest);

        public JObject AsJson() =>
            JObject.FromObject(this, JsonSerializer.Create(PbixProject.DefaultJsonSerializerSettings));

    }

    public enum PbiDeploymentMode
    {
        Report = 1,
        // Dataset
        // ProvisionWorkspace
        // AAS
    }

    public class PbiDeploymentSource
    {
        [JsonProperty("type")]
        public PbiDeploymentSourceType Type { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }
    }

    public enum PbiDeploymentSourceType
    {
        Folder = 1,
        File = 2
    }

    public class PbiDeploymentAuthentication
    {
        [JsonProperty("type")]
        public PbiDeploymentAuthenticationType Type { get; set; }

        /// <summary>
        /// The Azure AD authority URI. This can be used instead of <see cref="TenantId"/> when full control is required over
        /// the Azure Cloud, the audience, and the tenant.
        /// </summary>
        [JsonProperty("authority")]
        public string Authority { get; set; }

        [JsonProperty("validateAuthority")]
        public bool ValidateAuthority { get; set; } = true;

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("clientSecret")]
        public string ClientSecret { get; set; }
    }

    public enum PbiDeploymentAuthenticationType
    {
        ServicePrincipal = 1
    }

    public class PbiDeploymentOptions
    {
        /// <summary>
        /// Allows setting an alternative Power BI base uri, supporting different Azure Clouds (default: 'https://api.powerbi.com').
        /// </summary>
        [JsonProperty("pbiBaseUri")]
        public Uri PbiBaseUri { get; set; }
        
        /// <summary>
        /// Allows setting an alternative temp folder into which .pbix files are compiled. Supports variable expansion (default: '%TEMP%').
        /// </summary>
        [JsonProperty("tempDir")]
        public string TempDir { get; set; }

        /// <summary>
        /// If true, loads and displays all report metadata post deployment.
        /// </summary>
        /// <remarks>
        /// Reserved for future use.
        /// </remarks>
        [JsonProperty("loadFullReportInfo")]
        public bool LoadFullReportInfo { get; set; }

        [JsonProperty("import")]
        public ImportOptions Import { get; set; }

        public class ImportOptions
        {
            /// <summary>
            /// Determines the behavior in case a report/dataset with the same name already exists.
            /// </summary>
            [JsonProperty("nameConflict")]
            public Microsoft.PowerBI.Api.Models.ImportConflictHandlerMode NameConflict { get; set; }

            /// <summary>
            /// Determines whether to skip report import, and import the dataset only.
            /// </summary>
            [JsonProperty("skipReport")]
            public bool? SkipReport { get; set; }

            /// <summary>
            /// Determines whether to override existing label on report during republish of PBIX file, service default value is true.
            /// </summary>
            [JsonProperty("overrideReportLabel")]
            public bool? OverrideReportLabel { get; set; }

            /// <summary>
            /// Determines whether to override existing label on model during republish of PBIX file, service default value is true.
            /// </summary>
            [JsonProperty("overrideModelLabel")]
            public bool? OverrideModelLabel { get; set; }

        }
    }

    public class PbiDeploymentEnvironment
    {
        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty("disabled")]
        public bool Disabled { get; set; }

        /// <summary>
        /// A workspace name or guid. Supports parameter expansion.
        /// </summary>
        [JsonProperty("workspace")]
        public string Workspace { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
        
        
        // Workspace Members? [ User/Group/App, AccessLevel]
        // Capacity
    }
}