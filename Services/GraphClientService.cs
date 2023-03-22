// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Azure.Identity;
using GraphNotifications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace GraphNotifications.Services
{
    public class GraphClientService : IGraphClientService
    {
        private readonly AppSettings _settings;
        private readonly ILogger _logger;

        public GraphClientService(IOptions<AppSettings> options, ILogger<GraphClientService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public GraphServiceClient GetUserGraphClient(string userAssertion)
        {
            var tenantId = _settings.TenantId;
            var clientId = _settings.ClientId;
            var clientSecret = _settings.ClientSecret;

            if (string.IsNullOrEmpty(tenantId) ||
                string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(clientSecret))
            {
                _logger.LogError("Required settings missing: 'tenantId', 'apiClientId', and 'apiClientSecret'.");
                throw new ArgumentNullException("Required settings missing: 'tenantId', 'apiClientId', and 'apiClientSecret'.");
            }

            var onBehalfOfCredential = new OnBehalfOfCredential(
                tenantId, clientId, clientSecret, userAssertion);

            return new GraphServiceClient(onBehalfOfCredential);
        }
    }
}
