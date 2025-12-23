// Azure Container Registry for storing container images

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

var acrName = replace('acr${baseName}${environment}', '-', '')

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: environment == 'prod' ? 'Premium' : 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
    policies: {
      retentionPolicy: {
        days: environment == 'prod' ? 30 : 7
        status: 'enabled'
      }
    }
  }
  tags: {
    environment: environment
    application: baseName
  }
}

output name string = acr.name
output loginServer string = acr.properties.loginServer
output id string = acr.id


