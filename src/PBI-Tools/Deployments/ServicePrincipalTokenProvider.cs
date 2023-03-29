// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace PbiTools.Deployments
{
    public interface IOAuthTokenProvider
    {
        Task<AuthenticationResult> AcquireTokenAsync();
    }

    public class ServicePrincipalTokenProvider : IOAuthTokenProvider
    {
        private readonly IConfidentialClientApplication _app;
        private readonly string[] scopes;

        public ServicePrincipalTokenProvider(PbiDeploymentOAuthCredentials options, params string[] scopes)
        {
            var builder = ConfidentialClientApplicationBuilder
                .Create(options.ClientId)
                .WithClientSecret(options.ClientSecret);
            // TODO Support Certificate

            _app = (options.Authority != null
                ? builder.WithAuthority(options.Authority, options.ValidateAuthority)
                : builder.WithTenantId(options.TenantId)
            ).Build();

            this.scopes = options.Scopes == null || options.Scopes.Length == 0
                ? scopes
                : options.Scopes;
        }

        public Task<AuthenticationResult> AcquireTokenAsync() => _app.AcquireTokenForClient(scopes).ExecuteAsync();
    }

    public class PowerBIServicePrincipalTokenProvider : ServicePrincipalTokenProvider
    {
        public const string POWERBI_API_RESOURCE = "https://analysis.windows.net/powerbi/api";

        public PowerBIServicePrincipalTokenProvider(PbiDeploymentOAuthCredentials options)
            : base(options, $"{POWERBI_API_RESOURCE}/.default")
        { }
        
    }

}