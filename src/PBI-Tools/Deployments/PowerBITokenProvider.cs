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
        private static string[] scopes = new [] { $"{POWERBI_API_RESOURCE}/.default" };
        private readonly IConfidentialClientApplication _app;


        public ServicePrincipalPowerBITokenProvider(string clientId, string clientSecret, Uri authority)
        {
            _app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();            
        }
        
        public Task<AuthenticationResult> AcquireTokenAsync() => _app.AcquireTokenForClient(scopes).ExecuteAsync();
    }

}