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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using TOM = Microsoft.AnalysisServices.Tabular;

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
            ? new Dictionary<string, DeploymentParameter>(StringComparer.InvariantCultureIgnoreCase)
            : input.ToDictionary(
                x => x.Key,
                x => x.Value.ExpandEnv(), StringComparer.InvariantCultureIgnoreCase);

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
        /// Performs parameter replacement in all dictionary values, using the <c>externalParameters</c> dictionary as a source.
        /// Only <see cref="DeploymentParameter"/>s of type string are modified.
        /// </summary>
        public static IDictionary<string, DeploymentParameter> ExpandParameters(this IDictionary<string, DeploymentParameter> target,
            IDictionary<string, DeploymentParameter> externalParameters)
        {
            foreach (var key in target.Keys.ToArray())
            {
                var item = target[key];
                if (item.Value is string s && s.Contains("{{") && s.Contains("}}"))
                {
                    target[key] = item.CloneWithValue(s.ExpandParameters(externalParameters));
                }
            }
            return target;
        }

        /// <summary>
        /// Adds or sets the specified dictionary value.
        /// </summary>
        public static T With<T>(this T dictionary, string key, string value)
            where T : IDictionary<string, string>
        {
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
                    sb.Replace("{{" + param.Key + "}}", param.Value);
                    return sb;
                }
            ).ToString();

        /// <summary>
        /// Expands parameters within the string first, then environment variables.
        /// </summary>
        public static string ExpandParamsAndEnv(this string value, IDictionary<string, DeploymentParameter> parameters) =>
            value.ExpandParameters(parameters).ExpandEnv();

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
        /// Looks up a Power BI workspace from a manifest workspace reference,
        /// optionally using a session cache first, then the Power BI API.
        /// Only workspaces accessible to the authenticated user can be resolved.
        /// </summary>
        public static async Task<Group> ResolveWorkspaceAsync(this string workspaceRef, IPowerBIClient powerBI, IDictionary<string, (Group, Capacity)> cache = null)
        {
            DeploymentManager.Log.Debug("Resolving details for workspace ref: '{Workspace}'", workspaceRef);

            if (cache != null && cache.TryGetValue(workspaceRef, out var cached) && cached.Item1 is { } cachedWorkspace)
                return cachedWorkspace;

            if (Guid.TryParse(workspaceRef, out var id))
            { 
                var workspace = await powerBI.Groups.GetGroupAsync(id);
                if (cache != null) cache[workspaceRef] = (workspace, null);
                DeploymentManager.Log.Information("Resolved workspace: '{Workspace}'", workspaceRef);
                return workspace;
            }

            var apiResult = await powerBI.Groups.GetGroupsAsync(filter: $"name eq '{workspaceRef}'", top: 2);

            switch (apiResult.Value.Count)
            {
                case 1:
                    var workspace = apiResult.Value[0];
                    if (cache != null) cache[workspaceRef] = (workspace, null);
                    DeploymentManager.Log.Information("Resolved workspace ID '{Id}' for workspace: '{Workspace}'", workspace.Id, workspaceRef);
                    return workspace;
                case 0:
                    throw new DeploymentException($"No Power BI workspace found matching the name '{workspaceRef}'. Does the API user have the required permissions?");
                default:
                    throw new DeploymentException($"More than one Power BI workspace found matching the name '{workspaceRef}'. Please specify the workspace Guid instead to avoid ambiguity.");
            }
        }

        /// <summary>
        /// Looks up the Power BI capacity for the given workspace name, optionally using a session cache first, then the Power BI API.
        /// Only capacities accessible to the authenticated user can be resolved.
        /// </summary>
        public static async Task<Capacity> ResolveCapacityAsync(this Group workspace, IPowerBIClient powerBI, IDictionary<string, (Group, Capacity)> cache = null)
        {
            DeploymentManager.Log.Debug("Resolving capacity for workspace: '{Workspace}'", workspace.Name);

            // Workspace is cached
            if (cache != null && cache.TryGetValue(workspace.Name, out var cached))
            { 
                if (cached.Item2 is { } cachedCapacity)
                    return cachedCapacity;
                
                if (cached.Item1 is { } cachedWorkspace && cachedWorkspace.IsOnDedicatedCapacity != true)
                    return default;
            }

            if (workspace.IsOnDedicatedCapacity == true)
            {
                DeploymentManager.Log.Debug("Retrieving all capacities...");
                var capacities = await powerBI.Capacities.GetCapacitiesAsync();
                if (capacities.Value.FirstOrDefault(c => c.Id == workspace.CapacityId) is { } capacity)
                {
                    if (cache != null) cache[workspace.Name] = (workspace, capacity);
                    DeploymentManager.Log.Information("Resolved capacity '{Capacity}' for workspace: '{Workspace}'", capacity.DisplayName, workspace.Name);
                    return capacity;
                }
                else
                {
                    DeploymentManager.Log.Warning("Capacity with ID '{CapacityId}' not found.", workspace.CapacityId);
                }
            }

            var empty = new Capacity { };
            if (cache != null) cache[workspace.Name] = (workspace, empty);
            return empty;
        }

        /// <summary>
        /// Expands environment variables referenced in ClientId, ClientSecret, TenantId, or Authority, and verifies all required settings are provided.
        /// </summary>
        public static PbiDeploymentOAuthCredentials ExpandAndValidate(this PbiDeploymentOAuthCredentials credentials)
        {
            // ReSharper disable once SuspiciousParameterNameInArgumentNullException
            if (credentials == null) throw new ArgumentNullException(nameof(PbiDeploymentManifest.Authentication));

            if (credentials.ClientId == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientId));

            if (credentials.ClientSecret == null) throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.ClientSecret));

            if (credentials.TenantId == null && credentials.Authority == null)
                throw new ArgumentNullException(nameof(PbiDeploymentAuthentication.TenantId), $"Either {nameof(credentials.TenantId)} or {nameof(credentials.Authority)} must be provided. Both are blank.");

            if (credentials.TenantId != null && credentials.Authority != null)
                DeploymentManager.Log.Warning($"Both {nameof(credentials.TenantId)} and {nameof(credentials.Authority)} are provided. '{nameof(credentials.TenantId)}' will be ignored.");

            return new PbiDeploymentOAuthCredentials
            {
                Authority = credentials.Authority.ExpandEnv(),
                ClientId = credentials.ClientId.ExpandEnv(),
                ClientSecret = credentials.ClientSecret.ExpandEnv(),
                TenantId = credentials.TenantId.ExpandEnv(),
                ValidateAuthority = credentials.ValidateAuthority,
                Scopes = credentials.Scopes
            };
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

        /// <summary>
        /// Converts a <see cref="DatasetRefreshType"/> value to the corresponding <see cref="TOM.RefreshType"/>.
        /// </summary>
        public static TOM.RefreshType ConvertToTOM(this DatasetRefreshType apiRefreshType) =>
            (TOM.RefreshType)Enum.Parse(typeof(TOM.RefreshType), $"{apiRefreshType}");
    }
}
