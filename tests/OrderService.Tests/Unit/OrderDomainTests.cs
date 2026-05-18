using FluentAssertions;
using OrderService.Domain.Entities;

namespace OrderService.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class OrderDomainTests
{
    [Fact]
    public void Create_ShouldRaiseOrderCreatedDomainEvent()
    {
        // Arrange & Act
        var order = Order.Create("customer@test.com", Guid.NewGuid(), "Laptop", 2, 1500m);

        // Assert
        order.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "OrderCreatedDomainEvent");
        order.CustomerEmail.Should().Be("customer@test.com");
        order.Quantity.Should().Be(2);
        order.TotalPrice.Should().Be(3000m);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Confirm_ShouldSetStatusToConfirmed()
    {
        // Arrange
        var order = Order.Create("customer@test.com", Guid.NewGuid(), "Laptop", 1, 1000m);

        // Act
        order.Confirm();

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_ShouldThrow()
    {
        // Arrange
        var order = Order.Create("customer@test.com", Guid.NewGuid(), "Laptop", 1, 1000m);
        order.Confirm();

        // Act
        var act = () => order.Confirm();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot confirm*");
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelledAndRaiseEvent()
    {
        // Arrange
        var order = Order.Create("customer@test.com", Guid.NewGuid(), "Laptop", 1, 1000m);
        order.ClearDomainEvents();

        // Act
        order.Cancel("Customer changed mind");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("Customer changed mind");
        order.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "OrderCancelledDomainEvent");
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        // Arrange
        var order = Order.Create("customer@test.com", Guid.NewGuid(), "Laptop", 1, 1000m);
        order.Cancel("Some reason");

        // Act
        var act = () => order.Cancel("Another reason");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }
}
