namespace GraphNotifications.Models
{
    /// <summary>
    /// Subscription record saved in subscription store
    /// </summary>
    public class SubscriptionRecord : SubscriptionDefinition
    {
        /// <summary>
        /// The ID of the graph subscription
        /// </summary>
        public string SubscriptionId { get; set; }
    }
}
