// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace PbiTools.Deployments
{
    public interface IPowerBITokenProvider
    {
        Task<AuthenticationResult> AcquireTokenAsync();
    }

    public class ServicePrincipalPowerBITokenProvider : IPowerBITokenProvider
    {
        public const string POWERBI_API_RESOURCE = "https://analysis.windows.net/powerbi/api";
        private static readonly string[] scopes = new [] { $"{POWERBI_API_RESOURCE}/.default" };
        private readonly IConfidentialClientApplication _app;


        public ServicePrincipalPowerBITokenProvider(PbiDeploymentAuthentication options)
        {
            var builder = ConfidentialClientApplicationBuilder
                .Create(options.ClientId)
                .WithClientSecret(options.ClientSecret);
                // TODO Support Certificate

            _app = (options.Authority != null
                ? builder.WithAuthority(options.Authority, options.ValidateAuthority)
                : builder.WithTenantId(options.TenantId)
            ).Build();
        }
        
        public Task<AuthenticationResult> AcquireTokenAsync() => _app.AcquireTokenForClient(scopes).ExecuteAsync();
    }

}