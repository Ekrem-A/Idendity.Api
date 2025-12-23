#!/bin/bash
# Bash deployment script for Azure Container Apps infrastructure
# Prerequisites: Azure CLI, logged in with 'az login'

set -e

# Default values
ENVIRONMENT="${1:-dev}"
LOCATION="${2:-eastus}"
BASE_NAME="${3:-ecommerce}"

echo "=== Deploying IdentityService Infrastructure ==="
echo "Environment: $ENVIRONMENT"
echo "Location: $LOCATION"
echo "Base Name: $BASE_NAME"

# Prompt for sensitive values
read -p "SQL Admin Login: " SQL_ADMIN_LOGIN
read -s -p "SQL Admin Password: " SQL_ADMIN_PASSWORD
echo ""
read -s -p "JWT Secret Key (min 32 chars): " JWT_SECRET_KEY
echo ""

# Validate JWT key length
if [ ${#JWT_SECRET_KEY} -lt 32 ]; then
    echo "Error: JWT Secret Key must be at least 32 characters"
    exit 1
fi

# Deploy infrastructure
echo ""
echo "Deploying Bicep template..."

az deployment sub create \
    --name "identity-infra-$ENVIRONMENT-$(date +%Y%m%d%H%M%S)" \
    --location "$LOCATION" \
    --template-file ./main.bicep \
    --parameters environment="$ENVIRONMENT" \
    --parameters location="$LOCATION" \
    --parameters baseName="$BASE_NAME" \
    --parameters sqlAdminLogin="$SQL_ADMIN_LOGIN" \
    --parameters sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    --parameters jwtSecretKey="$JWT_SECRET_KEY"

if [ $? -ne 0 ]; then
    echo "Deployment failed!"
    exit 1
fi

echo ""
echo "=== Deployment completed successfully! ==="

# Get outputs
RESOURCE_GROUP="rg-$BASE_NAME-$ENVIRONMENT"
echo ""
echo "Resource Group: $RESOURCE_GROUP"

# Display next steps
ACR_NAME="acr${BASE_NAME}${ENVIRONMENT}"
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv 2>/dev/null || echo "")

if [ -n "$ACR_LOGIN_SERVER" ]; then
    echo "ACR Login Server: $ACR_LOGIN_SERVER"
    echo ""
    echo "Next steps:"
    echo "1. Build and push container image:"
    echo "   docker build -t $ACR_LOGIN_SERVER/identity-service:latest -f Idendity.Api/Dockerfile ."
    echo "   az acr login --name $ACR_NAME"
    echo "   docker push $ACR_LOGIN_SERVER/identity-service:latest"
    echo ""
    echo "2. Update container app with new image:"
    echo "   az containerapp update --name identity-service-$ENVIRONMENT --resource-group $RESOURCE_GROUP --image $ACR_LOGIN_SERVER/identity-service:latest"
fi


