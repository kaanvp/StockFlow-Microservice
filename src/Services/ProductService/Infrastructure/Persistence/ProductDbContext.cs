using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using StockFlow.Shared.Domain;
using StockFlow.Shared.Outbox;
using System.Text.Json;

namespace ProductService.Infrastructure.Persistence;

public sealed class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockEvent> StockEvents => Set<StockEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // Write domain events to outbox before saving
        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().FullName!,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = DateTime.UtcNow
            };
            OutboxMessages.Add(outboxMessage);
        }

        // Write stock events before saving
        var stockEvents = ChangeTracker
            .Entries<Product>()
            .SelectMany(e => e.Entity.StockEvents)
            .ToList();

        foreach (var stockEvent in stockEvents)
        {
            StockEvents.Add(stockEvent);
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        // Clear domain events after persisting
        foreach (var entity in ChangeTracker.Entries<BaseEntity>())
            entity.Entity.ClearDomainEvents();

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Description).HasMaxLength(1000);
            builder.Property(p => p.Sku).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Price).HasPrecision(18, 2);
            builder.HasIndex(p => p.Sku).IsUnique();
        });

        modelBuilder.Entity<StockEvent>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.EventType).IsRequired().HasMaxLength(50);
            builder.Property(s => s.Description).HasMaxLength(500);
            builder.HasIndex(s => s.ProductId);
            builder.HasIndex(s => s.OccurredAt);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Type).IsRequired().HasMaxLength(500);
            builder.Property(o => o.Payload).IsRequired();
        });
    }
}
