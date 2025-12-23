// Azure Key Vault for secrets management

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

@description('SQL Connection String')
@secure()
param sqlConnectionString string

@description('JWT Secret Key')
@secure()
param jwtSecretKey string

var keyVaultName = 'kv-${baseName}-${environment}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled' // Set to 'Disabled' for production with VNet
    networkAcls: {
      defaultAction: 'Allow' // Set to 'Deny' for production
      bypass: 'AzureServices'
    }
  }
  tags: {
    environment: environment
    application: baseName
  }
}

// Store secrets
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ConnectionStrings--DefaultConnection'
  properties: {
    value: sqlConnectionString
  }
}

resource jwtSecretKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'Jwt--SecretKey'
  properties: {
    value: jwtSecretKey
  }
}

output name string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri


