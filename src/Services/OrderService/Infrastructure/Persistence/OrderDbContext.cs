using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using StockFlow.Shared.Domain;
using StockFlow.Shared.Outbox;
using System.Text.Json;

namespace OrderService.Infrastructure.Persistence;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

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

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in ChangeTracker.Entries<BaseEntity>())
            entity.Entity.ClearDomainEvents();

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(200);
            builder.Property(o => o.ProductName).IsRequired().HasMaxLength(200);
            builder.Property(o => o.UnitPrice).HasPrecision(18, 2);
            builder.Property(o => o.TotalPrice).HasPrecision(18, 2);
            builder.Property(o => o.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);
            builder.Property(o => o.CancelReason).HasMaxLength(500);
            builder.HasIndex(o => o.CustomerEmail);
            builder.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Type).IsRequired().HasMaxLength(500);
            builder.Property(o => o.Payload).IsRequired();
        });
    }
}
