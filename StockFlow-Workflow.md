# StockFlow — Build Workflow

> IDE üzerinden sıfırdan yapım planı. Her aşama bağımsız çalışan bir sonuç üretir.

---

## Aşama 0 — Hazırlık

### Solution & Proje Yapısını Oluştur

```bash
dotnet new sln -n StockFlow

# Shared
dotnet new classlib -o src/Shared/StockFlow.Shared
dotnet new classlib -o src/Shared/StockFlow.Contracts

# Servisler
dotnet new webapi -o src/Gateway
dotnet new webapi -o src/Services/ProductService
dotnet new webapi -o src/Services/OrderService
dotnet new webapi -o src/Services/NotificationService

# Testler
dotnet new xunit -o tests/ProductService.Tests
dotnet new xunit -o tests/OrderService.Tests

# Solution'a ekle
dotnet sln add src/Shared/StockFlow.Shared
dotnet sln add src/Shared/StockFlow.Contracts
dotnet sln add src/Gateway
dotnet sln add src/Services/ProductService
dotnet sln add src/Services/OrderService
dotnet sln add src/Services/NotificationService
dotnet sln add tests/ProductService.Tests
dotnet sln add tests/OrderService.Tests
```

### .gitignore & İlk Commit

```bash
dotnet new gitignore
git init
git add .
git commit -m "chore: solution skeleton"
```

---

## Aşama 1 — Shared Kütüphaneler

**Hedef:** Tüm servisler bu paketi kullanacak, önce bu bitmeli.

### StockFlow.Shared

Yazılacaklar (sırayla):

1. `Domain/BaseEntity.cs` — Id, CreatedAt, domain event listesi
2. `Domain/IDomainEvent.cs` — interface
3. `Outbox/OutboxMessage.cs` — Id, Type, Payload, ProcessedAt, RetryCount
4. `Common/Result.cs` — `Result<T>` ve `Result` pattern

### StockFlow.Contracts

Yazılacaklar:

1. `Protos/stock.proto` — `CheckStock`, `ReserveStock`, `ReleaseStock` RPC tanımları
2. `Events/DomainEvents.cs` — `OrderCreatedEvent`, `OrderCancelledEvent`, `StockUpdatedEvent`, `LowStockAlertEvent`

### Paket ekle

```bash
# Contracts'a gRPC tool
cd src/Shared/StockFlow.Contracts
dotnet add package Google.Protobuf
dotnet add package Grpc.Tools
dotnet add package Grpc.Core.Api
```

**Commit:** `feat: add shared libraries and contracts`

---

## Aşama 2 — ProductService

**Hedef:** Tek başına ayakta duran, HTTP + gRPC dönen servis.

### 2.1 — Domain Katmanı

1. `Domain/Entities/Product.cs`
   - Private setter'lar, factory method `Create()`
   - `DecreaseStock()` → stok yetersizse exception, domain event raise et
   - `IncreaseStock()` → domain event raise et
   - `IsLowStock` computed property
2. `Domain/Events/ProductDomainEvents.cs`
   - `ProductCreatedDomainEvent`
   - `StockDecreasedDomainEvent`
   - `StockIncreasedDomainEvent`

### 2.2 — Infrastructure Katmanı

1. `Infrastructure/Persistence/ProductDbContext.cs`
   - `Products` ve `OutboxMessages` DbSet
   - `SaveChangesAsync` override → domain event'leri OutboxMessages'a yaz
   - `OnModelCreating` → SKU unique index, precision ayarları
2. EF Core Migration:
   ```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Design
   dotnet ef migrations add Initial --output-dir Infrastructure/Persistence/Migrations
   ```
3. `Infrastructure/Messaging/OutboxProcessorJob.cs`
   - `BackgroundService` implement et
   - Her 5 saniyede OutboxMessages'ı oku
   - `ProcessedAt == null && RetryCount < 3` filtresi
   - MassTransit `IPublishEndpoint` ile publish et

### 2.3 — Application Katmanı (CQRS)

Commands:
- `CreateProductCommand` + Handler → SKU unique kontrolü, `Product.Create()`, `SaveChangesAsync`
- `IncreaseStockCommand` + Handler

Queries (Dapper ile):
- `GetProductsQuery` + Handler → search, lowStockOnly filtresi, sayfalama
- `GetProductByIdQuery` + Handler

