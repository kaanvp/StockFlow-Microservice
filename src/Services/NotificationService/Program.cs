using MassTransit;
using NotificationService.Consumers;
using NotificationService.Hubs;
using NotificationService.Services;
using Serilog;
using StockFlow.Shared.Common;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq(ctx.Configuration["Seq:Url"] ?? "http://localhost:5341"));

// ─── SignalR ─────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── CORS (SignalR için) ─────────────────────────────────────────
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

// ─── MassTransit + RabbitMQ ───────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<LowStockAlertConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("notification-order-created", e => e.ConfigureConsumer<OrderCreatedConsumer>(ctx));
        cfg.ReceiveEndpoint("notification-low-stock",     e => e.ConfigureConsumer<LowStockAlertConsumer>(ctx));
    });
});

// ─── Email Service ────────────────────────────────────────────────
builder.Services.AddSingleton<EmailService>();

// ─── Health Checks ────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── Controllers ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ─── Middleware pipeline ───────────────────────────────────────────
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseCors();
app.MapHub<StockHub>("/hubs/stock");
app.MapHealthChecks("/health");

await app.RunAsync();
