using Microsoft.Graph;
using GraphNotifications.Models;

namespace GraphNotifications.Services
{
    public interface IGraphNotificationService
    {
        Task<Subscription> GetSubscriptionAsync(string userAccessToken, string subscriptionId);
        Task<Subscription> AddSubscriptionAsync(string userAccessToken, SubscriptionDefinition subscriptionDefinition);
        Task<Subscription> RenewSubscriptionAsync(string userAccessToken, string subscriptionId, DateTimeOffset expirationTime);
    }
}
