param name string
param apiClientId string

@description('The pricing tier of the SignalR resource.')
@allowed([
  'Free_F1'
  'Standard_S1'
  'Premium_P1'
])
param pricingTier string = 'Free_F1'

@description('The number of SignalR Unit.')
@allowed([
  1
  2
  5
  10
  20
  50
  100
])
param capacity int = 1

@description('Visit https://github.com/Azure/azure-signalr/blob/dev/docs/faq.md#service-mode to understand SignalR Service Mode.')
@allowed([
  'Default'
  'Serverless'
  'Classic'
])
param serviceMode string = 'Serverless'

param enableConnectivityLogs bool = true

param enableMessagingLogs bool = true

param enableLiveTrace bool = true

param allowedOrigins array

resource functionApp 'Microsoft.Web/sites@2022-03-01' existing = {
  name: toLower('${name}func')
}

var signalRExtensionKey = listkeys('${functionApp.id}/host/default', functionApp.apiVersion).systemKeys.signalr_extension

resource signalR 'Microsoft.SignalRService/signalR@2022-02-01' = {
  name: '${name}signalr'
  location: resourceGroup().location
  sku: {
    capacity: capacity
    name: pricingTier
  }
  kind: 'SignalR'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    tls: {
      clientCertEnabled: false
    }
    features: [
      {
        flag: 'ServiceMode'
        value: serviceMode
      }
      {
        flag: 'EnableConnectivityLogs'
        value: string(enableConnectivityLogs)
      }
      {
        flag: 'EnableMessagingLogs'
        value: string(enableMessagingLogs)
      }
      {
        flag: 'EnableLiveTrace'
        value: string(enableLiveTrace)
      }
    ]
    cors: {
      allowedOrigins: allowedOrigins
    }
    liveTraceConfiguration: {
      categories: [
        {
          enabled: 'true'
          name: 'ConnectivityLogs'
        }
        {
          enabled: 'true'
          name: 'MessagingLogs'
        }
        {
          enabled: 'true'
          name: 'HttpRequestLogs'
        }
      ]
      enabled: 'true'
    }
    upstream: {
      templates: [
        {
          categoryPattern: '*'
          eventPattern: '*'
          hubPattern: '*'
          urlTemplate: 'https://${functionApp.properties.defaultHostName}/runtime/webhooks/signalr?code=${signalRExtensionKey}'
          auth: {
              type: 'ManagedIdentity'
              managedIdentity: {
                resource: apiClientId
              }
          }
        }
      ]
    }
  }
}
