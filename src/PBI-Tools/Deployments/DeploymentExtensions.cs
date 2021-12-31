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
        public static string ExpandEnv(this string input) => Environment.ExpandEnvironmentVariables(input);


        public static string ExpandParameters(this string value, IDictionary<string, string> parameters) =>
            (parameters ?? new Dictionary<string, string>()).Aggregate(
                new StringBuilder(value), 
                (sb, param) => 
                {
                    sb.Replace("{{" + param.Key + "}}", param.Value);
                    return sb;
                }
            ).ToString();


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


        public static PbiDeploymentAuthentication Validate(this PbiDeploymentAuthentication authentication)
        {
            if (authentication == null) throw new ArgumentNullException(nameof(PbiDeploymentManifest.Authentication));

            if (authentication.Type == PbiDeploymentAuthenticationType.ServicePrincipal)
            { 
                if (authentication.ClientId == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientId));
                if (authentication.ClientSecret == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientSecret));
                if (authentication.TenantId == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.TenantId));
            }

            return authentication;
        }
    }
}