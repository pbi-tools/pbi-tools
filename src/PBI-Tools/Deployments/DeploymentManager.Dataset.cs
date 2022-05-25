// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Deployments
{
    using Model;

    public partial class DeploymentManager
    {

        internal async Task DeployDatasetAsync(PbiDeploymentManifest manifest, string label, string environment)
        {
            if (!manifest.Environments.ContainsKey(environment))
                throw new DeploymentException($"The manifest does not contain the specified environment: {environment}.");

            var deploymentEnv = manifest.Environments[environment];

            if (deploymentEnv.Disabled)
            {
                Log.Warning("Deployment environment '{Environment}' disabled. Aborting.", environment);
                return;
            }

            Log.Information("Starting deployment '{DeploymentLabel}' into environment: {Environment} ...", label, environment);

            // Source: Folder | File
            var basePath = (BasePath == null)
                ? new FileInfo(Project.OriginalPath).DirectoryName
                : new DirectoryInfo(BasePath).FullName;

            Log.Write(WhatIfLogLevel, "Determining dataset from {SourceType} source: \"{SourcePath}\"", manifest.Source.Type, manifest.Source.Path);
            Log.Write(WhatIfLogLevel, "Base folder: {BasePath}", basePath);

            var dataset = manifest.Source.Type switch
            {
                PbiDeploymentSourceType.Folder => GenerateDatasetFromFolderSource(manifest, deploymentEnv, basePath),
                PbiDeploymentSourceType.File => GetDatasetFromFileSource(manifest, deploymentEnv, basePath),
                _ => throw new DeploymentException($"Unsupported source type: '{manifest.Source.Type}'")
            };

            // TODO Possibly perform further transforms here
            // Set/Replace model parameters
            // Inject version information

            // Get auth token
            if (manifest.Authentication.Type != PbiDeploymentAuthenticationType.ServicePrincipal)
                throw new DeploymentException("Only ServicePrincipal authentication is supported.");

            Log.Write(WhatIfLogLevel, "Acquiring access token...");
            AuthenticationResult authResult;
            var tokenProvider = PowerBITokenProviderFactory(manifest.Authentication.ExpandAndValidate());

            try
            {
                authResult = await tokenProvider.AcquireTokenAsync();
                Log.Write(WhatIfLogLevel, "Token Endpoint: {TokenEndpoint}", authResult?.AuthenticationResultMetadata.TokenEndpoint);
            }
            catch (MsalServiceException ex)
            {
                throw DeploymentException.From(ex);
            }

            var tokenCredentials = new TokenCredentials(authResult.AccessToken, authResult.TokenType);
            Log.Information("Access token received. Expires On: {ExpiresOn}", authResult.ExpiresOn);
            if (WhatIf) Log.Information("---");

            using var powerbi = PowerBIClientFactory(manifest?.Options?.PbiBaseUri ?? DefaultPowerBIApiBaseUri, tokenCredentials);
            using var server = new TOM.Server();

            var workspace = deploymentEnv.Workspace.ExpandParameters(dataset.Parameters);
            var workspaceId = Guid.TryParse(workspace, out var id)
                ? id
                : await workspace.ResolveWorkspaceIdAsync(powerbi);

            if (WhatIf)
            {
                Log.Information("Dataset: {Path}", dataset.SourcePath);
                Log.Information("  DisplayName: {DisplayName}", dataset.DisplayName);
                Log.Information("  Parameters:");
                foreach (var parameter in dataset.Parameters)
                    Log.Information("  * {ParamKey} = \"{ParamValue}\"", parameter.Key, parameter.Value);
                Log.Information("  Workspace: {Workspace} ({WorkspaceId})", workspace, workspaceId);
                Log.Information("---");
            }

            var dataSource = deploymentEnv.XmlaDataSource ?? $"powerbi://api.powerbi.com/v1.0/myorg/{workspace}";
            var connectionString = new System.Data.Common.DbConnectionStringBuilder {
                { "Data Source", dataSource },
                { "Password", authResult.AccessToken }
            }.ConnectionString;

            Log.Information("Connecting to XMLA endpoint: {XmlaDataSource}", dataSource);
            server.Connect(connectionString);

            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug) || WhatIf)
            {
                Log.Write(WhatIfLogLevel, "Server Properties:");
                Log.Write(WhatIfLogLevel, "* CompatibilityMode           : {CompatibilityMode}", server.CompatibilityMode);
                Log.Write(WhatIfLogLevel, "* ConnectionString            : {ConnectionString}", server.ConnectionString);
                Log.Write(WhatIfLogLevel, "* DefaultCompatibilityLevel   : {DefaultCompatibilityLevel}", server.DefaultCompatibilityLevel);
                Log.Write(WhatIfLogLevel, "* Edition                     : {Edition}", server.Edition);
                Log.Write(WhatIfLogLevel, "* ID                          : {ID}", server.ID);
                Log.Write(WhatIfLogLevel, "* Name                        : {Name}", server.Name);
                Log.Write(WhatIfLogLevel, "* SupportedCompatibilityLevels: {SupportedCompatibilityLevels}", server.SupportedCompatibilityLevels);
                Log.Write(WhatIfLogLevel, "* Version                     : {Version}", server.Version);
            }

            Log.Write(WhatIfLogLevel, "Deserializing tabular model from sources...");
            var dbNew = TOM.JsonSerializer.DeserializeDatabase(dataset.Model.DataModel.ToString(), new TOM.DeserializeOptions { });

            var datasetId = default(string);
            var createdNewDb = false;

            void LogDbInfo(TOM.Database db, string label, Serilog.Events.LogEventLevel level = Serilog.Events.LogEventLevel.Information) {
                Log.Write(level, label);
                Log.Write(level, "TOM Database Properties:");
                Log.Write(level, "* ID                    : {ID}", db.ID);
                Log.Write(level, "* Name                  : {Name}", db.Name);
                Log.Write(level, "* Description           : {Description}", db?.Model.Description);
                Log.Write(level, "* CompatibilityLevel    : {CompatibilityLevel}", db.CompatibilityLevel);
                Log.Write(level, "* CreatedTimestamp      : {CreatedTimestamp}", db.CreatedTimestamp);
                Log.Write(level, "* StructureModifiedTime : {StructureModifiedTime}", db?.Model.StructureModifiedTime);
                Log.Write(level, "* EstimatedSize         : {EstimatedSize}", db.EstimatedSize);
            };

            Log.Write(WhatIfLogLevel, "Checking for existing database with matching name...");
            if (!server.Databases.ContainsName(dataset.DisplayName))
            {
                using (var newDb = new TOM.Database
                {
                    Name = dataset.DisplayName,
                    CompatibilityLevel = dbNew.CompatibilityLevel,
                    StorageEngineUsed = AMO.StorageEngineUsed.TabularMetadata
                })
                {
                    if (WhatIf)
                    {
                        // Does NOT exist...
                        Log.Information("Workspace '{Workspace}' does not have exiting dataset named '{DatasetName}'.", workspace, dataset.DisplayName);
                        return;
                    }
                    else
                    {
                        server.Databases.Add(newDb);
                        newDb.Update(AMO.UpdateOptions.ExpandFull);

                        Log.Information("Created new Power BI dataset: {ID}", newDb.ID);
                    }

                    createdNewDb = true;
                }
            }
            else if (WhatIf)
            {
                // Database exists...
                using var db = server.Databases.GetByName(dataset.DisplayName);
                LogDbInfo(db, "Matching dataset found.");
                return;
            }

            using (var remoteDb = server.Databases.GetByName(dataset.DisplayName)) // Database with speified name is guaranteed to exist at this point
            {
                if (!createdNewDb)
                {
                    LogDbInfo(remoteDb, "Found existing dataset.");

                    // Ensure matching CompatibilityLevel
                    if (remoteDb.CompatibilityLevel != dbNew.CompatibilityLevel)
                    {
                        remoteDb.CompatibilityLevel = dbNew.CompatibilityLevel;
                        remoteDb.Update(AMO.UpdateOptions.ExpandFull);
                    }
                }

                dbNew.Name = dataset.DisplayName; // avoid name clash

                // Transfer new model schema...
                dbNew.Model.CopyTo(remoteDb.Model);

                Log.Debug("Updating model metadata...");
                var updateResults = remoteDb.Model.SaveChanges();
                // TODO Report updateResults.Impact?
                if (updateResults.XmlaResults != null && updateResults.XmlaResults.Count > 0)
                {
                    Log.Information("Update Results:");

                    foreach (var result in updateResults.XmlaResults.OfType<AMO.XmlaResult>())
                    {
                        Log.Information(result.Value);
                        foreach (var message in result.Messages.OfType<AMO.XmlaMessage>())
                            Log.Warning("- [{Severity}] {Description}\n\t{Location}\n--", message.GetType().Name, message.Description, message.Location.SourceObject);
                    }
                }

                datasetId = remoteDb.ID;

                Log.Information("Model deployment succeeded.");
            }

            var pbiDataset = await powerbi.Datasets.GetDatasetInGroupAsync(workspaceId, datasetId);
            Log.Information("Power BI Dataset Details:");
            Log.Information("* ID                     : {ID}", pbiDataset.Id);
            Log.Information("* Name                   : {Name}", pbiDataset.Name);
            Log.Information("* WebUrl                 : {WebUrl}", pbiDataset.WebUrl);
            Log.Information("* ConfiguredBy           : {ConfiguredBy}", pbiDataset.ConfiguredBy);
            Log.Information("* CreatedDate            : {CreatedDate}", pbiDataset.CreatedDate);
            Log.Information("* IsRefreshable          : {IsRefreshable}", pbiDataset.IsRefreshable);
            Log.Information("* IsOnPremGatewayRequired: {IsOnPremGatewayRequired}", pbiDataset.IsOnPremGatewayRequired);
            Log.Information("* TargetStorageMode      : {TargetStorageMode}", pbiDataset.TargetStorageMode);

            // ***** Dataset Refresh *****
            if (deploymentEnv.Refresh)
            {
                if (createdNewDb && dataset.Options.Refresh.SkipNewDataset) {
                    Log.Information("Skipping refresh because of the 'skipNewDataset' refresh option. You will likely need to set credentials and/or dataset gateways via Power BI Service first.");
                }
                else
                {
                    Log.Information("Starting dataset refresh ({RefreshType}) ...", dataset.Options.Refresh.Type);
                    switch (dataset.Options.Refresh.Method) {
                        case PbiDeploymentOptions.RefreshOptions.RefreshMethod.API:
                            throw new PbiToolsCliException(ExitCode.NotImplemented, "The 'API' refresh method is not implemented. Use 'XMLA' instead.");
                        case PbiDeploymentOptions.RefreshOptions.RefreshMethod.XMLA:
                            using (var db = server.Databases[datasetId]) {
                                RefreshXmla(db, dataset.Options.Refresh);
                            }
                            break;
                        default:
                            throw new DeploymentException($"Invalid refresh method '{dataset.Options.Refresh.Method}'.");
                    }
                    Log.Information("Refresh completed.");
                }
            }
        }

        internal static void RefreshXmla(TOM.Database database, PbiDeploymentOptions.RefreshOptions refreshOptions)
        {
            // Mapping API RefreshType -> TOM RefreshType
            var refreshType = (TOM.RefreshType)Enum.Parse(typeof(TOM.RefreshType), $"{refreshOptions.Type}");

            database.Model.RequestRefresh(refreshType);
            try
            {
                var refreshResults = database.Model.SaveChanges();
                if (refreshResults.XmlaResults != null && refreshResults.XmlaResults.Count > 0)
                {
                    Log.Information("Refresh Results:");

                    foreach (var result in refreshResults.XmlaResults.OfType<AMO.XmlaResult>())
                    {
                        Log.Information(result.Value);
                        foreach (var message in result.Messages.OfType<AMO.XmlaMessage>())
                            Log.Warning("- [{Severity}] {Description}\n\t{Location}\n--", message.GetType().Name, message.Description, message.Location.SourceObject);
                    }
                }
            }
            catch (AMO.OperationException ex) when (ex.Message.Contains("DMTS_DatasourceHasNoCredentialError")) {
                throw new DeploymentException("Refresh failed because of missing credentials. See https://docs.microsoft.com/power-bi/enterprise/service-premium-connect-tools#setting-data-source-credentials for further details.", ex);
            }
        }


        private DatasetDeploymentInfo GetDatasetFromFileSource(PbiDeploymentManifest manifest, PbiDeploymentEnvironment deploymentEnv, string basePath)
        {
            // Assuming we have a BIM file
            var sourceFile = new FileInfo(Path.Combine(basePath, manifest.Source.Path));
            var converter = PbixModelConverter.FromFile(sourceFile);

            var parameters = new Dictionary<string, string> {
                { DeploymentParameters.ENVIRONMENT, deploymentEnv.Name },
                { DeploymentParameters.FILE_NAME, sourceFile.Name },
                { DeploymentParameters.FILE_NAME_WITHOUT_EXT, Path.GetFileNameWithoutExtension(sourceFile.Name) }
            };

            return new() {
                Model = converter.Model,
                SourcePath = sourceFile.FullName,
                DisplayName = deploymentEnv.DisplayName.ExpandParameters(parameters) ?? Path.GetFileNameWithoutExtension(sourceFile.Name),
                Options = manifest.Options,
                Parameters = parameters.Aggregate(
                    manifest.Parameters ?? new Dictionary<string, string>(), // TODO Support ENV expansion in manifest params
                    (dict, x) => { dict[x.Key] = x.Value; return dict; }     // File params overwrite Manifest params
                )
            };
        }

        private DatasetDeploymentInfo GenerateDatasetFromFolderSource(PbiDeploymentManifest manifest, PbiDeploymentEnvironment deploymentEnv, string basePath)
        {
            // Assuming PbixProj or Model folder
            var sourceFolder = new DirectoryInfo(Path.Combine(basePath, manifest.Source.Path));
            var converter = PbixModelConverter.FromFolder(sourceFolder);

            var parameters = new Dictionary<string, string> {
                { DeploymentParameters.ENVIRONMENT, deploymentEnv.Name },
                { DeploymentParameters.PBIXPROJ_FOLDER, Path.GetFileName(converter.Model.SourcePath) },
                { DeploymentParameters.FILE_NAME_WITHOUT_EXT, Path.GetFileName(converter.Model.SourcePath) }
            };

            return new() {
                Model = converter.Model,
                SourcePath = sourceFolder.FullName,
                DisplayName = deploymentEnv.DisplayName.ExpandParameters(parameters) ?? sourceFolder.Name,
                Options = manifest.Options,
                Parameters = parameters.Aggregate(
                    manifest.Parameters ?? new Dictionary<string, string>(), // TODO Support ENV expansion in manifest params
                    (dict, x) => { dict[x.Key] = x.Value; return dict; }     // File params overwrite Manifest params
                )
            };
        }

        public class DatasetDeploymentInfo
        {
            public PbiDeploymentOptions Options { get; set; }
            public IDictionary<string, string> Parameters { get; set; }
            public string SourcePath { get; set; }
            public string DisplayName { get; set; }
            public IPbixModel Model { get; set; }
        }

    }
}