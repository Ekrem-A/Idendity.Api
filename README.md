# Identity Service

E-Commerce mikroservis mimarisinin kimlik doğrulama ve yetkilendirme servisi.

## Mimari

Clean Architecture prensipleri ile geliştirilmiştir:

```
Idendity.Api/
├── Idendity.Api/           # API katmanı (Controllers, Middleware)
├── Idendity.Application/   # Uygulama katmanı (DTOs, Interfaces, Validators)
├── Idendity.Domain/        # Domain katmanı (Entities, Constants)
├── Idendity.Infrastructure/ # Altyapı katmanı (EF Core, Identity, Services)
└── infra/                  # Azure Bicep altyapı dosyaları
```

## Özellikler

- **Kimlik Doğrulama**: ASP.NET Core Identity ile kullanıcı yönetimi
- **JWT Token**: Access token ve Refresh token rotation
- **Güvenlik**: 
  - Rate limiting
  - CORS
  - Security headers
  - Audit logging
- **Azure Entegrasyonu**:
  - Azure Key Vault (secret yönetimi)
  - Azure Container Apps (hosting)
  - Dapr mTLS (servis-servis güvenliği)

## API Endpoints

### Auth
- `POST /api/auth/register` - Yeni kullanıcı kaydı
- `POST /api/auth/login` - Giriş yapma
- `POST /api/auth/refresh` - Token yenileme
- `POST /api/auth/revoke` - Token iptal
- `POST /api/auth/logout` - Çıkış yapma

### Users
- `GET /api/users/me` - Mevcut kullanıcı bilgisi
- `PUT /api/users/me` - Profil güncelleme
- `POST /api/users/me/change-password` - Şifre değiştirme
- `GET /api/users` - Tüm kullanıcılar (Admin)
- `GET /api/users/{id}` - Kullanıcı detayı (Admin)
- `DELETE /api/users/{id}` - Kullanıcı deaktif etme (Admin)

### Health
- `GET /api/health` - Basit sağlık kontrolü
- `GET /api/health/ready` - Detaylı hazırlık kontrolü
- `GET /api/health/live` - Yaşam kontrolü (Kubernetes/ACA)

## Yerel Geliştirme

### Gereksinimler
- .NET 8 SDK
- SQL Server (LocalDB veya Docker)
- Docker (opsiyonel)

### Çalıştırma

```bash
# Veritabanı migration
cd Idendity.Api
dotnet ef database update --project Idendity.Infrastructure --startup-project Idendity.Api

# Uygulamayı çalıştırma
dotnet run --project Idendity.Api
```

### Docker ile Çalıştırma

```bash
cd Idendity.Api
docker build -t identity-service -f Idendity.Api/Dockerfile .
docker run -p 8080:8080 identity-service
```

## Azure'a Deploy

### 1. Altyapı Kurulumu

```bash
cd infra

# PowerShell
./deploy.ps1 -Environment dev -SqlAdminLogin sqladmin

# Bash
./deploy.sh dev eastus ecommerce
```

### 2. Container Image Push

```bash
az acr login --name acrecommercedev
docker build -t acrecommercedev.azurecr.io/identity-service:latest -f Idendity.Api/Dockerfile .
docker push acrecommercedev.azurecr.io/identity-service:latest
```

### 3. Container App Güncelleme

```bash
az containerapp update \
  --name identity-service-dev \
  --resource-group rg-ecommerce-dev \
  --image acrecommercedev.azurecr.io/identity-service:latest
```

### 4. (Opsiyonel) GitHub Actions ile Deploy (CI/CD)

Repo içinde örnek workflow: `.github/workflows/deploy-containerapp.yml`

Bu yaklaşımda `azure/login` adımı **SERVICE_PRINCIPAL** ile giriş yapar ve şu değerler sağlanmazsa şu hatayı alırsın:

- `Error: Login failed... auth-type: SERVICE_PRINCIPAL... Ensure 'client-id' and 'tenant-id' are supplied.`

