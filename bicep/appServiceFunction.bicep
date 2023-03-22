@description('The name of the function app that you wish to create.')
param name string
param certificateName string

@description('The language worker runtime to load in the function app.')
@allowed([
  'node'
  'dotnet'
  'java'
])
param runtime string = 'dotnet'

param allowedOrigins array

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = {
  name: toLower('${name}sa')
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: '${name}-ai'
}

var functionWorkerRuntime = runtime

resource uai 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' existing = {
  name: toLower('${name}-uai')
}

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${name}-hosting'
  location: resourceGroup().location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: toLower('${name}func')
  location: resourceGroup().location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities/', uai.name )}': {}
    }
  }
  properties: {
    keyVaultReferenceIdentity: uai.id
    serverFarmId: hostingPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: allowedOrigins
        supportCredentials: true
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(name)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionWorkerRuntime
        }
        {
          name: 'AzureSignalRConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${name}kv;SecretName=AzureSignalRConnectionString)'
        }
        {
          name: 'AppSettings:TenantId'
          value: 'common' // common is for multitenant apps, otherwise use: subscription().tenantId
        }
        {
          name: 'AppSettings:NotificationUrl'
          value: 'EventHub:https://${name}kv.vault.azure.net/secrets/GraphEventHubSendConnectionString?tenantId=${subscription().tenantId}'
        }
        {
          name: 'AppSettings:ClientId'
          value: '@Microsoft.KeyVault(VaultName=${name}kv;SecretName=ClientId)'
        }
        {
          name: 'AppSettings:ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${name}kv;SecretName=ClientSecret)'
        }
        {
          name: 'AppSettings:KeyVaultUrl'
          value: 'https://${name}kv.vault.azure.net'
        }
        {
          name: 'AppSettings:CertificateName'
          value: certificateName
        }
        {
          name: 'AppSettings:EventHubName'
          value: '${name}hub'
        }
        {
          name: 'AppSettings:EventHubListenConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${name}kv;SecretName=EventHubListenConnectionString)'
        }
        {
          name: 'AppSettings:UserAssignedClientId'
          value: uai.properties.clientId
        }
        {
          name: 'AppSettings:RedisConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${name}kv;SecretName=RedisConnectionString)'
        }
        {
          name: 'WEBSITE_LOAD_USER_PROFILE' // needed for new x509 certificate
          value: '1'                        // https://github.com/MicrosoftDocs/azure-docs/issues/63530
        }
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
  }
}

output hostname string = functionApp.properties.defaultHostName
