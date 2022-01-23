// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerBI.Api;

namespace PbiTools.Deployments
{
    public static class DeploymentExtensions
    {
        /// <summary>
        /// Replaces the name of each environment variable embedded in the specified string
        /// with the string equivalent of the value of the variable, then returns the resulting
        /// string.
        /// </summary>
        public static string ExpandEnv(this string input) => input == null
            ? null
            : Environment.ExpandEnvironmentVariables(input);


        /// <summary>
        /// Replaces the name of each parameter embedded in the specified string with the parameter value.
        /// Parameters are marked with double curly braces.
        /// </summary>
        public static string ExpandParameters(this string value, IDictionary<string, string> parameters) => value == null
            ? null
            : (parameters ?? new Dictionary<string, string>()).Aggregate(
                new StringBuilder(value), 
                (sb, param) => 
                {
                    sb.Replace("{{" + param.Key + "}}", param.Value);
                    return sb;
                }
            ).ToString();

        /// <summary>
        /// Assigns each deployment environment its name from the environments dictionary.
        /// </summary>
        public static PbiDeploymentManifest ExpandEnvironments(this PbiDeploymentManifest manifest)
        {
            if (manifest?.Environments != null)
            { 
                foreach (var x in manifest.Environments)
                    x.Value.Name = x.Key;
            }
            return manifest;
        }

        /// <summary>
        /// Looks up a Power BI workspace Id from its name, using a session cache first, then the Power BI API.
        /// Only workspaces accessible to the authenticated user can be resolved.
        /// </summary>
        public async static Task<Guid> ResolveWorkspaceIdAsync(this string name, IDictionary<string, Guid> cache, IPowerBIClient powerbi)
        {
            DeploymentManager.Log.Debug("Resolving workspace ID for workspace: '{Workspace}'", name);

            if (cache.ContainsKey(name))
                return cache[name];

            var apiResult = await powerbi.Groups.GetGroupsAsync(filter: $"name eq '{name}'", top: 2);

            switch (apiResult.Value.Count)
            {
                case 1:
                    var id = apiResult.Value[0].Id;
                    cache[name] = id;
                    DeploymentManager.Log.Information("Resolved workspace ID '{Id}' for workspace: '{Workspace}'", id, name);
                    return id;
                case 0:
                    throw new DeploymentException($"No Power BI workspace found matching the name '{name}'. Does the API user have the required permissions?");
                default:
                    throw new DeploymentException($"More than one Power BI workspace found matching the name '{name}'. Please specify the workspace Guid instead to avoid ambiguity.");
            }
        }

        /// <summary>
        /// Expands environment variables referenced in ClientId, ClientSecret, TenantId, or Authority, and verifies all required settings are provided.
        /// </summary>
        public static PbiDeploymentAuthentication ExpandAndValidate(this PbiDeploymentAuthentication authentication)
        {
            if (authentication == null) throw new ArgumentNullException(nameof(PbiDeploymentManifest.Authentication));

            if (authentication.Type == PbiDeploymentAuthenticationType.ServicePrincipal)
            { 
                if (authentication.ClientId == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientId));
                
                if (authentication.ClientSecret == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientSecret));
                
                if (authentication.TenantId == null && authentication.Authority == null)
                    throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.TenantId), $"Either {nameof(authentication.TenantId)} or {nameof(authentication.Authority)} must be provided. Both are blank.");
                
                if (authentication.TenantId != null && authentication.Authority != null)
                    DeploymentManager.Log.Warning($"Both {nameof(authentication.TenantId)} and {nameof(authentication.Authority)} are provided. '{nameof(authentication.TenantId)}' will be ignored.");

                return new PbiDeploymentAuthentication {
                    Authority = authentication.Authority.ExpandEnv(),
                    ClientId = authentication.ClientId.ExpandEnv(),
                    ClientSecret = authentication.ClientSecret.ExpandEnv(),
                    TenantId = authentication.TenantId.ExpandEnv(),
                    Type = authentication.Type,
                    ValidateAuthority = authentication.ValidateAuthority
                };
            }

            return authentication;
        }
    }
}