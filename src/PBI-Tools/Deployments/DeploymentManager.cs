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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;
using Serilog;
using Serilog.Events;
using Spectre.Console;

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

        internal Func<PbiDeploymentOAuthCredentials, IOAuthTokenProvider> PowerBITokenProviderFactory { get; set; }
            = (options) => new PowerBIServicePrincipalTokenProvider(options);


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
                    throw new DeploymentException($"The current project does not contain the specified deployment '{profileName}'");

                var manifest = _manifests[profileName];
                
                var prevConsoleWidth = AnsiConsole.Console.Profile.Width;
                if (manifest.Options.Console.Width.HasValue) {
                    AnsiConsole.Console.Profile.Width = manifest.Options.Console.Width.Value;
                }
                
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

                if (manifest.Options.Console.Width.HasValue) {
                    AnsiConsole.Console.Profile.Width = prevConsoleWidth;
                }
            }

            Log.Information("Deployment completed successfully.");
            if (WhatIf) Log.Information("=== Deployment WhatIf mode enabled ===");
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
