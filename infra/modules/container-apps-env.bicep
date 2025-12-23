// Azure Container Apps Environment with Dapr enabled

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

@description('Log Analytics Workspace ID')
param logAnalyticsWorkspaceId string

@description('Log Analytics Workspace Key')
@secure()
param logAnalyticsWorkspaceKey string

var envName = 'cae-${baseName}-${environment}'

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: envName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: split(logAnalyticsWorkspaceId, '/')[8]
        sharedKey: logAnalyticsWorkspaceKey
      }
    }
    daprAIConnectionString: '' // Add Application Insights connection string for Dapr tracing
    zoneRedundant: environment == 'prod'
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
  tags: {
    environment: environment
    application: baseName
  }
}

output environmentId string = containerAppsEnvironment.id
output environmentName string = containerAppsEnvironment.name
output defaultDomain string = containerAppsEnvironment.properties.defaultDomain