### 2.4 — API Katmanı

1. `API/Controllers/ProductsController.cs` — GET (list), GET (by id), POST, PATCH (stock)
2. `API/Grpc/StockGrpcService.cs` — `CheckStock`, `ReserveStock`, `ReleaseStock` implement et
3. `API/Middleware/ExceptionHandlingMiddleware.cs` — global exception handler

### 2.5 — Validation

```bash
dotnet add package FluentValidation.AspNetCore
```

- `CreateProductCommandValidator` → Name required, Price > 0, SKU not empty

### 2.6 — Program.cs

Eklenecekler sırayla:
- Serilog
- DbContext (SQL Server)
- MediatR
- FluentValidation
- gRPC (`AddGrpc`, `MapGrpcService<StockGrpcService>`)
- MassTransit + RabbitMQ
- OutboxProcessorJob (hosted service)
- OpenTelemetry (AspNetCore + EFCore + Jaeger)
- Health checks: `AddHealthChecks().AddSqlServer().AddRabbitMQ()`
- `MapHealthChecks("/health")`

### 2.7 — Auth (JWT Token endpoint)

Basit bir `POST /api/auth/token` endpoint yaz (demo amaçlı, tam identity değil):
- Request: `{ email, password }`
- Response: JWT token
- Gateway bu token'ı doğrular

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package System.IdentityModel.Tokens.Jwt
```

**Commit:** `feat: ProductService — domain, CQRS, gRPC, outbox`

---

## Aşama 3 — OrderService

**Hedef:** ProductService'e gRPC ile bağlanan, sipariş yöneten servis.

### 3.1 — Domain Katmanı

1. `Domain/Entities/Order.cs`
   - `OrderStatus` enum: Pending, Confirmed, Cancelled
   - `Create()` → `OrderCreatedDomainEvent` raise et
   - `Confirm()`, `Cancel(reason)` → `OrderCancelledDomainEvent` raise et
2. `Domain/Events/OrderDomainEvents.cs`

### 3.2 — Infrastructure Katmanı

1. `Infrastructure/Persistence/OrderDbContext.cs` — OutboxMessages dahil
2. Migration:
   ```bash
   dotnet ef migrations add Initial --output-dir Infrastructure/Persistence/Migrations
   ```
3. `Infrastructure/Messaging/OutboxProcessorJob.cs` — ProductService ile aynı pattern

### 3.3 — Application Katmanı (CQRS)

Commands:
- `CreateOrderCommand` + Handler:
  1. gRPC `CheckStock` → yetersizse `Result.Failure`
  2. gRPC `ReserveStock`
  3. `Order.Create()` + `Confirm()`
  4. `SaveChangesAsync` → OutboxMessage yazılır
- `CancelOrderCommand` + Handler:
  1. Order'ı bul
  2. `order.Cancel(reason)`
  3. gRPC `ReleaseStock`
  4. `SaveChangesAsync`

Queries (Dapper):
- `GetOrdersQuery` — email ve status filtresi, sayfalama
- `GetOrderByIdQuery`

### 3.4 — API Katmanı

1. `API/Controllers/OrdersController.cs` — GET (list), GET (by id), POST, DELETE (cancel)

### 3.5 — Program.cs

- Serilog, DbContext, MediatR, FluentValidation
- gRPC client:
  ```csharp
  builder.Services.AddGrpcClient<StockGrpc.StockGrpcClient>(o =>
      o.Address = new Uri(config["Grpc:ProductService"]));
  ```
- MassTransit + RabbitMQ, OutboxProcessorJob
- OpenTelemetry (GrpcClient instrumentation ekle)
- Health checks

**Commit:** `feat: OrderService — domain, CQRS, gRPC client, outbox`

---

## Aşama 4 — NotificationService

**Hedef:** RabbitMQ'dan event tüketip SignalR + e-posta gönderen servis.

### 4.1 — Consumers

1. `Consumers/OrderCreatedConsumer.cs`
   - `IConsumer<OrderCreatedEvent>` implement et
   - SignalR hub'a `admins` grubuna broadcast
   - MailKit ile müşteriye e-posta
2. `Consumers/LowStockAlertConsumer.cs`
   - `IConsumer<LowStockAlertEvent>` implement et
   - SignalR ile `product-{id}` grubuna + `admins` grubuna bildirim

### 4.2 — SignalR Hub

1. `Hubs/StockHub.cs`
   - `JoinProductGroup(productId)` metodu
   - `OnConnectedAsync` override → userId ile gruba katıl

### 4.3 — Program.cs

- Serilog, SignalR, MassTransit (consumer'ları kaydet, ayrı queue'lar)
- MailKit config
- CORS (SignalR için)
- `MapHub<StockHub>("/hubs/stock")`
- Health check

**Commit:** `feat: NotificationService — consumers, SignalR, email`

---

## Aşama 5 — Gateway

**Hedef:** Tek giriş noktası, JWT doğrulama, route yönetimi.

### 5.1 — Program.cs

```bash
dotnet add package Yarp.ReverseProxy
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

