// Log Analytics Workspace for monitoring and diagnostics

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

var workspaceName = 'log-${baseName}-${environment}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environment == 'prod' ? 90 : 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: environment == 'prod' ? 5 : 1
    }
  }
  tags: {
    environment: environment
    application: baseName
  }
}

output workspaceId string = logAnalyticsWorkspace.id
output workspaceKey string = logAnalyticsWorkspace.listKeys().primarySharedKey
output workspaceName string = logAnalyticsWorkspace.name


