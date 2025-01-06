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
using System.Collections.Generic;
using System.ComponentModel;
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

        [JsonProperty("credentials")]
        public PbiDeploymentCredential[] Credentials { get; set; }

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

    public class PbiDeploymentAuthentication : PbiDeploymentOAuthCredentials
    {
        [JsonProperty("type")]
        public PbiDeploymentAuthenticationType Type { get; set; }

    }

    public class PbiDeploymentOAuthCredentials
    {
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

        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }
    }

    public enum PbiDeploymentAuthenticationType
    {
        ServicePrincipal = 1
    }

    public class PbiDeploymentCredential : PbiDeploymentOAuthCredentials
    {

        [JsonProperty("match")]
        public CredentialMatch Match { get; set; } = new();

        public class CredentialMatch
        { 
            [JsonProperty("datasourceType")]
            public string DatasourceType { get; set; }

            [JsonProperty("connectionDetails")]
            public DatasourceConnectionDetails ConnectionDetails { get; set; } = new();
        }

        [JsonProperty("updateMode")]
        public CredentialUpdateMode UpdateMode { get; set; }

        public enum CredentialUpdateMode
        { 
            /// <summary>
            /// Only updates credentials if no credentials are specified for this datasource.
            /// </summary>
            NotSpecified = 0,
            /// <summary>
            /// Always updates credentials.
            /// </summary>
            Always = 1,
            /// <summary>
            /// Never updates credentials.
            /// </summary>
            Never = 2,
            /// <summary>
            /// Updates credential before each refresh.
            /// </summary>
            BeforeRefresh = 3
        }

        /// <summary>
        /// Allowed values are: Basic, Anonymous, OAuth2.
        /// </summary>
        /// <value></value>
        [JsonProperty("type")]
        public CredentialType Type { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("useDeploymentToken")]
        public bool UseDeploymentToken { get; set; }

        [JsonProperty("details")]
        public PbiCredentialDetails Details { get; set; } = new();

        public class PbiCredentialDetails
        {
            /// <summary>
            /// Gets or sets whether to encrypt the data source connection. The API
            /// call will fail if you select encryption and Power BI is unable to
            /// establish an encrypted connection with the data source. Possible
            /// values include: 'Encrypted', 'NotEncrypted'.
            /// Default value is <see cref="EncryptedConnection.Encrypted"/>.
            /// </summary>
            [JsonProperty(PropertyName = "encryptedConnection")]
            public EncryptedConnection EncryptedConnection { get; set; } = EncryptedConnection.Encrypted;

            /// <summary>
            /// Gets or sets the encryption algorithm. For a cloud data source,
            /// specify `None`. For an on-premises data source, specify `RSA-OAEP`
            /// and use the gateway public key to encrypt the credentials. Possible
            /// values include: 'None', 'RSA-OAEP'.
            /// Default value is <see cref="EncryptionAlgorithm.None"/>.
            /// </summary>
            [JsonProperty(PropertyName = "encryptionAlgorithm")]
            public EncryptionAlgorithm EncryptionAlgorithm { get; set; } = EncryptionAlgorithm.None;

            /// <summary>
            /// Gets or sets the privacy level, which is relevant when combining
            /// data from multiple sources. Possible values include: 'None',
            /// 'Public', 'Organizational', 'Private'.
            /// Default value is <see cref="PrivacyLevel.None"/>.
            /// </summary>
            [JsonProperty(PropertyName = "privacyLevel")]
            public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.None;

            /// <summary>
            /// Gets or sets whether the Azure AD identity (OAuth 2.0 credentials)
            /// of the API caller (which must be the data source owner) will be
            /// used to configure data source credentials (the owner OAuth access
            /// token). Typically, you would either use this flag or
            /// `useEndUserOAuth2Credentials`.
            /// </summary>
            [JsonProperty(PropertyName = "useCallerAADIdentity")]
            public bool? UseCallerAADIdentity { get; set; }

            /// <summary>
            /// Gets or sets whether the end-user Azure AD identity (OAuth 2.0
            /// credentials) is used when connecting to the data source in
            /// DirectQuery mode. Use with data sources that support [single
            /// sign-on
            /// (SSO)](/power-bi/connect-data/power-bi-data-sources#single-sign-on-sso-for-directquery-sources).
            /// Typically, you would either use this flag or
            /// `useCallerAADIdentity`.
            /// </summary>
            [JsonProperty(PropertyName = "useEndUserOAuth2Credentials")]
            public bool? UseEndUserOAuth2Credentials { get; set; }
        }
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

        [JsonProperty("sqlScripts")]
        public SqlScriptsOptions SqlScripts { get; set; } = new();

        [JsonProperty("console")]
        public ConsoleOptions Console { get; set; } = new();


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
            /// Globally enables or disables dataset refresh.
            /// If enabled, refresh can be skipped in specific environments. If disabled, environment refresh settings are ignored.
            /// </summary>
            [JsonProperty("enabled")]
            [DefaultValue(false)]
            public bool Enabled { get; set; }

            /// <summary>
            /// Skip refresh when the deployment created a new dataset (instead of updating an existing one).
            /// Default is <c>true</c>.
            /// </summary>
            [JsonProperty("skipNewDataset")]
            [DefaultValue(false)]
            public bool SkipNewDataset { get; set; }

            [JsonProperty("method")]
            [DefaultValue(RefreshMethod.XMLA)]
            public RefreshMethod Method { get; set; } = RefreshMethod.XMLA;

            [JsonProperty("type")]
            [DefaultValue(nameof(DatasetRefreshType.Automatic))]
            public DatasetRefreshType Type { get; set; } = DatasetRefreshType.Automatic;

            /// <summary>
            /// The refresh type to apply to any incremental refresh policy partitions in a 'NoData' state.
            /// </summary>
            [JsonProperty("unprocessedPolicyRangePartitions")]
            public DatasetRefreshType? UnprocessedPolicyRangePartitionsRefreshType { get; set; }

            /// <summary>
            /// If a table has an incremental refresh policy defined, ignoreRefreshPolicy will determine
            /// if the policy is applied or not. If the policy is not applied, a process full operation
            /// will leave partition definitions unchanged and all partitions in the table will be fully refreshed.
            /// Default value is false.
            /// </summary>
            [JsonProperty("ignoreRefreshPolicy")]
            [DefaultValue(false)]
            public bool IgnoreRefreshPolicy { get; set; }

            /// <summary>
            /// If an incremental refresh policy is being applied, it needs to know the current date to determine
            /// rolling window ranges for the historical range and the incremental range. The effectiveDate parameter
            /// allows you to override the current date. This is useful for testing, demos, and business scenarios
            /// where data is incrementally refreshed up to a date in the past or the future (for example, budgets
            /// in the future). The default value is the current date.
            /// </summary>
            [JsonProperty("effectiveDate")]
            public DateTime? EffectiveDate { get; set; }

            /// <summary>
            /// If set, does not explicitly request a refresh on a refresh policy partition, unless the partition
            /// is explicitly mentioned in the objects array.
            /// The default value is false.
            /// </summary>
            [JsonProperty("skipRefreshPolicyPartitions")]
            [DefaultValue(false)]
            public bool SkipRefreshPolicyPartitions { get; set; }

            [JsonProperty("objects")]
            public RefreshObjects Objects { get; set; }

            // *** https://docs.microsoft.com/rest/api/power-bi/datasets/refresh-dataset-in-group
            // commitMode
            // maxParallelism
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
                [DefaultValue(false)]
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
            /// <summary>
            /// If <c>true</c>, replaces the values of dataset shared expressions with respective values
            /// from manifest/environment parameters.
            /// Default is <c>false</c>.
            /// </summary>
            [JsonProperty("replaceParameters")]
            public bool ReplaceParameters { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("deployEmbeddedReport")]
            public bool DeployEmbeddedReport { get; set; }

            /// <summary>
            /// Do not overwrite partitions that have Incremental Refresh Policies defined.
            /// Default value is true.
            /// </summary>
            [JsonProperty("keepRefreshPolicyPartitions")]
            [DefaultValue(true)]
            public bool KeepRefreshPolicyPartitions { get; set; } = true;

            /// <summary>
            /// Applies refresh policies after metadata deployment.
            /// Default value is false.
            /// </summary>
            [JsonProperty("applyRefreshPolicies")]
            [DefaultValue(false)]
            public bool ApplyRefreshPolicies { get; set; }

            /// <summary>
            /// If <c>true</>, makes the deployment principal the dataset owner. Only applies to existing datasets; new datasets
            /// created during the deployment are always owned by the deployment principal.
            /// </summary>
            /// <remarks>NotImplemented</remarks>
            [JsonProperty("takeOver")]
            public bool TakeOver { get; set; }

            [JsonProperty("gateway")]
            public GatewayOptions Gateway { get; set; }

            public class GatewayOptions
            { 
                /// <summary>
                /// If true, discovers gateways applicable to the dataset and lists them in the logs.
                /// </summary>
                [JsonProperty("discoverGateways")]
                public bool DiscoverGateways { get; set; }

                /// <summary>
                /// If specified, binds a newly created dataset to the corresponding gateway.
                /// Must be a valid Guid.
                /// Parameter expansion supported.
                /// </summary>
                [JsonProperty("gatewayId")]
                public string GatewayId { get; set; }

                /// <summary>
                /// An optional list of specific gateway datasources to bind to.
                /// Can be the datasource guid or name.
                /// </summary>
                [JsonProperty("dataSources")]
                public string[] DataSources { get; set; }

                /// <summary>
                /// Determines when, and if, the dataset is bound to a gateway during deployments.
                /// </summary>
                [JsonProperty("mode")]
                public GatewayBindMode Mode { get; set; } = GatewayBindMode.OnCreation;

                public enum GatewayBindMode
                {
                    /// <summary>
                    /// Only binds newly created datasets to the specified gateway, if any.
                    /// </summary>
                    OnCreation = 0,
                    /// <summary>
                    /// Never binds the dataset to a gateway.
                    /// </summary>
                    Disabled,
                    /// <summary>
                    /// Always binds the dataset to the specified gateway, if any.
                    /// </summary>
                    Always
                }
            }

            [JsonProperty("setCredentials")]
            public bool SetCredentials { get; set; }
        }

        public class ReportOptions
        { 
            [JsonProperty("customConnectionsTemplate")]
            public string CustomConnectionsTemplate { get; set; }
        }

        public class SqlScriptsOptions
        {
            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("enabled")]
            [DefaultValue(false)]
            public bool Enabled { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("ensureDatabase")]
            [DefaultValue(false)]
            public bool EnsureDatabase { get; set; }


            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("schema")]
            public string Schema { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("connection")]
            public IDictionary<string, string> Connection { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("path")]
            public string Path { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("htmlReportPath")]
            public string HtmlReportPath { get; set; }

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("logScriptOutput")]
            [DefaultValue(true)]
            public bool LogScriptOutput { get; set; } = true;

            /// <summary>
            /// TODO
            /// </summary>
            [JsonProperty("journal")]
            public SqlScriptsJournalOptions Journal { get; set; }

            public class SqlScriptsJournalOptions
            {

                /// <summary>
                /// </summary>
                [JsonProperty("schema")]
                public string Schema { get; set; }

                /// <summary>
                /// </summary>
                [JsonProperty("table")]
                public string Table { get; set; }
            }

            public enum SqlScriptsTransactionType
            { 
                None = 0,
                SingleTransaction,
                PerScript
            }
        }

        public class ConsoleOptions
        { 
            /// <summary>
            /// If specified, sets an explicit console width. This setting can be useful with certain CI/CD runners.
            /// </summary>
            [JsonProperty("width")]
            public int? Width { get; set; }

            /// <summary>
            /// Indicates whether or not tables printed to the console should fit the available space.
            /// If <c>false</c>, the table width will be auto calculated. Defaults to <c>false</c>.
            /// </summary>
            [JsonProperty("expandTable"), DefaultValue(false)]
            public bool ExpandTable { get; set; }
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
        /// Supports parameter expansion.
        /// </summary>
        [JsonProperty("xmlaDataSource")]
        public string XmlaDataSource { get; set; }

        /// <summary>
        /// Contains environment-scoped parameters.
        /// </summary>
        [JsonProperty("parameters")]
        public DeploymentParameters Parameters { get; set; }

        /// <summary>
        /// Allows customizing environment settings for embedded reports published as part of a dataset deployment.
        /// </summary>
        [JsonProperty("report")]
        public ReportOptions Report { get; set; }

        public class ReportOptions
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

        [JsonProperty("refresh")]
        public RefreshOptions Refresh { get; set; }

        public class RefreshOptions
        {
            /// <summary>
            /// Used to disable dataset refresh for specific environments only.
            /// Default is <c>false</c>.
            /// </summary>
            [JsonProperty("skip")]
            [DefaultValue(false)]
            public bool Skip { get; set; }

            [JsonProperty("type")]
            public DatasetRefreshType? Type { get; set; }

            [JsonProperty("objects")]
            public RefreshObjects Objects { get; set; }
        }
    }
}
