param VaultName string
param CertName string = 'contoso'
param SubjectName string = 'CN=contoso.com'
param DnsNames array = [
  'contoso.com'
]
param Force bool = false
param userAssignedIdentityName string
param now string = utcNow('F')
var boolstring = Force == false ? '$false' : '$true'
resource newCertwithRotationKV 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'newCert-${CertName}'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', userAssignedIdentityName)}': {}
    }
  }
  location: resourceGroup().location
  kind: 'AzurePowerShell'
  properties: {
    azPowerShellVersion: '9.0.0'
    arguments: ' -VaultName ${VaultName} -CertName ${CertName} -SubjectName ${SubjectName} -Force ${boolstring} -DnsNames ${join(DnsNames,'_')}'
    scriptContent: loadTextContent('./scripts/Create-KVCertificate.ps1')
    forceUpdateTag: now
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
    timeout: 'PT8M'
  }
}
output VaultNameOut string = newCertwithRotationKV.properties.outputs.VaultName
output CertNameOut string = newCertwithRotationKV.properties.outputs.CertName
output ThumbprintOut string = newCertwithRotationKV.properties.outputs.Thumbprint
output CertEnabledOut bool = newCertwithRotationKV.properties.outputs.CertEnabled
output RenewAtPercentageLifetime int = newCertwithRotationKV.properties.outputs.RenewAtPercentageLifetime
output ValidityInMonthsOut int = newCertwithRotationKV.properties.outputs.ValidityInMonths
output SubjectNameOut string = newCertwithRotationKV.properties.outputs.SubjectName
output DnsNamesOut array = newCertwithRotationKV.properties.outputs.DnsNames
