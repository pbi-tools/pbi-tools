// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
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

    public class DeploymentManager
    {
        public const string POWERBI_API_RESOURCE = "https://analysis.windows.net/powerbi/api";
        public static readonly Uri DefaultPowerBIApiBaseUri = new Uri("https://api.powerbi.com");

        private static readonly ILogger Log = Serilog.Log.ForContext<DeploymentManager>();


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
                    throw new DeploymentException($"Unsupported deployment mode: '{manifest.Mode}'");
            }
        }

        internal static async Task DeployReportAsync(PbiDeploymentManifest manifest, string label, string environment)
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

            // Build PBIX

            var tempDir = Environment.ExpandEnvironmentVariables(String.IsNullOrEmpty(manifest.Options.TempDir) ? "%TEMP%" : manifest.Options.TempDir);
            Log.Debug("Using TEMP dir: {TempDir}", tempDir);
            var reportPath = Path.Combine(tempDir, $"{new DirectoryInfo(manifest.Source.Path).Name}.pbix");
            Log.Debug("Creating PBIX from report source at: '{Path}'...", reportPath);

            var model = PbixModel.FromFolder(manifest.Source.Path);

            model.ToFile(reportPath, PowerBI.PbiFileFormat.PBIX);

            // Get auth token

            var app = ConfidentialClientApplicationBuilder
                .Create(Environment.ExpandEnvironmentVariables(manifest.Authentication.ClientId))
                .WithClientSecret(Environment.ExpandEnvironmentVariables(manifest.Authentication.ClientSecret))
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{Environment.ExpandEnvironmentVariables(manifest.Authentication.TenantId)}"))
                // TODO Support custom authority
                .Build();

            string[] scopes = new string[] { $"{POWERBI_API_RESOURCE}/.default" };

            AuthenticationResult result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            var tokenCredentials = new TokenCredentials(result.AccessToken, result.TokenType);
            Log.Information("Access token received. Expires On: {ExpiresOn}", result.ExpiresOn);

            // Use Power BI API to import PBIX

            using (var powerbi = new PowerBIClient(DefaultPowerBIApiBaseUri, tokenCredentials))
            { 
                Import import;
                using (var file = File.OpenRead(reportPath))
                {
                    import = await powerbi.Imports.PostImportWithFileAsyncInGroup(deploymentEnv.WorkspaceId, file,
                        datasetDisplayName: Path.GetFileName(reportPath), // will FAIL without the parameter (although the API marks it as optional)
                        nameConflict: manifest.Options.NameConflict
                    );
                }

                while (import.ImportState != "Succeeded") // Must use magic string here :(
                {
                    if (import.ImportState != null)
                        Log.Information("Import: {Name}, State: {ImportState} (Id: {Id})", import.Name, import.ImportState, import.Id);

                    if (import.ImportState == "Failed")
                        throw new DeploymentException($"Deployment '{import.Name}' ({import.Id}) failed.");

                    await Task.Delay(500);

                    import = await powerbi.Imports.GetImportInGroupAsync(deploymentEnv.WorkspaceId, import.Id);
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
        }
    }

    [System.Serializable]
    public class DeploymentException : System.Exception
    {
        public DeploymentException() { }
        public DeploymentException(string message) : base(message) { }
        public DeploymentException(string message, System.Exception inner) : base(message, inner) { }
        protected DeploymentException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}