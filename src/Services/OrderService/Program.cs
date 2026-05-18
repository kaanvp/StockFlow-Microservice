using FluentValidation;
using MassTransit;
using MassTransit.Saga;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using Serilog;
using StockFlow.Contracts.Grpc;
using StockFlow.Shared.Common;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341"));

// ─── DbContext ────────────────────────────────────────────────────
builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"),
        sql => sql.EnableRetryOnFailure(3)));

// ─── MediatR ──────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>());

// ─── FluentValidation ─────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ─── JWT Auth ─────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, opt =>
    {
        opt.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ─── gRPC Client (with Polly resilience) ───────────────────────────
builder.Services.AddGrpcClient<StockGrpc.StockGrpcClient>(o =>
{
    o.Address = new Uri(builder.Configuration["Grpc:ProductService"] ?? "http://localhost:5011");
})
.AddStandardResilienceHandler();

// ─── MassTransit + RabbitMQ + Saga ────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .InMemoryRepository();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

// ─── Outbox Processor ─────────────────────────────────────────────
builder.Services.AddHostedService<OutboxProcessorJob>();

// ─── OpenTelemetry ────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("OrderService"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddOtlpExporter(o =>
            o.Endpoint = new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")));

// ─── Health Checks ────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>();

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
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    // Development: EnsureCreated ile şema oluşturulur
    // Production: 'dotnet ef migrations add Initial' çalıştırılıp MigrateAsync() kullanılmalı
    await db.Database.EnsureCreatedAsync();
}

// ─── Middleware pipeline ───────────────────────────────────────────
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
app.MapHealthChecks("/health");

await app.RunAsync();
