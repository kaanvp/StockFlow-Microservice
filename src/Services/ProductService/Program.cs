using Elastic.Clients.Elasticsearch;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.OpenApi.Models;
using ProductService.API.Grpc;
using ProductService.API.Middleware;
using ProductService.Application.Validators;
using ProductService.Consumers;
using ProductService.Infrastructure.Messaging;
using StockFlow.Shared.Common;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Search;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341"));

// ─── DbContext ────────────────────────────────────────────────────
builder.Services.AddDbContext<ProductDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"),
        sql => sql.EnableRetryOnFailure(3)));

// ─── MediatR ──────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>());

// ─── FluentValidation ─────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductCommandValidator>();

// ─── gRPC ─────────────────────────────────────────────────────────
builder.Services.AddGrpc();

// ─── MassTransit + RabbitMQ ───────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductIndexConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("product-index", e => e.ConfigureConsumer<ProductIndexConsumer>(ctx));
        cfg.ConfigureEndpoints(ctx);
    });
});

// ─── Elasticsearch ────────────────────────────────────────────────
var esUrl = builder.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
builder.Services.AddSingleton(new ElasticsearchClientSettings(new Uri(esUrl)).DefaultIndex("products"));
builder.Services.AddSingleton<ElasticsearchClient>();
builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
builder.Services.AddSingleton<ProductIndexMapping>();
builder.Services.AddHostedService<ProductIndexBackgroundService>();

// ─── Redis Cache ──────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    opt.InstanceName = "ProductService";
});
builder.Services.AddSingleton<ProductCacheService>();

// ─── Outbox Processor ─────────────────────────────────────────────
builder.Services.AddHostedService<OutboxProcessorJob>();

// ─── JWT Auth ─────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ─── OpenTelemetry ────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ProductService"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")))
    .WithMetrics(m => m
        .AddPrometheusExporter());

// ─── Health Checks ────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProductService.Infrastructure.Persistence.ProductDbContext>();

// ─── Controllers + Swagger ────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token giriniz: \"eyJ...\""
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─── Auto-create DB on startup ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    // Development: EnsureCreated ile şema oluşturulur
    // Production: 'dotnet ef migrations add Initial' çalıştırılıp MigrateAsync() kullanılmalı
    await db.Database.EnsureCreatedAsync();
}

// ─── Middleware pipeline ───────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCorrelationId();
app.UseIdempotency();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<StockGrpcService>();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint("/metrics");

await app.RunAsync();
