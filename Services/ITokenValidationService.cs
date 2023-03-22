// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.AspNetCore.Http;

namespace GraphNotifications.Services
{
    public interface ITokenValidationService
    {
        Task<TokenValidationResult?> ValidateAuthorizationHeaderAsync(
            HttpRequest request);

        Task<TokenValidationResult?> ValidateTokenAsync(
            string token);
    }
}
