using FluentAssertions;
using ProductService.Domain.Entities;

namespace ProductService.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class ProductDomainTests
{
    [Fact]
    public void Create_ShouldRaiseProductCreatedDomainEvent()
    {
        // Arrange & Act
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 20);

        // Assert
        product.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "ProductCreatedDomainEvent");
        product.Name.Should().Be("Laptop");
        product.Sku.Should().Be("LPT-001");
        product.Stock.Should().Be(20);
        product.Price.Should().Be(1500m);
    }

    [Fact]
    public void DecreaseStock_ShouldReduceStockAndRaiseEvent()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 20);
        product.ClearDomainEvents();

        // Act
        product.DecreaseStock(5);

        // Assert
        product.Stock.Should().Be(15);
        product.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "StockDecreasedDomainEvent");
    }

    [Fact]
    public void DecreaseStock_WithInsufficientStock_ShouldThrow()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 5);

        // Act
        var act = () => product.DecreaseStock(10);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public void DecreaseStock_WithNegativeQuantity_ShouldThrow()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 20);

        // Act
        var act = () => product.DecreaseStock(-1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LowStock_ShouldBeTrueWhenStockBelowThreshold()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 5, lowStockThreshold: 10);

        // Assert
        product.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public void LowStock_ShouldBeFalseWhenStockAboveThreshold()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 20, lowStockThreshold: 10);

        // Assert
        product.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public void DecreaseStock_WithLowStock_ShouldAlsoRaiseLowStockAlertEvent()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 15, lowStockThreshold: 10);
        product.ClearDomainEvents();

        // Act
        product.DecreaseStock(6); // Stock becomes 9, below threshold

        // Assert
        product.Stock.Should().Be(9);
        product.IsLowStock.Should().BeTrue();
        product.DomainEvents.Should().Contain(e => e.GetType().Name == "LowStockAlertEvent");
    }

    [Fact]
    public void IncreaseStock_ShouldIncreaseStockAndRaiseEvent()
    {
        // Arrange
        var product = Product.Create("Laptop", "Gaming laptop", "LPT-001", 1500m, 20);
        product.ClearDomainEvents();

        // Act
        product.IncreaseStock(10);

        // Assert
        product.Stock.Should().Be(30);
        product.DomainEvents.Should().ContainSingle(e =>
            e.GetType().Name == "StockIncreasedDomainEvent");
    }
}
