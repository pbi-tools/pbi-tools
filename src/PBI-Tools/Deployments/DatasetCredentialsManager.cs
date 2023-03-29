// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Serilog;

namespace PbiTools.Deployments
{
    using Utils;

    public class DatasetCredentialsManager
    { 
        private static readonly ILogger Log = Serilog.Log.ForContext<DatasetCredentialsManager>();

        private readonly PbiDeploymentCredential[] _credentials;
        private readonly IPowerBIClient _powerBI;
        private readonly bool _refreshEnabled;
        private readonly AuthenticationResult _deploymentToken;

        public DatasetCredentialsManager(PbiDeploymentManifest manifest, IPowerBIClient powerBIClient, bool refreshEnabled, AuthenticationResult deploymentToken)
        {
            _credentials = (manifest ?? throw new ArgumentNullException(nameof(manifest))).Credentials;
            _powerBI = powerBIClient ?? throw new ArgumentNullException(nameof(powerBIClient));
            _refreshEnabled = refreshEnabled;
            _deploymentToken = deploymentToken;
            
            Enabled = manifest.Options.Dataset.SetCredentials;
        }

        public bool WhatIf { get; set; }

        public bool Enabled { get; }

        public async Task SetCredentialsAsync(Guid workspaceId, string datasetId)
        {
            if (!Enabled || WhatIf) return;

            var datasources = await _powerBI.Datasets.GetDatasourcesInGroupAsync(workspaceId, datasetId);

            foreach (var credential in _credentials.DefaultIfEmpty())
            {
                // TODO Debug log

                // Find Match
                if (TryGetMatch(credential.Match, datasources.Value, out var result)) { 

                    Log.Information("Matched remote datasource {Type}: {ConnectionDetails} ({DatasourceId})",
                        result.DatasourceType,
                        result.ConnectionDetails.ToJsonString(Newtonsoft.Json.Formatting.None), 
                        result.DatasourceId);

                    // Check UpdateMode
                    if (credential.UpdateMode == PbiDeploymentCredential.CredentialUpdateMode.Never)
                    {
                        Log.Information("- Skipping datasource because UpdateMode is set to 'Never'.");
                        continue;
                    }

                    var gatewayDatasource = await _powerBI.Gateways.GetDatasourceAsync(result.GatewayId.Value, result.DatasourceId.Value);

                    if (credential.UpdateMode == PbiDeploymentCredential.CredentialUpdateMode.NotSpecified
                        && gatewayDatasource.CredentialType != "NotSpecified")
                    {
                        Log.Information("- Skipping datasource because UpdateMode is set to 'NotSpecified' and credential type is {CredentialType}."
                            , gatewayDatasource.CredentialType);
                        continue;
                    }

                    if (credential.UpdateMode == PbiDeploymentCredential.CredentialUpdateMode.BeforeRefresh && !_refreshEnabled) {
                        Log.Information("- Skipping datasource because UpdateMode is set to 'BeforeRefresh' and refresh is disabled.");
                        continue;
                    }

                    var credentialDetails = credential.Type switch
                    {
                        var type when type == CredentialType.Basic     => GetBasicCredentials(credential),
                        var type when type == CredentialType.Anonymous => GetAnonymousCredentials(credential),
                        var type when type == CredentialType.OAuth2    => await GetOAuthCredentials(credential),
                        _ => throw new NotSupportedException($"CredentialType {credential.Type} is not supported.")
                    };

                    // TODO Grab DM_ErrorDetailNameCode_UnderlyingErrorMessage from result if error
                    await _powerBI.Gateways.UpdateDatasourceAsync(
                        result.GatewayId.Value,
                        result.DatasourceId.Value,
                        new() { CredentialDetails = credentialDetails }
                    );

                    Log.Information("Successfully set credentials for datasource {Type}: {ConnectionDetails}",
                        credential.Type,
                        credential.Match.ConnectionDetails.ToJsonString(Newtonsoft.Json.Formatting.None));

                }
            }

        }

        private static CredentialDetails GetBasicCredentials(PbiDeploymentCredential credential) =>
            new (new BasicCredentials(
                    credential.Username.ExpandEnv(),
                    credential.Password.ExpandEnv()
                )
                , credential.Details.PrivacyLevel
                , credential.Details.EncryptedConnection
            )
            {
                EncryptionAlgorithm = credential.Details.EncryptionAlgorithm,
                UseCallerAADIdentity = credential.Details.UseCallerAADIdentity,
                UseEndUserOAuth2Credentials = credential.Details.UseEndUserOAuth2Credentials
            };

        private static CredentialDetails GetAnonymousCredentials(PbiDeploymentCredential credential) =>
            new (new AnonymousCredentials()
                , credential.Details.PrivacyLevel
                , EncryptedConnection.NotEncrypted
            );

        private async Task<CredentialDetails> GetOAuthCredentials(PbiDeploymentCredential credential)
        {
            var token = _deploymentToken;

            if (!credential.UseDeploymentToken)
            {
                var tokenProvider = new ServicePrincipalTokenProvider(credential.ExpandAndValidate());
                token = await tokenProvider.AcquireTokenAsync();

                Log.Information("Received AccessToken from endpoint: {TokenEndpoint}; Expires On: {ExpiresOn}"
                    , token.AuthenticationResultMetadata.TokenEndpoint
                    , token.ExpiresOn);
            }

            return new CredentialDetails(
                new OAuth2Credentials(token.AccessToken)
                , credential.Details.PrivacyLevel
                , credential.Details.EncryptedConnection
            )
            {
                EncryptionAlgorithm = credential.Details.EncryptionAlgorithm,
            };
        }

        private static PropertyInfo[] ConnectionDetailsProperties = typeof(DatasourceConnectionDetails).GetProperties();

        internal static bool TryGetMatch(PbiDeploymentCredential.CredentialMatch match, IEnumerable<Datasource> dataSources, out Datasource matchedDataSource)
        {
            var matchValues = ConnectionDetailsProperties.ToDictionary(
                x => x.Name,
                y => (y.GetValue(match.ConnectionDetails) switch {
                    string s => s,
                    null => "",
                    var o => o.ToString()
                }).ToLowerInvariant()
            );

            foreach (var dataSource in dataSources)
            {
                if (dataSource.DatasourceType.Equals(match.DatasourceType, StringComparison.InvariantCultureIgnoreCase))
                { 
                    var dataSourceValues = ConnectionDetailsProperties.ToDictionary(
                        x => x.Name, 
                        y => (y.GetValue(dataSource.ConnectionDetails) switch {
                            string s => s,
                            null => "",
                            var o => o.ToString()
                        }).ToLowerInvariant()
                    );

                    var mismatches = matchValues.Except(dataSourceValues);
                    if (mismatches.Count() == 0) {
                        matchedDataSource = dataSource;
                        return true;
                    }
                }
            }

            matchedDataSource = default;
            return false;
        }

    }

}