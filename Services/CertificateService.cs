// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using GraphNotifications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GraphNotifications.Services
{

    /// <summary>
    /// Implements methods to retrieve certificates from Azure Key Vault
    /// </summary>
    public class CertificateService : ICertificateService
    {
        private readonly AppSettings _settings;
        private readonly ILogger _logger;
        private readonly Uri _keyVaultUrl;
        private byte[] _publicKeyBytes = null;
        private byte[] _privateKeyBytes = null;

        public CertificateService(IOptions<AppSettings> options, ILogger<CertificateService> logger)
        {
            _settings = options.Value;
            _logger = logger;
            _keyVaultUrl = !string.IsNullOrEmpty(_settings.KeyVaultUrl) ? 
                new Uri(_settings.KeyVaultUrl) : throw new ArgumentNullException(nameof(_settings.KeyVaultUrl));
        }

        /// <summary>
        /// Gets the configured public key from the Azure Key Vault
        /// </summary>
        /// <returns>The public key</returns>
        public async Task<X509Certificate2> GetEncryptionCertificate()
        {
            if (_publicKeyBytes == null)
            {
                await LoadCertificates();
            }

            return new X509Certificate2(_publicKeyBytes);
        }

        /// <summary>
        /// Gets the configure private key from the Azure Key Vault
        /// </summary>
        /// <returns>The private key</returns>
        public async Task<X509Certificate2> GetDecryptionCertificate()
        {
            if (_privateKeyBytes == null)
            {
                await LoadCertificates();
            }

            return new X509Certificate2(_privateKeyBytes);
        }

        private TokenCredential GetCredential()
        {
            // If you granted your Application access to the key vault
            // you can use clientId and Client Secret
            if (_settings.UseClientSecretAuth)
            {
                var tenantId = _settings.TenantId;
                var clientId = _settings.ClientId;
                var clientSecret = _settings.ClientSecret;

                // Authenticate as the app to connect to Azure Key Vault
                return new ClientSecretCredential(tenantId, clientId, clientSecret);
            }

            // If using user assigned managed identity
            // pass the client id of the identity
            var userAssignedClientId = _settings.UserAssignedClientId;
            if (!string.IsNullOrEmpty(userAssignedClientId))
            {
                return new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = userAssignedClientId });
            }

            // If using system assigned managed identity
            // or local development
            // Authenticate as the app to connect to Azure Key Vault
            var defaultAzureCredentialOptions = new DefaultAzureCredentialOptions();
            defaultAzureCredentialOptions.ExcludeAzureCliCredential = false;
            defaultAzureCredentialOptions.ExcludeEnvironmentCredential = true;
            defaultAzureCredentialOptions.ExcludeInteractiveBrowserCredential = true;
            defaultAzureCredentialOptions.ExcludeManagedIdentityCredential = false;
            defaultAzureCredentialOptions.ExcludeSharedTokenCacheCredential = true;
            defaultAzureCredentialOptions.ExcludeVisualStudioCodeCredential = false;
            defaultAzureCredentialOptions.ExcludeVisualStudioCredential = false;

            return new DefaultAzureCredential(defaultAzureCredentialOptions);
        }

        /// <summary>
        /// Gets the public and private keys from Azure Key Vault and caches the raw values
        /// </summary>
        private async Task LoadCertificates()
        {
            // Load configuration values
            var certificateName = _settings.CertificateName;

            var credential = GetCredential();

            // CertificateClient can get the public key
            var certClient = new CertificateClient(_keyVaultUrl, credential);
            // Secret client can get the private key
            var secretClient = new SecretClient(_keyVaultUrl, credential);

            // Get the public key
            var publicCertificate = await certClient.GetCertificateAsync(certificateName);

            // Each certificate that has a private key in Azure Key Vault has a corresponding
            // secret ID. Use this to get the private key
            var privateCertificate = await secretClient.GetSecretAsync(ParseSecretName(publicCertificate.Value.SecretId));

            _publicKeyBytes = publicCertificate.Value.Cer;
            _privateKeyBytes = Convert.FromBase64String(privateCertificate.Value.Value);
        }

        /// <summary>
        /// Extract the secret name from the secret ID
        /// </summary>
        /// <param name="secretId">The URI to the secret</param>
        /// <returns>The secret name</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static string ParseSecretName(Uri secretId)
        {
            // Secret IDs are URIs. The name is in the
            // third segment
            if (secretId.Segments.Length < 3)
            {
                throw new InvalidOperationException($@"The secret ""{secretId}"" does not contain a valid name.");
            }

            return secretId.Segments[2].TrimEnd('/');
        }
    }
}
