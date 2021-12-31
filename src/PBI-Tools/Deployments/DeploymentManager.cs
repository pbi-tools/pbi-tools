// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Serilog;

namespace PbiTools.Deployments
{
    using Model;
    using ProjectSystem;
    using Utils;

    public static class DeploymentParameters
    {
        public const string PBIXPROJ_FOLDER = "PBIXPROJ_FOLDER";
        public const string PBIXPROJ_NAME = "PBIXPROJ_NAME";
        public const string ENVIRONMENT = "ENVIRONMENT";
    }

    public class DeploymentManager
    {
        public const string DEFAULT_ENVIRONMENT_NAME = ".defaults";
        public static readonly Uri DefaultPowerBIApiBaseUri = new Uri("https://api.powerbi.com");

        internal static readonly ILogger Log = Serilog.Log.ForContext<DeploymentManager>();

        internal Func<Uri, ServiceClientCredentials, IPowerBIClient> PowerBIClientFactory { get; set; }
            = (uri, creds) => new PowerBIClient(uri, creds);
        
        internal Func<string, string, Uri, IPowerBITokenProvider> PowerBITokenProviderFactory { get; set; }
            = (clientId, clientSecret, authority) => new ServicePrincipalPowerBITokenProvider(clientId, clientSecret, authority);
        
        public async Task DeployAsync(PbixProject project, string environment, string key)
        {
            using (new CurrentDirectoryScope(Path.GetDirectoryName(project.OriginalPath)))
            { 
                var manifests = PbiDeploymentManifest.FromProject(project);

                if (!manifests.ContainsKey(key))
                    throw new DeploymentException($"The current project does not contain the specified deploymment '{key}'");

                var manifest = manifests[key];
                if (manifest.Mode == PbiDeploymentMode.Report)
                    await DeployReportAsync(manifest, key, environment);
                else
                    // TODO Enable other deployment modes here
                    throw new DeploymentException($"Unsupported deployment mode: '{manifest.Mode}'");
            }

            Log.Information("Deployment completed successfully.");
        }

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

            Log.Information("Starting deployment '{DeploymentLabel}' into environment: {Environment}...", label, environment);

            // Source: Folder | File