- YARP: `AddReverseProxy().LoadFromConfig(...)`
- JWT auth: `AddAuthentication("Bearer").AddJwtBearer(...)`
- OpenTelemetry
- Serilog

### 5.2 — appsettings.json YARP config

```json
"ReverseProxy": {
  "Routes": {
    "product-route": { "ClusterId": "product-cluster", "AuthorizationPolicy": "default", "Match": { "Path": "/api/products/{**catch-all}" } },
    "order-route":   { "ClusterId": "order-cluster",   "AuthorizationPolicy": "default", "Match": { "Path": "/api/orders/{**catch-all}" } },
    "auth-route":    { "ClusterId": "product-cluster",                                   "Match": { "Path": "/api/auth/{**catch-all}" } },
    "notification-route": { "ClusterId": "notification-cluster", "Match": { "Path": "/hubs/{**catch-all}" } }
  },
  "Clusters": {
    "product-cluster":      { "Destinations": { "d1": { "Address": "http://product-service:5001" } } },
    "order-cluster":        { "Destinations": { "d1": { "Address": "http://order-service:5002" } } },
    "notification-cluster": { "Destinations": { "d1": { "Address": "http://notification-service:5003" } } }
  }
}
```

**Commit:** `feat: Gateway — YARP routing, JWT auth`

---

## Aşama 6 — Docker

**Hedef:** `docker-compose up -d` ile tüm sistem ayağa kalksın.

### 6.1 — Her servis için Dockerfile

Multi-stage build:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# restore → build → publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# sadece runtime
```

> Dikkat: `COPY` sırasını doğru kur. Shared projeler servisten önce kopyalanmalı.

### 6.2 — docker-compose.yml

Altyapı servisleri:
- `sqlserver` — healthcheck ekle (servisler başlamadan önce hazır olsun)
- `rabbitmq:3.13-management` — healthcheck ekle
- `redis:7-alpine`
- `jaeger/all-in-one:1.57` — port 6831 (UDP), 16686 (UI)
- `datalust/seq` — port 5341, 8080
- `mailhog` — port 1025 (SMTP), 8025 (UI)

Uygulama servisleri:
- `depends_on` → sqlserver ve rabbitmq `service_healthy` olana kadar bekleme
- `restart: on-failure`
- Environment variable'ları ile connection string'leri inject et

### 6.3 — Test

```bash
docker-compose up -d
# Logları izle:
docker-compose logs -f product-service
# Swagger:
# http://localhost:5000/swagger  (Gateway üzerinden değil, direkt servis)
# Jaeger:
# http://localhost:16686
```

**Commit:** `feat: Docker — multi-stage Dockerfiles, docker-compose`

---

## Aşama 7 — Testler

**Hedef:** Hem domain logic hem de gerçek DB ile integration test.

### 7.1 — ProductService Unit Tests

```bash
cd tests/ProductService.Tests
dotnet add package FluentAssertions
dotnet add package xunit
```

Yazılacaklar:
- `Unit/ProductDomainTests.cs`
  - `Create` → domain event raise edildi mi?
  - `DecreaseStock` → stok düştü mü, event var mı?
  - `DecreaseStock` yetersiz stok → exception fırlatıyor mu?
  - `IsLowStock` threshold kontrolü

### 7.2 — ProductService Integration Tests (Testcontainers)

```bash
dotnet add package Testcontainers.MsSql
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

