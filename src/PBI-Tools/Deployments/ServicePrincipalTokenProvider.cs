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
