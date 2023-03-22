// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using GraphNotifications.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace GraphNotifications.Services
{
    public class TokenValidationService : ITokenValidationService
    {
        private TokenValidationParameters? _validationParameters;
        private readonly AppSettings _settings;
        private readonly ILogger _logger;
        private static readonly Lazy<JwtSecurityTokenHandler> JwtSecurityTokenHandler = new Lazy<JwtSecurityTokenHandler>(() => new JwtSecurityTokenHandler());

        public TokenValidationService(IOptions<AppSettings> settings, ILogger<TokenValidationService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<TokenValidationResult?> ValidateTokenAsync(string token)
        {
            var validationParameters = await GetTokenValidationParametersAsync();
            if (validationParameters == null)
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                // Validate the token
                var result = tokenHandler.ValidateToken(token,
                    _validationParameters, out SecurityToken jwtToken);

                // If ValidateToken did not throw an exception, token is valid.
                return new TokenValidationResult(token, GetClaims(token));                        
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error validating bearer token");
            }

            return null;
        }

        public async Task<TokenValidationResult?> ValidateAuthorizationHeaderAsync(
            HttpRequest request)
        {
            // The incoming request should have an Authorization header
            if (request.Headers.TryGetValue("authorization", out var authValues))
            {
                var authHeader = AuthenticationHeaderValue.Parse(authValues.ToArray().First());

                // Make sure that the value is "Bearer token-value"
                if (authHeader != null &&
                    string.Compare(authHeader.Scheme, "bearer", true, CultureInfo.InvariantCulture) == 0 &&
                    !string.IsNullOrEmpty(authHeader.Parameter))
                {
                    var validationParameters = await GetTokenValidationParametersAsync();
                    if (validationParameters == null)
                    {
                        return null;
                    }

                    var tokenHandler = new JwtSecurityTokenHandler();
                    try
                    {
                        // Validate the token
                        var result = tokenHandler.ValidateToken(authHeader.Parameter,
                            _validationParameters, out SecurityToken jwtToken);

                        // If ValidateToken did not throw an exception, token is valid.
                        return new TokenValidationResult(authHeader.Parameter, GetClaims(authHeader.Parameter));                        
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Error validating bearer token");
                    }
                }
            }

            return null;
        }

        private async Task<TokenValidationParameters?> GetTokenValidationParametersAsync()
        {
            if (_validationParameters == null)
            {
                // Get tenant ID and client ID
                var tenantId = _settings.TenantId;
                var clientId = _settings.ClientId;
                if (string.IsNullOrEmpty(tenantId) ||
                    string.IsNullOrEmpty(clientId))
                {
                    _logger.LogError("Required settings missing: 'tenantId' and/or 'apiClientId'.");
                    return null;
                }

                // Load the tenant-specific OpenID config from Azure
                var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"https://login.microsoftonline.com/{tenantId}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());

                var config = await configManager.GetConfigurationAsync();

                _validationParameters = new TokenValidationParameters
                {
                    // Use signing keys retrieved from Azure
                    IssuerSigningKeys = config.SigningKeys,
                    ValidateAudience = true,
                    // Audience MUST be the app ID for the Web API or api://<app Id>. This is required for powerapp as connector will always request token for api://<app id?
                    ValidAudiences = new string[] { clientId, $"api://{clientId}" },
                    ValidateIssuer = tenantId != "common",
                    // Use the issuer retrieved from Azure
                    ValidIssuer = config.Issuer,
                    ValidateLifetime = true
                };
            }

            return _validationParameters;
        }

        private IList<Claim> GetClaims(string jwt)
        {
            if (jwt.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                jwt = jwt.Substring("Bearer ".Length).Trim();
            }
            return JwtSecurityTokenHandler.Value.ReadJwtToken(jwt).Claims.ToList();
        }
    }
}
