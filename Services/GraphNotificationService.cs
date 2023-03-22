using GraphNotifications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace GraphNotifications.Services
{
    public class GraphNotificationService : IGraphNotificationService
    {
        private readonly ILogger _logger;
        private readonly string _notificationUrl;
        private readonly IGraphClientService _graphClientService;
        private readonly ICertificateService _certificateService;

        public GraphNotificationService(IGraphClientService graphClientService, 
            ICertificateService certificateService, IOptions<AppSettings> settings, ILogger<GraphNotificationService> logger)
        {
            _graphClientService = graphClientService;
            _certificateService = certificateService ?? throw new ArgumentException(nameof(certificateService));
            _logger = logger;
            _notificationUrl = settings.Value.NotificationUrl ?? throw new ArgumentException(nameof(settings.Value.NotificationUrl));
        }

        public async Task<Subscription> AddSubscriptionAsync(string userAccessToken, SubscriptionDefinition subscriptionDefinition)
        {
            // Create the subscription request
            var subscription = new Subscription
            {
                ChangeType = string.Join(',', subscriptionDefinition.ChangeTypes), //"created",
                NotificationUrl = _notificationUrl,
                Resource = subscriptionDefinition.Resource, // "me/mailfolders/inbox/messages",
                ClientState = Guid.NewGuid().ToString(),
                IncludeResourceData = subscriptionDefinition.ResourceData,
                ExpirationDateTime = subscriptionDefinition.ExpirationTime
            };

            if (subscriptionDefinition.ResourceData)
            {
                // Get the encryption certificate (public key)
                var encryptionCertificate = await _certificateService.GetEncryptionCertificate();
                subscription.EncryptionCertificateId = encryptionCertificate.Subject;
                // To get resource data, we must provide a public key that
                // Microsoft Graph will use to encrypt their key
                // See https://docs.microsoft.com/graph/webhooks-with-resource-data#creating-a-subscription
                subscription.AddPublicEncryptionCertificate(encryptionCertificate);
            }

            _logger.LogInformation("Getting GraphService with accesstoken for Graph onbehalf of user");
            var graphUserClient = _graphClientService.GetUserGraphClient(userAccessToken);

            _logger.LogInformation("Create graph subscription");
            return await graphUserClient.Subscriptions.Request().AddAsync(subscription);
        }

        public async Task<Subscription> RenewSubscriptionAsync(string userAccessToken, string subscriptionId, DateTimeOffset expirationTime)
        {
            var subscription = new Subscription
            {
                ExpirationDateTime = expirationTime
            };
            
            _logger.LogInformation("Getting GraphService with accesstoken for Graph onbehalf of user");
            var graphUserClient = _graphClientService.GetUserGraphClient(userAccessToken);
            _logger.LogInformation($"Renew graph subscription: {subscriptionId}");
            return await graphUserClient.Subscriptions[subscriptionId].Request().UpdateAsync(subscription);
        }

        public async Task<Subscription> GetSubscriptionAsync(string userAccessToken, string subscriptionId)
        {
            _logger.LogInformation("Getting GraphService with accesstoken for Graph onbehalf of user");
            var graphUserClient = _graphClientService.GetUserGraphClient(userAccessToken);
            _logger.LogInformation($"Get graph subscription: {subscriptionId}");
            return await graphUserClient.Subscriptions[subscriptionId].Request().GetAsync();
        }
    }
}
