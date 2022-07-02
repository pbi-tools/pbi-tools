// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;
using Serilog;
using Serilog.Events;

namespace PbiTools.Deployments
{
    using Configuration;
    using ProjectSystem;

    public partial class DeploymentManager
    {
        public const string DEFAULT_ENVIRONMENT_NAME = ".defaults";  // TODO Enable default environment
        public static readonly Uri DefaultPowerBIApiBaseUri = new("https://api.powerbi.com");

        internal static readonly ILogger Log = Serilog.Log.ForContext<DeploymentManager>();

        internal Func<Uri, ServiceClientCredentials, IPowerBIClient> PowerBIClientFactory { get; set; }
            = (uri, creds) => new PowerBIClient(uri, creds);

        internal Func<PbiDeploymentAuthentication, IPowerBITokenProvider> PowerBITokenProviderFactory { get; set; }
            = (options) => new ServicePrincipalPowerBITokenProvider(options);


        public DeploymentManager(PbixProject project) : this(project, Program.AppSettings) { }

        public DeploymentManager(PbixProject project, AppSettings appSettings)
        {
            this.Project = project ?? throw new ArgumentNullException(nameof(project));
            this._manifests = PbiDeploymentManifest.FromProject(project);
            this.AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        }

        public PbixProject Project { get; }

        private readonly IDictionary<string, PbiDeploymentManifest> _manifests;

        /// <summary>
        /// When enabled, provides diagnostic output for a deployment without performing any of the deployment actions.
        /// </summary>
        public bool WhatIf { get; set; }

        /// <summary>
        /// Log Level for debug messages which are always logged in WhatIf mode. 
        /// </summary>
        private LogEventLevel WhatIfLogLevel => WhatIf ? LogEventLevel.Information : LogEventLevel.Debug;

        public AppSettings AppSettings { get; }

        /// <summary>
        /// (Optional) Base path, relative to which all deployment source paths are resolved.
        /// When <c>null<c>, the location of the PbixProj file is used.
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// Performs a deployment.
        /// </summary>
        public async Task DeployAsync(string profileName, string environment)
        {
            using (AppSettings.SetScopedLogLevel(WhatIf ? (LogEventLevel)Math.Min((int)LogEventLevel.Information, (int)AppSettings.LevelSwitch.MinimumLevel) : AppSettings.LevelSwitch.MinimumLevel))
            {
                if (WhatIf) Log.Information("=== Deployment WhatIf mode enabled ===");
                Log.Debug("Performing deployment from manifest at: {Path}", Project.OriginalPath);

                if (!_manifests.ContainsKey(profileName))
                    throw new DeploymentException($"The current project does not contain the specified deploymment '{profileName}'");

                var manifest = _manifests[profileName];
                switch (manifest.Mode)
                {
                    case PbiDeploymentMode.Report:
                        await DeployReportAsync(manifest, profileName, environment);
                        break;
                    case PbiDeploymentMode.Dataset:
                        await DeployDatasetAsync(manifest, profileName, environment);
                        break;
                    default:
                        throw new DeploymentException($"Unsupported deployment mode: '{manifest.Mode}'");
                }
            }

            Log.Information("Deployment completed successfully.");
        }

        /// <summary>
        /// Converts a source path expression into a <see cref="Regex"/>.
        /// Supports single wildcards <c>*</c> as well as named paramters <c>{{PARAM_NAME}}</c>. The remaining pattern is converted as-is.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static Regex ConvertSearchPatternToRegex(string pattern)
        {
            var result = new StringBuilder(pattern)
                .Replace('\\', '/')          // normalize all path separators
                .Replace("./", null, 0, 2)   // remove leading rel folder reference
                .Replace("/", "\\/")         // insert regex mask character
                .Replace(".", "\\.")         // mask dot characters
                .ToString();

            // Convert anonymous wildcards '*' -- Exposed as "MATCH_i"
            var matchId = 0;
            result = Regex.Replace(result, @"\*", m => @$"(?<MATCH_{++matchId}>[^:*?\\\/""<>|]*)");
            
            // Convert named params '{{PARAM_NAME}}'
            result = Regex.Replace(result, @"{{([^:*?\\\/""<>|]*)}}", m => @$"(?<{m.Groups[1]}>[^:*?\\\/""<>|]*)");

            Log.Debug("Converted search pattern: '{SearchPattern}' to Regex: '{Regex}'", pattern, result);

            return new Regex(result, RegexOptions.Compiled);
        }

    }

}