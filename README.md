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

## Railway + PostgreSQL ile Deploy (Docker)

Bu proje artık **PostgreSQL** kullanacak şekilde ayarlanmıştır.

### Railway'de PostgreSQL oluşturma

Railway projesinde **Add → Database → PostgreSQL** ekle.

Railway, uygulama servisinde genelde otomatik olarak şu environment variable’ı verir:

- `DATABASE_URL`

Uygulama, `ConnectionStrings:DefaultConnection` boş ise `DATABASE_URL`’ı otomatik olarak Postgres connection string’e çevirir.

### Railway'de Docker ile deploy

1) Railway’de yeni servis oluştur (Dockerfile ile).
2) Repository olarak bu projeyi bağla.
3) Environment variables:
   - **`DATABASE_URL`** (PostgreSQL tarafından otomatik gelir)
   - **`Jwt__SecretKey`** (en az 32 karakter)
   - (opsiyonel) `Jwt__Issuer`, `Jwt__Audience`
   - (opsiyonel) **`RUN_MIGRATIONS=true`** (container açılışında migration otomatik uygulasın)

### PostgreSQL Migration

SQL Server migration’ları Postgres ile uyumlu değildir. PostgreSQL için migration’ı yeniden üret:

```bash
cd Idendity.Api

# (gerekirse) tooling
dotnet tool install --global dotnet-ef

# Yeni migration (PostgreSQL)
dotnet ef migrations add InitialCreate_Postgres --project ..\Idendity.Infrastructure --startup-project .\Idendity.Api

# DB'ye uygula
dotnet ef database update --project ..\Idendity.Infrastructure --startup-project .\Idendity.Api
```

Railway’de migration’ı uygulamak için en basit yöntem:
- Deploy öncesi local’de `database update` çalıştırıp DB’yi hazırlamak, ya da
- Railway “deploy command”/“pre-deploy” adımı kullanıyorsan orada `dotnet ef database update` çalıştırmak.

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


