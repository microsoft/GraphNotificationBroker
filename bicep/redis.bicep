param name string

resource redis 'Microsoft.Cache/redis@2022-06-01' = {
  name: '${name}redis'
  location: resourceGroup().location
  properties: {
    enableNonSslPort: false
    publicNetworkAccess: 'Enabled'
    redisConfiguration: {
      'maxfragmentationmemory-reserved': '30'
      'maxmemory-delta': '30'
      'maxmemory-reserved': '30'
    }
    redisVersion: '6.0'
    sku: {
      capacity: 0
      family: 'C'
      name: 'Basic'
    }
  }
}
