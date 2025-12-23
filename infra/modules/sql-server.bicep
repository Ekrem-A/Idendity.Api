// Azure SQL Server and Database

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

@description('SQL Admin Login')
@secure()
param adminLogin string

@description('SQL Admin Password')
@secure()
param adminPassword string

var serverName = 'sql-${baseName}-${environment}'
var databaseName = 'IdentityDb'

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // Set to 'Disabled' for production with VNet
  }
  tags: {
    environment: environment
    application: baseName
  }
}

// Allow Azure services
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: environment == 'prod' ? 'S1' : 'Basic'
    tier: environment == 'prod' ? 'Standard' : 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: environment == 'prod' ? 268435456000 : 2147483648 // 250GB or 2GB
    zoneRedundant: environment == 'prod'
  }
  tags: {
    environment: environment
    application: baseName
  }
}

output serverName string = sqlServer.name
output databaseName string = sqlDatabase.name
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};User ID=${adminLogin};Password=${adminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'


