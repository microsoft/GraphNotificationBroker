
param name string
param apiClientId string
@secure()
param apiClientSecret string

@description('Specifies the object ID of a user, service principal or security group in the Azure Active Directory tenant for the vault. The object ID must be unique for the list of access policies. Get it by using Get-AzADUser or Get-AzADServicePrincipal cmdlets.')
param userId string


@description('Specifies whether Azure Virtual Machines are permitted to retrieve certificates stored as secrets from the key vault.')
param enabledForDeployment bool = false

@description('Specifies whether Azure Disk Encryption is permitted to retrieve secrets from the vault and unwrap keys.')
param enabledForDiskEncryption bool = false

@description('Specifies whether Azure Resource Manager is permitted to retrieve secrets from the key vault.')
param enabledForTemplateDeployment bool = false

@description('Specifies the Azure Active Directory tenant ID that should be used for authenticating requests to the key vault. Get it by using Get-AzSubscription cmdlet.')
param tenantId string = subscription().tenantId

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param skuName string = 'standard'

var keyVaultName = '${name}kv'

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-11-01' = {
  name: '${name}ns'
  location: resourceGroup().location
}

resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' existing = {
  parent: eventHubNamespace
  name: '${name}hub'
  resource eventHubSendPolicy 'authorizationRules@2021-01-01-preview' existing = {
    name: 'sendpolicy'
  }
  resource eventHubListenPolicy 'authorizationRules@2021-01-01-preview' existing = {
    name: 'listenpolicy'
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2022-02-01' existing = {
  name: '${name}signalr'
}

resource redis 'Microsoft.Cache/redis@2022-06-01' existing = {
  name: '${name}redis'
}

resource kv 'Microsoft.KeyVault/vaults@2021-11-01-preview' = {
  name: keyVaultName
  location: resourceGroup().location
  properties: {
    enabledForDeployment: enabledForDeployment
    enabledForDiskEncryption: enabledForDiskEncryption
    enabledForTemplateDeployment: enabledForTemplateDeployment
    tenantId: tenantId
    enableRbacAuthorization: true
    sku: {
      name: skuName
      family: 'A'
    }
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource eventHubSendPolicySecret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'GraphEventHubSendConnectionString'
  properties: {
    value: eventHub::eventHubSendPolicy.listKeys().primaryConnectionString
  }
}

resource appSignalRConnectionString 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'AzureSignalRConnectionString'
  properties: {
    value: signalR.listKeys().primaryConnectionString
  }
}

resource appClientId 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'ClientId'
  properties: {
    value: apiClientId
  }
}

resource appClientSecret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'ClientSecret'
  properties: {
    value: apiClientSecret
  }
}

resource appEventHubListenConnectionString 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'EventHubListenConnectionString'
  properties: {
    value: eventHub::eventHubListenPolicy.listKeys().primaryConnectionString
  }
}

resource redisConnectionString 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  parent: kv
  name: 'RedisConnectionString'
  properties: {
    value: '${redis.name}.redis.cache.windows.net:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}
