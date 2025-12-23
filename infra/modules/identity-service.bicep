// IdentityService Container App

@description('Azure region')
param location string

@description('Base name')
param baseName string

@description('Environment')
param environment string

@description('Container Apps Environment ID')
param containerAppsEnvId string

@description('ACR Login Server')
param acrLoginServer string

@description('ACR Name')
param acrName string

@description('Key Vault Name')
param keyVaultName string

@description('Container image tag')
param imageTag string = 'latest'

var appName = 'identity-service'
var imageName = '${acrLoginServer}/${appName}:${imageTag}'

// Get ACR credentials
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

// Get Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' existing = {
  name: keyVaultName
}

resource identityService 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${appName}-${environment}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false // Internal only - accessed via Gateway
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        corsPolicy: {
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedHeaders: ['*']
          maxAge: 86400
        }
      }
      dapr: {
        enabled: true
        appId: appName
        appPort: 8080
        appProtocol: 'http'
        enableApiLogging: true
      }
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
      ]
      registries: [
        {
          server: acrLoginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: imageName
          resources: {
            cpu: json(environment == 'prod' ? '1.0' : '0.5')
            memory: environment == 'prod' ? '2Gi' : '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environment == 'prod' ? 'Production' : 'Development'
            }
            {
              name: 'KeyVault__Uri'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'Jwt__Issuer'
              value: 'IdentityService'
            }
            {
              name: 'Jwt__Audience'
              value: 'ECommerceApp'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/api/health/live'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/api/health/ready'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: environment == 'prod' ? 2 : 1
        maxReplicas: environment == 'prod' ? 10 : 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
  tags: {
    environment: environment
    application: baseName
    service: appName
  }
}

// Assign Key Vault access to the Container App's managed identity
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identityService.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: identityService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output name string = identityService.name
output fqdn string = identityService.properties.configuration.ingress.fqdn
output principalId string = identityService.identity.principalId