#### Gerekli GitHub Secrets (OIDC önerilir)

GitHub → Repository → **Settings → Secrets and variables → Actions**

- **Secrets**
  - `AZURE_CLIENT_ID`
  - `AZURE_TENANT_ID`
  - `AZURE_SUBSCRIPTION_ID`

- **Variables**
  - `RESOURCE_GROUP_NAME` (örn: `rg-ecommerce-dev`)
  - `CONTAINER_APP_NAME` (örn: `identity-service-dev`)
  - `ACR_NAME` (örn: `acrecommercedev`)
  - `ACR_LOGIN_SERVER` (örn: `acrecommercedev.azurecr.io`)

#### OIDC kurulumu (Azure tarafı) — adım adım

Bu adımlar Azure’da bir **App Registration / Service Principal** oluşturur ve GitHub Actions’a **Federated Credential** ekler (client secret gerekmez).

1) Azure CLI ile tenant/subscription bilgilerini al:

```bash
az account show --query "{tenantId:tenantId, subscriptionId:id}" -o json
```

2) App Registration oluştur:

```bash
APP_NAME="github-oidc-identityservice"
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
az ad sp create --id "$APP_ID"
echo "APP_ID=$APP_ID"
```

3) GitHub Federated Credential ekle (main branch örneği):

`OWNER/REPO` kısmını kendi repo adınla değiştir.

```bash
OWNER_REPO="OWNER/REPO"
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters "{
    \"name\": \"github-main\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${OWNER_REPO}:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
```

4) Yetkiler (role assignment):

- Container Apps update + RG içindeki kaynaklar için: **Contributor** (Resource Group scope)
- ACR’a image push için: **AcrPush** (ACR scope)

```bash
SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)

RG_ID=$(az group show -n "rg-ecommerce-dev" --query id -o tsv)
az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal --role "Contributor" --scope "$RG_ID"

ACR_ID=$(az acr show -n "acrecommercedev" --query id -o tsv)
az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal --role "AcrPush" --scope "$ACR_ID"
```

5) GitHub Secrets’ları doldur:

- `AZURE_CLIENT_ID` = **APP_ID**
- `AZURE_TENANT_ID` = `az account show --query tenantId -o tsv`
- `AZURE_SUBSCRIPTION_ID` = `az account show --query id -o tsv`

#### Alternatif: AZURE_CREDENTIALS ile login (client secret)

OIDC yerine klasik yöntem istersen workflow’daki “Azure Login (Service Principal Secret)” adımını açıp şu secret’ı ekle:

- **Secrets**
  - `AZURE_CREDENTIALS` (JSON)

## Yapılandırma

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=IdentityDb;..."
  },
  "Jwt": {
    "SecretKey": "min-32-karakter-gizli-anahtar",
    "Issuer": "IdentityService",
    "Audience": "ECommerceApp",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "KeyVault": {
    "Uri": "https://kv-ecommerce-dev.vault.azure.net/"
  }
}
```

## Güvenlik

- JWT token'lar 15 dakika geçerli (yapılandırılabilir)
- Refresh token rotation (eski token kullanılamaz)
- Reuse detection (güvenlik ihlali tespiti)
- Rate limiting:
  - Login: 5 deneme/dakika
  - Register: 10 kayıt/saat
- Security headers (XSS, CSRF, Clickjacking koruması)

## Dapr Entegrasyonu

Servis, Azure Container Apps üzerinde Dapr sidecar ile çalışır:

- **mTLS**: Servisler arası şifreli iletişim
- **Service Discovery**: `identity-service` app ID ile erişim
- **Access Control**: Sadece yetkili servisler erişebilir

### Diğer Servislerden Çağrı

```csharp
// Dapr üzerinden kullanıcı bilgisi alma
var user = await daprClient.InvokeMethodAsync<UserDto>(
    HttpMethod.Get, 
    "identity-service", 
    $"api/users/{userId}");
```

## Lisans

Bu proje özel lisans altındadır.


