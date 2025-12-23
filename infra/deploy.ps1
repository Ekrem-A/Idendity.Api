# PowerShell deployment script for Azure Container Apps infrastructure
# Prerequisites: Azure CLI, logged in with 'az login'

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'dev',
    
    [Parameter(Mandatory=$false)]
    [string]$Location = 'eastus',
    
    [Parameter(Mandatory=$false)]
    [string]$BaseName = 'ecommerce',
    
    [Parameter(Mandatory=$true)]
    [string]$SqlAdminLogin,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$SqlAdminPassword,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$JwtSecretKey
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Deploying IdentityService Infrastructure ===" -ForegroundColor Cyan
Write-Host "Environment: $Environment"
Write-Host "Location: $Location"
Write-Host "Base Name: $BaseName"

# Convert SecureString to plain text for Azure CLI (handled securely)
$sqlPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlAdminPassword))
$jwtKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($JwtSecretKey))

# Deploy infrastructure
Write-Host "`nDeploying Bicep template..." -ForegroundColor Yellow

az deployment sub create `
    --name "identity-infra-$Environment-$(Get-Date -Format 'yyyyMMddHHmmss')" `
    --location $Location `
    --template-file ./main.bicep `
    --parameters environment=$Environment `
    --parameters location=$Location `
    --parameters baseName=$BaseName `
    --parameters sqlAdminLogin=$SqlAdminLogin `
    --parameters sqlAdminPassword=$sqlPasswordPlain `
    --parameters jwtSecretKey=$jwtKeyPlain

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed!"
    exit 1
}

Write-Host "`n=== Deployment completed successfully! ===" -ForegroundColor Green

# Get outputs
$resourceGroupName = "rg-$BaseName-$Environment"
Write-Host "`nResource Group: $resourceGroupName"

$acrLoginServer = az deployment sub show `
    --name "identity-infra-$Environment-$(Get-Date -Format 'yyyyMMdd')*" `
    --query properties.outputs.acrLoginServer.value -o tsv 2>$null

if ($acrLoginServer) {
    Write-Host "ACR Login Server: $acrLoginServer"
    Write-Host "`nNext steps:"
    Write-Host "1. Build and push container image:"
    Write-Host "   docker build -t $acrLoginServer/identity-service:latest ."
    Write-Host "   az acr login --name $($acrLoginServer.Split('.')[0])"
    Write-Host "   docker push $acrLoginServer/identity-service:latest"
    Write-Host ""
    Write-Host "2. Update container app with new image:"
    Write-Host "   az containerapp update --name identity-service-$Environment --resource-group $resourceGroupName --image $acrLoginServer/identity-service:latest"
}


