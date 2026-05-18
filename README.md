# StockFlow 🚀

> Mikroservis tabanlı envanter ve sipariş yönetim sistemi  
> .NET 9 · YARP · gRPC · RabbitMQ · Elasticsearch · Redis · OpenTelemetry · Docker

## Mimarı

```
┌──────────────────────────────────────────────────────┐
│              YARP API Gateway (:5000)                │
│     Rate Limiting · JWT Auth · CORS · CorrelationId  │
└──────┬──────────────┬──────────────┬─────────────────┘
       │              │              │
  Product API     Order API     Notification API
  (:5001/5011)    (:5002)        (:5003)
       │              │
  [SQL Server]   [SQL Server]
       │              │
       └──────────────┘
     RabbitMQ (Outbox Pattern)
            │
     Elasticsearch (Full-text Search)
     Redis (Cache)
     Jaeger (Tracing)
     Seq (Logging)
```

## Hızlı Başlangıç

```bash
# Altyapıyı başlat
docker compose up -d sqlserver rabbitmq redis elasticsearch kibana jaeger seq mailhog

# Servisleri çalıştır
dotnet run --project src/Gateway
dotnet run --project src/Services/ProductService
dotnet run --project src/Services/OrderService
dotnet run --project src/Services/NotificationService
```

## Servisler

| Servis | Port | HTTP | gRPC | DB |
|--------|:----:|:----:|:----:|:--:|
| Gateway | 5000 | ✅ YARP | ❌ | ❌ |
| ProductService | 5001/5011 | ✅ REST | ✅ Stock | SQL Server |
| OrderService | 5002 | ✅ REST | ❌ (client) | SQL Server |
| NotificationService | 5003 | ✅ SignalR | ❌ | ❌ |

## Teknoloji Yığını

| Alan | Teknoloji |
|------|-----------|
| Framework | .NET 9, ASP.NET Core |
| Servisler arası (sync) | gRPC (Grpc.AspNetCore) |
| Servisler arası (async) | RabbitMQ + MassTransit |
| Outbox Pattern | EF Core interceptor + BackgroundService |
| Gateway | YARP (Yet Another Reverse Proxy) |
| ORM | EF Core 9 (write) + Dapper (read) |
| Full-text Search | Elasticsearch 8.14 + Elastic.Clients.Elasticsearch |
| Cache | Redis (StackExchange.Redis) |
| Auth | JWT Bearer |
| Real-time | SignalR |
| Resilience | Polly (Microsoft.Extensions.Http.Resilience) |
| Tracing | OpenTelemetry + Jaeger |
| Logging | Serilog + Seq |
| Email | MailKit + Mailhog (dev) |
| Rate Limiting | Built-in ASP.NET Rate Limiter |
| Test | xUnit + FluentAssertions + Testcontainers |
| Container | Docker + docker-compose |

## API Endpoints

### Product Service
| Method | Path | Açıklama |
|--------|------|----------|
| GET | `/api/products` | Ürün listesi (Dapper, pagination) |
| GET | `/api/products/{id}` | Ürün detayı |
| GET | `/api/products/search?q=` | Full-text arama (Elasticsearch) |
| POST | `/api/products` | Ürün oluştur |
| PATCH | `/api/products/{id}/stock` | Stok güncelle |
| POST | `/api/auth/token` | JWT token al |

### Order Service
| Method | Path | Açıklama |
|--------|------|----------|
| GET | `/api/orders` | Sipariş listesi |
| GET | `/api/orders/{id}` | Sipariş detayı |
| POST | `/api/orders` | Sipariş oluştur (gRPC stock check) |
| DELETE | `/api/orders/{id}?reason=` | Sipariş iptal (gRPC stock release) |

### Notification (SignalR)
| Hub | Path |
|-----|------|
| StockHub | `/hubs/stock` |

## Monitoring

| Araç | URL |
|------|-----|
| Jaeger | http://localhost:16686 |
| Seq | http://localhost:5341 |
| Kibana | http://localhost:5601 |
| Swagger | http://localhost:5000/swagger |
| Mailhog | http://localhost:8025 |
| RabbitMQ | http://localhost:15672 (guest/guest) |

## Development

```bash
# Testler
dotnet test --filter "Category=Unit"

# Migration (production için)
dotnet ef migrations add Initial --project src/Services/ProductService
dotnet ef migrations add Initial --project src/Services/OrderService

# User Secrets (appsettings.json'daki secret'ları override etmek için)
dotnet user-secrets init --project src/Services/ProductService
dotnet user-secrets set "ConnectionStrings:SqlServer" "..."
```

## License

MIT
