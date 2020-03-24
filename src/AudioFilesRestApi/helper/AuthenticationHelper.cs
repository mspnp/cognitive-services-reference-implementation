// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace AudioFilesRestApi.helper
{
    sealed class AuthenticationHelper
    {
        public static async Task<string> GetAccessToken(IConfiguration config)
        {
            var authContext = new AuthenticationContext($"https://login.windows.net/{config["UserDelegation:TenantId"]}");
            var credential = new ClientCredential(config["UserDelegation:ClientId"], config["UserDelegation:ClientSecret"]);
            var result = await authContext.AcquireTokenAsync("https://storage.azure.com", credential);

            if (result == null)
            {
                throw new Exception("Failed to authenticate via ADAL");
            }

            return result.AccessToken;
        }

        public static TokenCredential GetTokenCredential(IConfiguration config)
        {
            return new ClientSecretCredential(config["UserDelegation:TenantId"], config["UserDelegation:ClientId"], config["UserDelegation:ClientSecret"], new TokenCredentialOptions());
        }
    }
}