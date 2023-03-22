
param appName string
param graphChangeTrackingSpId string
param userId string
param apiClientId string
@secure()
param apiClientSecret string
param corsUrls array
param certificateName string = 'contoso'

module redis 'redis.bicep' = {
  name: 'dp${appName}-redis'
  params: {
    name: appName
  }
}

module uai 'uai.bicep' = {
  name: 'dp${appName}-uai'
  params: {
    name: appName
    graphSpId: graphChangeTrackingSpId
    userId: userId
  }
}

module eventHub 'eventHub.bicep' = {
  name: 'dp${appName}-evh'
  params: {
    name: appName
  }
}

module applicationInsights 'applicationInsights.bicep' = {
  name: 'dp${appName}-AppInsights'
  params: {
    name: appName
  }
}

module storageAccount 'storageAccount.bicep' = {
  name: 'dp${appName}-StorageAccount'
  params: {
    name: appName
  }
}

module signalR 'signalR.bicep' = {
  name: 'dp${appName}-SignalR'
  params: {
    name: appName
    apiClientId: apiClientId
  }
}

module keyVault 'keyVault.bicep' = {
  name: 'dp${appName}-kv'
  params: {
    name: appName
    userId: userId
    apiClientId: apiClientId
    apiClientSecret: apiClientSecret
  }
  dependsOn: [
    eventHub
    signalR
    redis
  ]
}

module createCertificate 'newCertificate.ps1.bicep' = {
  name: 'dp-createCert-${appName}'
  params: {
    userAssignedIdentityName: toLower('${appName}-uai')
    VaultName: '${appName}kv'
    CertName: certificateName
    Force: false
  }
  dependsOn: [
    keyVault
  ]
}

module functionApp 'appServiceFunction.bicep' = {
  name: 'dp${appName}-FunctionApp'
  params: {
    name: appName
    certificateName: certificateName
    allowedOrigins: corsUrls
  }
  dependsOn: [
    storageAccount
    applicationInsights
    keyVault
    uai
  ]
}
