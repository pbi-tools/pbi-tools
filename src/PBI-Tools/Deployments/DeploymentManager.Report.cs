// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{
    using Model;
    using PowerBI;
    using ProjectSystem;
    using Utils;

    public partial class DeploymentManager
    {

        internal async Task DeployReportAsync(PbiDeploymentManifest manifest, string label, string environment)
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

            Log.Write(WhatIfLogLevel, "Determining reports from {SourceType} source: \"{SourcePath}\"", manifest.Source.Type, manifest.Source.Path);
            Log.Write(WhatIfLogLevel, "Base folder: {BasePath}", basePath);

            var reports = manifest.Source.Type switch
            {
                PbiDeploymentSourceType.Folder => GenerateReportsFromFolderSource(manifest, deploymentEnv, basePath),
                PbiDeploymentSourceType.File   => GetReportsFromFileSource(manifest, deploymentEnv, basePath),
                _ => throw new DeploymentException($"Unsupported source type: '{manifest.Source.Type}'")
            };

            // TODO Possibly perform further transforms here

            // Upload *****************************************************

            // Get auth token
            if (manifest.Authentication.Type != PbiDeploymentAuthenticationType.ServicePrincipal)
                throw new DeploymentException("Only ServicePrincipal authentication is supported.");

            Log.Write(WhatIfLogLevel, "Acquiring access token...");
            var tokenProvider = PowerBITokenProviderFactory(manifest.Authentication.ExpandAndValidate());

            AuthenticationResult authResult;
            
            try {
                authResult = await tokenProvider.AcquireTokenAsync();
                Log.Write(WhatIfLogLevel, "Token Endpoint: {TokenEndpoint}", authResult?.AuthenticationResultMetadata.TokenEndpoint);
            }
            catch (MsalServiceException ex) {
                throw DeploymentException.From(ex);
            }

            var tokenCredentials = new TokenCredentials(authResult.AccessToken, authResult.TokenType);
            Log.Information("Access token received. Expires On: {ExpiresOn}", authResult.ExpiresOn);
            if (WhatIf) Log.Information("---");

            using var powerbi = PowerBIClientFactory(manifest?.Options?.PbiBaseUri ?? DefaultPowerBIApiBaseUri, tokenCredentials);
            var workspaceIdCache = new Dictionary<string, Guid>();

            foreach (var report in reports)
            {
                var workspace = deploymentEnv.Workspace.ExpandParameters(report.Parameters);

                if (WhatIf) {
                    Log.Information("Report: {Path}", report.SourcePath);
                    Log.Information("  DisplayName: {DisplayName}", report.DisplayName);
                    Log.Information("  Parameters:");
                    foreach (var parameter in report.Parameters)
                        Log.Information("  * {ParamKey} = \"{ParamValue}\"", parameter.Key, parameter.Value);
                    Log.Information("  Workspace: {Workspace}", workspace);
                    Log.Information("---");
                    continue;
                }

                try { 
                    await ImportReportAsync(report, powerbi, workspace, workspaceIdCache);
                }
                catch (Microsoft.Rest.HttpOperationException ex) {
                    if (ex.Response.Content.TryParseJson<JObject>(out var json)) {
                        throw new DeploymentException($"HTTP Error: {ex.Response.StatusCode}\n{json.ToString(Newtonsoft.Json.Formatting.Indented)}", ex);
                    }
                    throw;
                }
            }

        }

        internal ReportDeploymentInfo[] GenerateReportsFromFolderSource(PbiDeploymentManifest manifest, PbiDeploymentEnvironment environment, string baseFolder = null)
        {
            var sourceFolderRegex = ConvertSearchPatternToRegex(manifest.Source.Path);

            var currentDir = baseFolder ?? Environment.CurrentDirectory;
            var sourceFolders = Directory.EnumerateDirectories(
                    currentDir,
                    "*",
                    SearchOption.AllDirectories
                )
                .Select(d =>
                {
                    var dir = new DirectoryInfo(d);
#if NETFRAMEWORK
                    var relPath = new Uri(currentDir + "/").MakeRelativeUri(new Uri(dir.FullName)).OriginalString;
#elif NET
                    var relPath = Path.GetRelativePath(currentDir, dir.FullName);
#endif
                    var match = sourceFolderRegex.Match(relPath.Replace('\\', '/'));
                    return new
                    {
                        Directory = dir,
                        Match = match
                    };
                })
                .Where(x => x.Match.Success && PbixProject.IsPbixProjFolder(x.Directory.FullName))
                .Select(x => new {
                    x.Directory.FullName,
#if NETFRAMEWORK
                    Parameters = x.Match.Groups.OfType<System.Text.RegularExpressions.Group>()
                        .Aggregate(
                            new Dictionary<string, string> {
                                    { DeploymentParameters.Names.ENVIRONMENT, environment.Name },
                                    { DeploymentParameters.Names.PBIXPROJ_NAME, x.Directory.Name },
                                    { DeploymentParameters.Names.FILE_NAME_WITHOUT_EXT, x.Directory.Name }
                            },
                            (dict, group) => {
                                dict[group.Name == "0" ? DeploymentParameters.Names.PBIXPROJ_FOLDER : group.Name] = group.Value;
                                return dict;
                            }
                        )
#elif NET
                    Parameters = x.Match.Groups.Keys.Aggregate(
                        new Dictionary<string, string> {
                            { DeploymentParameters.Names.ENVIRONMENT, environment.Name },
                            { DeploymentParameters.Names.PBIXPROJ_NAME, x.Directory.Name },
                            { DeploymentParameters.Names.FILE_NAME_WITHOUT_EXT, x.Directory.Name }
                        },
                        (dict, key) => {
                            dict[key == "0" ? DeploymentParameters.Names.PBIXPROJ_FOLDER : key] = x.Match.Groups[key].Value;
                            return dict;
                        })
#endif
                })
                .ToArray();

            if (sourceFolders.Length == 0)
            {
                Log.Warning("Found no matching source folders for path: '{SourcePath}'. Exiting...", manifest.Source.Path);
                return new ReportDeploymentInfo[0];
            }
            else
            {
                Log.Information("Found {Count} source folders to deploy.", sourceFolders.Length);
            }

            // Compile PBIX files *****************************************

            var tempDir = Environment.ExpandEnvironmentVariables(
                String.IsNullOrEmpty(manifest.Options.TempDir)
                ? "%TEMP%"
                : manifest.Options.TempDir
            );
            Log.Debug("Using TEMP dir: {TempDir}", tempDir);

            var reports = sourceFolders.Select(folder => CompileReportForDeployment(  // TODO Perform transforms here (connection swap, for instance)
                manifest.Options,
                environment,
                folder.FullName,
                tempDir,
                folder.Parameters.Aggregate(
                    manifest.Parameters.ExpandEnv(),
                    (dict, x) => { dict[x.Key] = DeploymentParameter.From(x.Value); return dict; }     // Folder params overwrite Manifest params
                )
            )).ToArray();

            return reports;
        }

        internal ReportDeploymentInfo[] GetReportsFromFileSource(PbiDeploymentManifest manifest, PbiDeploymentEnvironment environment, string baseFolder = null)
        {
            var sourceFileRegex = ConvertSearchPatternToRegex(manifest.Source.Path);

            var currentDir = baseFolder ?? Environment.CurrentDirectory;
            var sourceFiles = Directory.EnumerateFiles(
                    currentDir,
                    "*.*",
                    SearchOption.AllDirectories
                )
                .Select(path =>
                {
                    var file = new FileInfo(path);
#if NETFRAMEWORK
                    var relPath = new Uri(currentDir + "/").MakeRelativeUri(new Uri(file.FullName)).OriginalString;
#elif NET
                    var relPath = Path.GetRelativePath(currentDir, file.FullName);
#endif
                    var match = sourceFileRegex.Match(relPath.Replace('\\', '/'));
                    return new
                    {
                        File = file,
                        Match = match
                    };
                })
                .Where(x => {
                    if (!x.Match.Success)
                        return false;
                    if (x.File.Extension.ToLowerInvariant() != ".pbix")
                        Log.Warning("This file matched by the source expression does not have a .pbix extension and will likely fail to deploy: {FullPath}", x.File.FullName);
                    return true;
                })
                .Select(x => new {
                    x.File.FullName,
#if NETFRAMEWORK
                    Parameters = x.Match.Groups.OfType<System.Text.RegularExpressions.Group>()
                        .Aggregate(
                            new Dictionary<string, string> {
                                    { DeploymentParameters.Names.ENVIRONMENT, environment.Name },
                                    { DeploymentParameters.Names.FILE_NAME, x.File.Name },
                                    { DeploymentParameters.Names.FILE_NAME_WITHOUT_EXT, Path.GetFileNameWithoutExtension(x.File.Name) }
                            },
                            (dict, group) => {
                                dict[group.Name == "0" ? DeploymentParameters.Names.FILE_PATH : group.Name] = group.Value;
                                return dict;
                            }
                        )
#elif NET
                    Parameters = x.Match.Groups.Keys.Aggregate(
                        new Dictionary<string, string> {
                            { DeploymentParameters.Names.ENVIRONMENT, environment.Name },
                            { DeploymentParameters.Names.FILE_NAME, x.File.Name },
                            { DeploymentParameters.Names.FILE_NAME_WITHOUT_EXT, Path.GetFileNameWithoutExtension(x.File.Name) }
                        },
                        (dict, key) => {
                            dict[key == "0" ? DeploymentParameters.Names.FILE_PATH : key] = x.Match.Groups[key].Value;
                            return dict;
                        })
#endif
                })
                .ToArray();

            if (sourceFiles.Length == 0)
            {
                Log.Warning("Found no matching source files for path: '{SourcePath}'. Exiting...", manifest.Source.Path);
                return new ReportDeploymentInfo[0];
            }
            else
            {
                Log.Information("Found {Count} source files to deploy.", sourceFiles.Length);
            }

            // Compile PBIX files *****************************************

            var tempDir = Environment.ExpandEnvironmentVariables(
                String.IsNullOrEmpty(manifest.Options.TempDir)
                ? "%TEMP%"
                : manifest.Options.TempDir
            );
            Log.Debug("Using TEMP dir: {TempDir}", tempDir);


            return sourceFiles.Select(file =>
            {
                var parameters = file.Parameters.Aggregate(
                    manifest.Parameters.ExpandEnv(),
                    (dict, x) => { dict[x.Key] = DeploymentParameter.From(x.Value); return dict; }     // Folder params overwrite Manifest params
                );
                return new ReportDeploymentInfo
                {
                    Options = manifest.Options,
                    Parameters = parameters,
                    SourcePath = file.FullName,
                    PbixPath = file.FullName,
                    DisplayName = environment.DisplayName.ExpandParameters(parameters) ?? Path.GetFileName(file.FullName),
                };
            })
            .ToArray();
        }

        internal ReportDeploymentInfo CompileReportForDeployment(PbiDeploymentOptions options, PbiDeploymentEnvironment environment, string pbixProjFolder, string tempDir, IDictionary<string, DeploymentParameter> parameters)
        {
            var reportPath = Path.Combine(tempDir, Guid.NewGuid().ToString(), $"{new DirectoryInfo(pbixProjFolder).Name}.pbix");

            var reportInfo = new ReportDeploymentInfo
            {
                Options = options,
                Parameters = parameters,
                SourcePath = pbixProjFolder,
                PbixPath = reportPath,
                DisplayName = environment.DisplayName.ExpandParameters(parameters) ?? Path.GetFileName(reportPath),
            };

            if (!WhatIf) { 
                Log.Information("Creating PBIX from report source at: '{Path}'...", reportPath);

                var model = PbixModel.FromFolder(pbixProjFolder);
                model.ToFile(reportPath, PbiFileFormat.PBIX);

                reportInfo.Model = model;
            }

            return reportInfo;
        }

        internal static async Task ImportReportAsync(ReportDeploymentInfo args, IPowerBIClient powerbi, string workspace, IDictionary<string, Guid> workspaceIdCache)
        {
            // Determine Workspace
            var workspaceId = Guid.TryParse(workspace, out var id)
                ? id
                : await workspace.ResolveWorkspaceIdAsync(powerbi, workspaceIdCache);

            Import import;
            using (var file = File.OpenRead(args.PbixPath))
            {
                import = await powerbi.Imports.PostImportWithFileAsyncInGroup(workspaceId, file,
                    datasetDisplayName: args.DisplayName, // will FAIL without the parameter (although the API marks it as optional)
                    nameConflict: args.Options.Import.NameConflict,
                    skipReport: args.Options.Import.SkipReport,
                    overrideReportLabel: args.Options.Import.OverrideReportLabel,
                    overrideModelLabel: args.Options.Import.OverrideModelLabel
                );
            }

            while (import.ImportState != "Succeeded") // Must use magic string here :(
            {
                if (import.ImportState != null)
                    Log.Information("Import: {Name}, State: {ImportState} (Id: {Id})", import.Name, import.ImportState, import.Id);

                if (import.ImportState == "Failed")
                    throw new DeploymentException($"Deployment '{import.Name}' ({import.Id}) failed.");

                await Task.Delay(500);

                import = await powerbi.Imports.GetImportInGroupAsync(workspaceId, import.Id);
            }

            Log.Information("Import succeeded: {Id} ({Name})\n\tReport: {ReportId} \"{ReportName}\"\n\tUrl: {WebUrl}"
                , import.Id
                , import.Name
                , import.Reports[0].Id
                , import.Reports[0].Name
                , import.Reports[0].WebUrl
            );
            Log.Information("Report Created: {Created}", import.CreatedDateTime);
            Log.Information("Report Updated: {Updated}", import.UpdatedDateTime);
        }

        public class ReportDeploymentInfo
        {
            public PbiDeploymentOptions Options { get; set; }
            public IDictionary<string, DeploymentParameter> Parameters { get; set; }
            public string SourcePath { get; set; }
            public string DisplayName { get; set; }
            public string PbixPath { get; set; }
            public IPbixModel Model { get; set; }
        }

    }
}