Yazılacaklar:
- `Integration/ProductRepositoryTests.cs`
  - `IAsyncLifetime` → container başlat/durdur
  - Gerçek migration çalıştır
  - OutboxMessage yazıldı mı kontrolü
  - SKU unique constraint testi

### 7.3 — OrderService Tests

- `Unit/OrderDomainTests.cs` — Order.Create, Cancel domain event kontrolleri
- `Integration/CreateOrderCommandTests.cs` — gRPC client mock + DB container

```bash
dotnet add package Testcontainers.MsSql
dotnet add package NSubstitute   # gRPC client mock için
```

**Commit:** `test: unit and integration tests with Testcontainers`

---

## Aşama 8 — GitHub Actions CI/CD

**Hedef:** Her push'ta build + test otomatik çalışsın.

### `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Unit Tests
        run: dotnet test tests/ProductService.Tests --filter "Category=Unit" --no-build

      - name: Integration Tests
        run: dotnet test tests/ProductService.Tests --filter "Category=Integration" --no-build
        # Testcontainers Docker'ı otomatik bulur, runner'da Docker mevcut

  docker-build:
    runs-on: ubuntu-latest
    needs: build-and-test
    steps:
      - uses: actions/checkout@v4

      - name: Build Docker images
        run: docker-compose build
```

**Commit:** `ci: GitHub Actions — build, test, docker`

---

## Aşama 9 — Kubernetes (Helm)

**Hedef:** Tek komutla cluster'a deploy.

### 9.1 — Helm Chart Yapısı

```
helm/stockflow/
├── Chart.yaml
├── values.yaml
└── templates/
    ├── secrets.yaml          # DB password, JWT secret
    ├── product-service.yaml  # Deployment + Service
    ├── order-service.yaml
    ├── notification-service.yaml
    ├── gateway.yaml
    └── ingress.yaml          # dışarıya açık tek endpoint
```

### 9.2 — Her template için kontrol listesi

- `livenessProbe` → `/health` endpoint
- `readinessProbe` → `/health` endpoint
- `resources.requests` ve `resources.limits`
- `env` → secret ref ile connection string
- `replicaCount` → values.yaml'dan gelsin

### 9.3 — Deploy

```bash
# Local (minikube veya kind):
minikube start
helm install stockflow ./helm/stockflow

# Güncelle:
helm upgrade stockflow ./helm/stockflow

# Durum:
kubectl get pods
kubectl logs deployment/product-service
```

**Commit:** `feat: Kubernetes Helm chart`

---

## Commit Özeti (Tüm Proje)

```
chore: solution skeleton
feat: add shared libraries and contracts
feat: ProductService — domain, CQRS, gRPC, outbox
feat: OrderService — domain, CQRS, gRPC client, outbox
feat: NotificationService — consumers, SignalR, email
feat: Gateway — YARP routing, JWT auth
feat: Docker — multi-stage Dockerfiles, docker-compose
test: unit and integration tests with Testcontainers
ci: GitHub Actions — build, test, docker
feat: Kubernetes Helm chart
```

---

## Kritik Notlar

**EF Core Migration sırası:** Her servisin kendi DbContext'i var. Migration'ları ayrı ayrı çalıştır:
```bash
dotnet ef migrations add Initial --project src/Services/ProductService
dotnet ef migrations add Initial --project src/Services/OrderService
```

**gRPC HTTP/2:** Docker içinde HTTP/2 için Kestrel config'e dikkat:
```json
"Kestrel": {
  "Endpoints": {
    "Http": { "Url": "http://+:5001", "Protocols": "Http1" },
    "Grpc": { "Url": "http://+:5011", "Protocols": "Http2" }
  }
}
```

**Duplicate dosyalar:** Önceki oturumdan `OrderCreatedEventConsumer.cs` ve `StockFlowHub.cs` isimli fazladan dosyalar var. Bunları sil, doğru isimler: `OrderCreatedConsumer.cs` ve `StockHub.cs`.

**OutboxMessage type map:** `OutboxProcessorJob`'da event type'larını mutlaka kaydet:
```csharp
private static readonly Dictionary<string, Type> _eventTypeMap = new()
{
    [typeof(StockUpdatedEvent).FullName!] = typeof(StockUpdatedEvent),
    [typeof(LowStockAlertEvent).FullName!] = typeof(LowStockAlertEvent),
};
```