            if (manifest.Source.Type == PbiDeploymentSourceType.Folder)
            {
                var sourceFolderRegex = ConvertSearchPatternToRegex(manifest.Source.Path);

                var currentDir = Environment.CurrentDirectory;
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
                                { DeploymentParameters.ENVIRONMENT, environment },
                                { DeploymentParameters.PBIXPROJ_NAME, x.Directory.Name }
                            },
                            (dict, group) => {
                                dict[group.Name == "0" ? DeploymentParameters.PBIXPROJ_FOLDER : group.Name] = group.Value;
                                return dict;
                            })
#elif NET
                        Parameters = x.Match.Groups.Keys.Aggregate(
                            new Dictionary<string, string> {
                                { DeploymentParameters.ENVIRONMENT, environment },
                                { DeploymentParameters.PBIXPROJ_NAME, x.Directory.Name }
                            },
                            (dict, key) => {
                                dict[key == "0" ? DeploymentParameters.PBIXPROJ_FOLDER : key] = x.Match.Groups[key].Value;
                                return dict;
                            })
#endif
                    })
                    .ToArray();

                if (sourceFolders.Length == 0)
                {
                    Log.Warning("Found no matching source folders for path: '{SourcePath}'. Exiting...", manifest.Source.Path);
                    return;
                }
                else
                {
                    Log.Information("Found {Count} source folders to deploy.", sourceFolders.Length);
                }

                // Compile PBIX files *****************************************

                var tempDir = Environment.ExpandEnvironmentVariables(String.IsNullOrEmpty(manifest.Options.TempDir) ? "%TEMP%" : manifest.Options.TempDir);
                Log.Debug("Using TEMP dir: {TempDir}", tempDir);

                var reports = sourceFolders.Select(folder => CompileReportForDeployment(
                    manifest.Options, 
                    deploymentEnv, 
                    folder.FullName, 
                    tempDir, 
                    folder.Parameters.Aggregate(
                        manifest.Parameters ?? new Dictionary<string, string>(), 
                        (dict, x) => { dict[x.Key] = x.Value; return dict; }     // Folder params overwrite Manifest params
                    )
                )).ToArray();

                // Upload *****************************************************

                // Get auth token
                if (manifest.Authentication.Type != PbiDeploymentAuthenticationType.ServicePrincipal)
                    throw new DeploymentException("Only ServicePrincipal authentication is supported.");
                var authSettings = manifest.Authentication.Validate();

                var tokenProvider = PowerBITokenProviderFactory(
                    authSettings.ClientId.ExpandEnv(),
                    authSettings.ClientSecret.ExpandEnv(),
                    new Uri($"https://login.microsoftonline.com/{authSettings.TenantId.ExpandEnv()}")
                );

                AuthenticationResult authResult = await tokenProvider.AcquireTokenAsync();
                var tokenCredentials = new TokenCredentials(authResult.AccessToken, authResult.TokenType);
                Log.Information("Access token received. Expires On: {ExpiresOn}", authResult.ExpiresOn);

                using (var powerbi = PowerBIClientFactory(manifest?.Options?.PbiBaseUri ?? DefaultPowerBIApiBaseUri, tokenCredentials))
                {
                    var workspaceIdCache = new Dictionary<string, Guid>();

                    foreach (var report in reports)
                    {
                        await ImportReportAsync(report, powerbi, workspaceIdCache);
                    }
                }

            }
            else
            {
                throw new DeploymentException($"Unsupported source type: '{manifest.Source.Type}'");
            }

        }

        internal static Regex ConvertSearchPatternToRegex(string pattern)
        {
            var result = new StringBuilder(pattern)
                .Replace('\\', '/')          // normalize all path separators
                .Replace("./", null, 0, 2)   // remove leading rel folder reference
                .Replace("/", "\\/")         // insert regex mask character
                .ToString();

            // Convert anonymous wildcards '*' -- Exposed as "MATCH_i"
            var matchId = 0;
            result = Regex.Replace(result, @"\*", m => @$"(?<MATCH_{++matchId}>[^:*?\\\/""<>|]*)");
            
            // Convert named params '{{PARAM_NAME}}'
            result = Regex.Replace(result, @"{{([^:*?\\\/""<>|]*)}}", m => @$"(?<{m.Groups[1]}>[^:*?\\\/""<>|]*)");

            Log.Debug("Converted search pattern: '{SearchPattern}' to Regex: '{Regex}'", pattern, result);

            return new Regex(result, RegexOptions.Compiled);
        }

        internal static ReportDeploymentArgs CompileReportForDeployment(PbiDeploymentOptions options, PbiDeploymentEnvironment environment, string pbixProjFolder, string tempDir, IDictionary<string, string> parameters)
        {
            var reportPath = Path.Combine(tempDir, Guid.NewGuid().ToString(), $"{new DirectoryInfo(pbixProjFolder).Name}.pbix");
            Log.Information("Creating PBIX from report source at: '{Path}'...", reportPath);

            var model = PbixModel.FromFolder(pbixProjFolder);

            model.ToFile(reportPath, PowerBI.PbiFileFormat.PBIX);

            return new ReportDeploymentArgs
            {
                Model = model,
                Options = options,
                Environment = environment,
                Parameters = parameters,
                PbixProjFolder = pbixProjFolder,
                PbixTempPath = reportPath,
                DisplayName = Path.GetFileName(reportPath),
            };
        }

        internal static async Task ImportReportAsync(ReportDeploymentArgs args, IPowerBIClient powerbi, IDictionary<string, Guid> workspaceIdCache)
        {
            // Determine Workspace
            var workspaceValue = args.Environment.Workspace.ExpandParameters(args.Parameters);
            var workspaceId = Guid.TryParse(workspaceValue, out var id) ? id : await workspaceValue.ResolveWorkspaceIdAsync(workspaceIdCache, powerbi);

            Import import;
            using (var file = File.OpenRead(args.PbixTempPath))
            {
                import = await powerbi.Imports.PostImportWithFileAsyncInGroup(workspaceId, file,
                    datasetDisplayName: args.DisplayName, // will FAIL without the parameter (although the API marks it as optional)
                    nameConflict: args.Options.NameConflict
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

        public class ReportDeploymentArgs
        {
            public PbiDeploymentOptions Options { get; set; }
            public PbiDeploymentEnvironment Environment { get; set; }
            public IDictionary<string, string> Parameters { get; set; }
            public string PbixProjFolder { get; set; }
            public string DisplayName { get; set; }
            public string PbixTempPath { get; set; }
            public IPbixModel Model { get; set; }
        }

    }


    [Serializable]
    public class DeploymentException : Exception
    {
        public DeploymentException() { }
        public DeploymentException(string message) : base(message) { }
        public DeploymentException(string message, System.Exception inner) : base(message, inner) { }
        protected DeploymentException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}