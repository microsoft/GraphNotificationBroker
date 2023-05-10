using Microsoft.Extensions.Logging;
using GraphNotifications.Services;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using GraphNotifications.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Graph;
using System.Net;
using Microsoft.Extensions.Options;
using Azure.Messaging.EventHubs;
using System.Text;
using Newtonsoft.Json;

namespace GraphNotifications.Functions
{
    public class GraphNotificationsHub : ServerlessHub
    {
        private readonly ITokenValidationService _tokenValidationService;
        private readonly IGraphNotificationService _graphNotificationService;
        private readonly ICertificateService _certificateService;
        private readonly ICacheService _cacheService;
        private readonly ILogger _logger;
        private readonly AppSettings _settings;
        private const string MicrosoftGraphChangeTrackingSpId = "0bf30f3b-4a52-48df-9a82-234910c4a086";

        public GraphNotificationsHub(
            ITokenValidationService tokenValidationService,
            IGraphNotificationService graphNotificationService,
            ICacheService cacheService,
            ICertificateService certificateService,
            ILogger<GraphNotificationsHub> logger,
            IOptions<AppSettings> options)
        {
            _tokenValidationService = tokenValidationService;
            _graphNotificationService = graphNotificationService;
            _certificateService = certificateService ?? throw new ArgumentException(nameof(certificateService));
            _cacheService = cacheService ?? throw new ArgumentException(nameof(cacheService));
            _logger = logger;
            _settings = options.Value;
        }

