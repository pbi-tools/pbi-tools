// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerBI.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{
    using ProjectSystem;

    /// <summary>
    /// Represents a single deployment profile, i.e. a source definition, one or more target environments,
    /// as well as deployment options, deployment parameters, and authentication settings.
    /// </summary>
    public class PbiDeploymentManifest
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("mode")]
        public PbiDeploymentMode Mode { get; set; }

        [JsonProperty("source")]
        public PbiDeploymentSource Source { get; set; }

        [JsonProperty("authentication")]
        public PbiDeploymentAuthentication Authentication { get; set; } = new();

        [JsonProperty("options")]
        public PbiDeploymentOptions Options { get; set; } = new();

        [JsonProperty("parameters")]
        public DeploymentParameters Parameters { get; set; }

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

        public string ResolveTempDir() =>
            String.IsNullOrEmpty(Options?.TempDir)
            ? System.IO.Path.GetTempPath()
            : Environment.ExpandEnvironmentVariables(Options.TempDir);
    }

    public enum PbiDeploymentMode
    {
        Report = 1,
        Dataset = 2,
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
        /// Allows setting an alternative temp folder into which .pbix files are compiled. Supports variable expansion (default: <see cref="System.IO.Path.GetTempPath"/>).
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
        public ImportOptions Import { get; set; } = new();

        [JsonProperty("refresh")]
        public RefreshOptions Refresh { get; set; } = new();

        [JsonProperty("dataset")]
        public DatasetOptions Dataset { get; set; } = new();

        [JsonProperty("report")]
        public ReportOptions Report { get; set; } = new();

        public class ImportOptions
        {
            /// <summary>
            /// Determines the behavior in case a report/dataset with the same name already exists.
            /// </summary>
            [JsonProperty("nameConflict")]
            public ImportConflictHandlerMode NameConflict { get; set; } = ImportConflictHandlerMode.CreateOrOverwrite;

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

        public class RefreshOptions
        {
            /// <summary>
            /// Skip refresh when the deployment created a new dataset (instead of updating an existing one).
            /// Default is <c>true</c>.
            /// </summary>
            [JsonProperty("skipNewDataset")]
            public bool SkipNewDataset { get; set; } = true;

            [JsonProperty("method")]
            public RefreshMethod Method { get; set; } = RefreshMethod.API;

            [JsonProperty("type")]
            public DatasetRefreshType Type { get; set; } = DatasetRefreshType.Automatic;

            // *** https://docs.microsoft.com/rest/api/power-bi/datasets/refresh-dataset-in-group
            // applyRefreshPolicy
            // commitMode
            // effectiveDate
            // maxParallelism
            // objects
            // retryCount
            
            [JsonProperty("tracing")]
            public TraceOptions Tracing { get; set; } = new();

            public enum RefreshMethod
            { 
                API = 1,
                XMLA = 2
            }

            public class TraceOptions
            {
                [JsonProperty("enabled")]
                public bool Enabled { get; set; }

                [JsonProperty("logEvents")]
                public TraceLogEvents LogEvents { get; set; }

                [JsonProperty("summary")]
                public TraceSummary Summary { get; set; }


                public class TraceLogEvents
                {
                    [JsonProperty("filter")]
                    public string[] Filter { get; set; }
                }

                public class TraceSummary
                {
                    [JsonProperty("events")]
                    public string[] Events { get; set; }

                    [JsonProperty("objectTypes")]
                    public string[] ObjectTypes { get; set; }

                    [JsonProperty("outPath")]
                    public string OutPath { get; set; }

                    [JsonProperty("console")]
                    public bool Console { get; set; }
                }
            }
        }

        public class DatasetOptions
        {
            [JsonProperty("replaceParameters")]
            public bool ReplaceParameters { get; set; }

            [JsonProperty("deployEmbeddedReport")]
            public bool DeployEmbeddedReport { get; set; }
        }

        public class ReportOptions
        { 
            [JsonProperty("customConnectionsTemplate")]
            public string CustomConnectionsTemplate { get; set; }
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

        /// <summary>
        /// For PBIX import deployments, corresponds to the <c>datasetDisplayName</c> API parameter. Must include a file extension.
        /// For dataset deployments, sets the dataset name shown in Power BI Service.
        /// Defaults to the source file/folder name if not specified in the manifest.
        /// Supports parameter expansion.
        /// </summary>
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        /// <summary>
        /// The <c>Data Source</c> parameter in a XMLA connection string. Can be omitted if workspace name is provided and the default
        /// Power BI connection string applies.
        /// </summary>
        [JsonProperty("xmlaDataSource")]
        public string XmlaDataSource { get; set; }

        /// <summary>
        /// If <c>true</c>, refreshes the dataset after metadata deployment. Default is <c>false</c>.
        /// </summary>
        [JsonProperty("refresh")]
        public bool Refresh { get; set; }

        // (Dataset) refresh settings
        // Workspace Members? [ User/Group/App, AccessLevel]
        // Capacity

        /// <summary>
        /// Allows customizing environment settings for embedded reports published as part of a dataset deployment.
        /// </summary>
        [JsonProperty("report")]
        public ReportEnvironment Report { get; set; }

        public class ReportEnvironment
        { 
            /// <summary>
            /// Used to disable report deployment for specific environments only.
            /// </summary>
            [JsonProperty("skip")]
            public bool Skip { get; set; }

            /// <summary>
            /// The optional workspace Name or Guid to deploy the dataset report to.
            /// If omitted, the dataset workspace is used.
            /// Supports parameter expansion.
            /// </summary>
            [JsonProperty("workspace")]
            public string Workspace { get; set; }

            /// <summary>
            /// The optional DisplayName to use for the report deployment. Must include a file extension.
            /// If omitted, the dataset display name is used.
            /// Supports parameter expansion.
            /// </summary>
            [JsonProperty("displayName")]
            public string DisplayName { get; set; }
        }
    }
}