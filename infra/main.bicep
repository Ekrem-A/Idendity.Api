// Main Bicep deployment file for IdentityService on Azure Container Apps
// Deploy with: az deployment sub create --location eastus --template-file main.bicep --parameters main.parameters.json

targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure region for all resources')
param location string = 'eastus'

@description('Base name for all resources')
param baseName string = 'ecommerce'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('JWT Secret Key (min 32 characters)')
@secure()
param jwtSecretKey string

// Resource group
var resourceGroupName = 'rg-${baseName}-${environment}'

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
  tags: {
    environment: environment
    application: baseName
    managedBy: 'bicep'
  }
}

// Deploy all modules
module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
    sqlConnectionString: sqlServer.outputs.connectionString
    jwtSecretKey: jwtSecretKey
  }
}

module acr 'modules/container-registry.bicep' = {
  name: 'acr-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
  }
}

module sqlServer 'modules/sql-server.bicep' = {
  name: 'sql-server-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    logAnalyticsWorkspaceKey: logAnalytics.outputs.workspaceKey
  }
}

module identityService 'modules/identity-service.bicep' = {
  name: 'identity-service-deployment'
  scope: rg
  params: {
    location: location
    baseName: baseName
    environment: environment
    containerAppsEnvId: containerAppsEnv.outputs.environmentId
    acrLoginServer: acr.outputs.loginServer
    acrName: acr.outputs.name
    keyVaultName: keyVault.outputs.name
  }
  dependsOn: [
    acr
    keyVault
    containerAppsEnv
  ]
}

// Outputs
output resourceGroupName string = rg.name
output acrLoginServer string = acr.outputs.loginServer
output identityServiceUrl string = identityService.outputs.fqdn
output keyVaultName string = keyVault.outputs.name