        [FunctionName("negotiate")]
        public async Task<SignalRConnectionInfo?> Negotiate([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/negotiate")] HttpRequest req)
        {
            try
            {
                // Validate the bearer token
                var validationTokenResult = await _tokenValidationService.ValidateAuthorizationHeaderAsync(req);
                if (validationTokenResult == null || string.IsNullOrEmpty(validationTokenResult.UserId))
                {
                    // If token wasn't returned it isn't valid
                    return null;
                }

                return Negotiate(validationTokenResult.UserId, validationTokenResult.Claims);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered an error in negotiate");
                return null;
            }
        }

        [FunctionName("CreateSubscription")]
        public async Task CreateSubscription([SignalRTrigger]InvocationContext invocationContext, SubscriptionDefinition subscriptionDefinition, string accessToken)
        {
            try
            {
                // Validate the token
                var tokenValidationResult = await _tokenValidationService.ValidateTokenAsync(accessToken);
                if (tokenValidationResult == null)
                {
                    // This request is Unauthorized
                    await Clients.Client(invocationContext.ConnectionId).SendAsync("Unauthorized", "The request is unauthorized to create the subscription");
                    return;
                }

                if (subscriptionDefinition == null)
                {
                    _logger.LogError("No subscription definition supplied.");
                    await Clients.Client(invocationContext.ConnectionId).SendAsync("SubscriptionCreationFailed", subscriptionDefinition);
                    return;
                }

                _logger.LogInformation($"Creating subscription from {invocationContext.ConnectionId} for resource {subscriptionDefinition.Resource}.");

                // When notification events are received we get the Graph Subscription Id as the identifier
                // Adding a cache entry with the logicalCacheKey as the Graph Subscription Id, we can
                // fetch the subscription from the cache
                
                // The logicalCacheKey is key we can build when we create a subscription and when we receive a notification
                var logicalCacheKey = $"{subscriptionDefinition.Resource}_{tokenValidationResult.UserId}";
                _logger.LogInformation($"logicalCacheKey: {logicalCacheKey}");
                var subscriptionId = await _cacheService.GetAsync<string>(logicalCacheKey);
                _logger.LogInformation($"subscriptionId: {subscriptionId ?? "null"}");

                SubscriptionRecord? subscription = null;
                if (!string.IsNullOrEmpty(subscriptionId))
                {
                    // Check if subscription on the resource for this user already exist
                    // If subscription on the resource for this user already exist, add the signalR connection to the SignalR group
                    subscription = await _cacheService.GetAsync<SubscriptionRecord>(subscriptionId);
                }
                
                if (subscription == null)
                {
                    _logger.LogInformation($"Supscription not found in the cache");
                    // if subscription on the resource for this user does not exist create it
                    subscription = await this.CreateGraphSubscription(tokenValidationResult, subscriptionDefinition);
                    _logger.LogInformation($"SubscriptionId: {subscription.SubscriptionId}");
                }
                else
                {
                    var subscriptionTimeBeforeExpiring = subscription.ExpirationTime.Subtract(DateTimeOffset.UtcNow);
                    _logger.LogInformation($"Supscription found in the cache");
                    _logger.LogInformation($"Supscription ExpirationTime: {subscription.ExpirationTime}");
                    _logger.LogInformation($"subscriptionTimeBeforeExpiring: {subscriptionTimeBeforeExpiring.ToString(@"hh\:mm\:ss")}");
                    _logger.LogInformation($"Supscription ExpirationTime: {subscriptionTimeBeforeExpiring.TotalSeconds}");
                    // If the subscription will expire in less than 5 minutes, renew the subscription ahead of time
                    if (subscriptionTimeBeforeExpiring.TotalSeconds < (5 * 60))
                    {
                        _logger.LogInformation($"Less than 5 minutes before renewal");
                        try
                        {
                            // Renew the current subscription
                            subscription = await this.RenewGraphSubscription(tokenValidationResult.Token, subscription, subscriptionDefinition.ExpirationTime);
                            _logger.LogInformation($"SubscriptionId: {subscription.SubscriptionId}");
                        }
                        catch (Exception ex)
                        {
                            // There was a subscription in the cache, but we were unable to renew it
                            _logger.LogError(ex, $"Encountered an error renewing subscription: {JsonConvert.SerializeObject(subscription)}");

                            // Let's check if there is an existing subscription
                            var graphSubscription = await this.GetGraphSubscription(tokenValidationResult.Token, subscription);
                            if (graphSubscription == null)
                            {
                                _logger.LogInformation($"Subscription does not exist. Removing cache entries for subscriptionId: {subscription.SubscriptionId}");
                                // Remove the logicalCacheKey refering to the graph Subscription Id from cache
                                await _cacheService.DeleteAsync(logicalCacheKey);

                                // Remove the old Graph Subscription Id
                                await _cacheService.DeleteAsync(subscription.SubscriptionId);

                                // We should try to create a new subscription for the following reasons:
                                // A subscription was found in the cache, but the renew subscription failed
                                // After the renewal failed, we still couldn't find that subscription in Graph 
                                // Create a new subscription
                                subscription = await this.CreateGraphSubscription(tokenValidationResult, subscriptionDefinition);
                            } else {
                                // If the renew failed, but we found a subscription
                                // return it
                                subscription = graphSubscription;
                            }
                        }
                    }
                }

                var expirationTimeSpan = subscription.ExpirationTime.Subtract(DateTimeOffset.UtcNow);
                _logger.LogInformation($"expirationTimeSpan: {expirationTimeSpan.ToString(@"hh\:mm\:ss")}");

                // Add connection to the Signal Group for this subscription.
                _logger.LogInformation($"Adding connection to SignalR Group");
                await Groups.AddToGroupAsync(invocationContext.ConnectionId, subscription.SubscriptionId);

                // Add or update the logicalCacheKey with the subscriptionId
                _logger.LogInformation($"Add or update the logicalCacheKey: {logicalCacheKey} with the subscriptionId: {subscription.SubscriptionId}.");
                await _cacheService.AddAsync<string>(logicalCacheKey, subscription.SubscriptionId, expirationTimeSpan);

                // Add or update the cache with updated subscription
                _logger.LogInformation($"Adding subscription to cache");
                await _cacheService.AddAsync<SubscriptionRecord>(subscription.SubscriptionId, subscription, expirationTimeSpan);

                await Clients.Client(invocationContext.ConnectionId).SendAsync("SubscriptionCreated", subscription);
                _logger.LogInformation($"Subscription was created successfully with connectionId {invocationContext.ConnectionId}");
                return;
            }
            catch(Exception ex)
            {
                 _logger.LogError(ex, "Encountered an error when creating a subscription");
                 await Clients.Client(invocationContext.ConnectionId).SendAsync("SubscriptionCreationFailed", subscriptionDefinition);
            }
        }

        [FunctionName("EventHubProcessor")]
        public async Task Run([EventHubTrigger("%AppSettings:EventHubName%", Connection = "AppSettings:EventHubListenConnectionString")] EventData[] events)
        {
            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
                    
                    // Replace these two lines with your processing logic.
                    _logger.LogWarning($"C# Event Hub trigger function processed a message: {messageBody}");

                    // Deserializing to ChangeNotificationCollection throws an error when the validation requests
                    // are sent. Using a JObject to get around this issue.
                    var notificationsObj = Newtonsoft.Json.Linq.JObject.Parse(messageBody);
                    var notifications = notificationsObj["value"];
                    if (notifications == null)
                    {
                        _logger.LogWarning($"No notifications found");;
                        return;
                    }

                    foreach (var notification in notifications)
                    {

                        var subscriptionId = notification["subscriptionId"]?.ToString();
                        if (string.IsNullOrEmpty(subscriptionId))
                        {
                            _logger.LogWarning($"Notification subscriptionId is null");
                            continue;
                        }

                        // if this is a validation request, the subscription Id will be NA
                        if (subscriptionId.ToLower() == "na")
                            continue;

                        var decryptedContent = String.Empty;
                        if(notification["encryptedContent"] != null)
                        {
                            var encryptedContentJson = notification["encryptedContent"]?.ToString();
                            var encryptedContent = Newtonsoft.Json.JsonConvert.DeserializeObject<ChangeNotificationEncryptedContent>(encryptedContentJson) ;
                            decryptedContent = await encryptedContent.DecryptAsync((id, thumbprint) => {
                                return _certificateService.GetDecryptionCertificate();
                            });
                        }

                        // A user can have multiple connections to the same resource / subscription. All need to be notified.
                        await Clients.Group(subscriptionId).SendAsync("NewMessage", notification, decryptedContent);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered an error processing the event");
                }
            }
        }

        [FunctionName(nameof(GetChatMessageFromNotification))]
        public async Task<HttpResponseMessage> GetChatMessageFromNotification(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/GetChatMessageFromNotification")] HttpRequest req)
        {
            var response = new HttpResponseMessage();
            try
            {
                _logger.LogInformation("GetChatMessageFromNotification function triggered.");
                
                var access_token = GetAccessToken(req) ?? "";
                // Validate the  token
                var validationTokenResult = await _tokenValidationService
                    .ValidateTokenAsync(access_token);
                if (validationTokenResult == null)
                {
                    response.StatusCode = HttpStatusCode.Unauthorized;
                    return response;
                }
                
                string requestBody = string.Empty;
                using (var streamReader = new StreamReader(req.Body))
                {
                    requestBody = await streamReader.ReadToEndAsync();
                }

                var encryptedContent = JsonConvert.DeserializeObject<ChangeNotificationEncryptedContent>(requestBody);             

                if(encryptedContent == null)
                {
                    response.StatusCode = HttpStatusCode.InternalServerError;
                    response.Content = new StringContent("Notification does not have right format.");
                    return response;
                }

                _logger.LogInformation($"Decrypting content of type {encryptedContent.ODataType}");

                // Decrypt the encrypted payload using private key
                var decryptedContent = await encryptedContent.DecryptAsync((id, thumbprint) => {
                    return _certificateService.GetDecryptionCertificate();
                });

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("ContentType", "application/json");
                response.Content = new StringContent(decryptedContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error decrypting");
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            
            return response;
        }

        private string? GetAccessToken(HttpRequest req)
        {
            if (req!.Headers.TryGetValue("Authorization", out var authHeader)) {
                string[] parts = authHeader.First().Split(null) ?? new string[0];
                if (parts.Length == 2 && parts[0].Equals("Bearer"))
                    return parts[1];
            }
            return null;
        }

        private async Task<Subscription?> GetGraphSubscription2(string accessToken, string subscriptionId)
        {
            _logger.LogInformation($"Fetching subscription");

            try
            {
                return await _graphNotificationService.GetSubscriptionAsync(accessToken, subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get graph subscriptionId: {subscriptionId}");
            }

            return null;
        }

        private async Task<SubscriptionRecord?> GetGraphSubscription(string accessToken, SubscriptionRecord subscription)
        {
            _logger.LogInformation($"Fetching subscription");

            try
            {
                var graphSubscription = await _graphNotificationService.GetSubscriptionAsync(accessToken, subscription.SubscriptionId);
                if (!graphSubscription.ExpirationDateTime.HasValue)
                {
                    _logger.LogError("Graph Subscription does not have an expiration date");
                    throw new Exception("Graph Subscription does not have an expiration date");
                }
                
                return new SubscriptionRecord
                {
                    SubscriptionId = graphSubscription.Id,
                    Resource = subscription.Resource,
                    ExpirationTime = graphSubscription.ExpirationDateTime.Value,
                    ResourceData = subscription.ResourceData,
                    ChangeTypes = subscription.ChangeTypes 
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get graph subscriptionId: {subscription.SubscriptionId}");
            }

            return null;
        }

        private async Task<SubscriptionRecord> RenewGraphSubscription(string accessToken, SubscriptionRecord subscription, DateTimeOffset expirationTime)
        {
            _logger.LogInformation($"Renewing subscription");

            // Renew the current graph subscription, passing the new expiration time sent from the client
            var graphSubscription = await _graphNotificationService.RenewSubscriptionAsync(accessToken, subscription.SubscriptionId, expirationTime);

            if (!graphSubscription.ExpirationDateTime.HasValue)
            {
                _logger.LogError("Graph Subscription does not have an expiration date");
                throw new Exception("Graph Subscription does not have an expiration date");
            }

            // Update subscription with renewed graph subscription data
            subscription.SubscriptionId = graphSubscription.Id;
            // If the graph subscription returns a null expiration time
            subscription.ExpirationTime = graphSubscription.ExpirationDateTime.Value;

            return subscription;
        }

        private async Task<SubscriptionRecord> CreateGraphSubscription(TokenValidationResult tokenValidationResult, SubscriptionDefinition subscriptionDefinition)
        {
            _logger.LogInformation("Creating Subscription");
            // if subscription on the resource for this user does not exist create it
            var graphSubscription = await _graphNotificationService.AddSubscriptionAsync(tokenValidationResult.Token, subscriptionDefinition);

            if (!graphSubscription.ExpirationDateTime.HasValue)
            {
                _logger.LogError("Graph Subscription does not have an expiration date");
                throw new Exception("Graph Subscription does not have an expiration date");
            }

            _logger.LogInformation("Subscription Created");
            // Create a new subscription
            return new SubscriptionRecord
            {
                SubscriptionId = graphSubscription.Id,
                Resource = subscriptionDefinition.Resource,
                ExpirationTime = graphSubscription.ExpirationDateTime.Value,
                ResourceData = subscriptionDefinition.ResourceData,
                ChangeTypes = subscriptionDefinition.ChangeTypes 
            };
        }
    }
}
