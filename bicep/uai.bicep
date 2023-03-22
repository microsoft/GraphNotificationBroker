param name string
param graphSpId string
@description('Specifies the object ID of a user, service principal or security group in the Azure Active Directory tenant for the vault. The object ID must be unique for the list of access policies. Get it by using Get-AzADUser or Get-AzADServicePrincipal cmdlets.')
param userId string

// create user assigned managed identity
resource uai 'Microsoft.ManagedIdentity/userAssignedIdentities@2022-01-31-preview' = {
  name: toLower('${name}-uai')
  location: resourceGroup().location
}

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultAdminRoleId = '00482a5a-887f-4fb3-b363-3b7fe8e74483'
var keyVaultCertificateOfficerRoleId = 'a4417e6f-fecd-4de8-b567-7b0420556985'

// grant key vault roles to user assigned identity
resource uaiSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, keyVaultSecretsUserRoleId, uai.name)
  properties: {
    principalId: uai.properties.principalId
    roleDefinitionId: '${subscription().id}/providers/Microsoft.Authorization/roleDefinitions/${keyVaultSecretsUserRoleId}'
    principalType: 'ServicePrincipal'
  }
}

// key vault certificate officer
resource uaiCertificateOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, keyVaultCertificateOfficerRoleId, uai.name)
  properties: {
    principalId: uai.properties.principalId
    roleDefinitionId: '${subscription().id}/providers/Microsoft.Authorization/roleDefinitions/${keyVaultCertificateOfficerRoleId}'
    principalType: 'ServicePrincipal'
  }
}

// grant key vault secrets user role to Graph Change Tracking Service Principal
resource graphSpRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, keyVaultSecretsUserRoleId, graphSpId)
  properties: {
    principalId: graphSpId
    roleDefinitionId: '${subscription().id}/providers/Microsoft.Authorization/roleDefinitions/${keyVaultSecretsUserRoleId}'
    principalType: 'ServicePrincipal'
  }
}

// grant key vault admin to current user
resource userKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, keyVaultAdminRoleId, userId)
  properties: {
    principalId: userId
    roleDefinitionId: '${subscription().id}/providers/Microsoft.Authorization/roleDefinitions/${keyVaultAdminRoleId}'
  }
}
