namespace GraphNotifications.Models
{
    public class AppSettings
    {
        public string TenantId { get; set; }
        
        public string NotificationUrl { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        public string KeyVaultUrl { get; set; }

        public string CertificateName { get; set; }

        public string UserAssignedClientId { get; set; }

        public string RedisConnectionString { get; set; }
        
        // Flag to set if ClientSecret of Managed Identity is used
        public bool UseClientSecretAuth { get; set; }
    }
}
