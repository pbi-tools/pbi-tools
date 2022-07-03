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
        public static string ExpandEnv(this string input) =>
            input == null
            ? null
            : Environment.ExpandEnvironmentVariables(input);

        /// <summary>
        /// Performs ENV expansion in all Text parameters, returns the original value for all other parameter types.
        /// </summary>
        public static DeploymentParameter ExpandEnv(this DeploymentParameter input) =>
            input.Value is string s
            ? input.CloneWithValue(Environment.ExpandEnvironmentVariables(s))
            : input;

        /// <summary>
        /// Replaces the name of each environment variable embedded in any of the dictionary values
        /// with the string equivalent of the value of the variable, then returns a new dictionary
        /// with all expanded values.
        /// </summary>
        public static IDictionary<string, DeploymentParameter> ExpandEnv(this IDictionary<string, DeploymentParameter> input) =>
            input == null
            ? new Dictionary<string, DeploymentParameter>()
            : input.ToDictionary(x => x.Key, x => x.Value.ExpandEnv(), StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Performs parameter replacement in all dictionary values, using the <c>externalParameters</c> dictionary as a source.
        /// Only <see cref="DeploymentParameter"/>s of type string are modified.
        /// </summary>
        public static IDictionary<string, DeploymentParameter> ExpandParameters(this IDictionary<string, DeploymentParameter> target,
            IDictionary<string, string> externalParameters)
        {
            foreach (var key in target.Keys.ToArray()) {
                var item = target[key];
                if (item.Value is string s && s.Contains("{{") && s.Contains("}}")) {
                    target[key] = item.CloneWithValue(s.ExpandParameters(externalParameters));
                }
            }
            return target;
        }

        /// <summary>
        /// Adds or sets the specified dictionary value.
        /// </summary>
        public static IDictionary<string, string> With(this IDictionary<string, string> dictionary, string key, string value) {
            dictionary[key] = value;
            return dictionary;
        }

        /// <summary>
        /// Replaces the name of each parameter embedded in the specified string with the parameter value.
        /// Parameters are marked with double curly braces: <c>{{PARAMETER}}</c>.
        /// </summary>
        public static string ExpandParameters(this string value, IDictionary<string, DeploymentParameter> parameters) =>
            value == null
            ? null
            : (parameters ?? new Dictionary<string, DeploymentParameter>()).Aggregate(
                new StringBuilder(value), 
                (sb, param) => 
                {
                    sb.Replace("{{" + param.Key + "}}", $"{param.Value}");
                    return sb;
                }
            ).ToString();

        /// <summary>
        /// Replaces the name of each parameter embedded in the specified string with the parameter value.
        /// Parameters are marked with double curly braces: <c>{{PARAMETER}}</c>.
        /// </summary>
        public static string ExpandParameters(this string value, IDictionary<string, string> parameters) =>
            value == null
            ? null
            : (parameters ?? new Dictionary<string, string>()).Aggregate(
                new StringBuilder(value),
                (sb, param) =>
                {
                    sb.Replace("{{" + param.Key + "}}", $"{param.Value}");
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
        /// Looks up a Power BI workspace Id from its name, optionally using a session cache first, then the Power BI API.
        /// Only workspaces accessible to the authenticated user can be resolved.
        /// </summary>
        public async static Task<Guid> ResolveWorkspaceIdAsync(this string name, IPowerBIClient powerbi, IDictionary<string, Guid> cache = null)
        {
            DeploymentManager.Log.Debug("Resolving workspace ID for workspace: '{Workspace}'", name);

            if (cache != null && cache.ContainsKey(name))
                return cache[name];

            var apiResult = await powerbi.Groups.GetGroupsAsync(filter: $"name eq '{name}'", top: 2);

            switch (apiResult.Value.Count)
            {
                case 1:
                    var id = apiResult.Value[0].Id;
                    if (cache != null) cache[name] = id;
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

        /// <summary>
        /// Gets the first item in a collection, or the default value if there is none.
        /// Allows the collection reference to be <c>null</c>.
        /// </summary>
        public static bool TryGetFirst<T>(this IEnumerable<T> collection, out T value) where T : class
        {
            value = collection?.FirstOrDefault();
            return value != default;
        }
    }
}