using MassTransit;
using OrderService.Domain.Entities;
using StockFlow.Contracts.Events;
using StockFlow.Contracts.Grpc;

namespace OrderService.Infrastructure.Messaging;

public sealed class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    // States
    public State Pending { get; private set; } = null!;
    public State StockReserved { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // Events
    public Event<OrderCreatedEvent> OrderCreated { get; private set; } = null!;
    public Event<StockReservedEvent> StockReservedEvent { get; private set; } = null!;
    public Event<StockReservationFailedEvent> StockReservationFailed { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => StockReservedEvent, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => StockReservationFailed, e => e.CorrelateById(ctx => ctx.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.ProductId = ctx.Message.ProductId;
                    ctx.Saga.Quantity = ctx.Message.Quantity;
                    ctx.Saga.TotalPrice = ctx.Message.TotalPrice;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.StockReserved = false;
                })
                .TransitionTo(Pending)
                .Publish(ctx => new CheckStockCommand(
                    ctx.Message.OrderId,
                    ctx.Message.ProductId,
                    ctx.Message.Quantity))
        );

        During(Pending,
            When(StockReservedEvent)
                .Then(ctx =>
                {
                    ctx.Saga.StockReserved = true;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .TransitionTo(Completed)
                .Publish(ctx => new OrderConfirmedEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail)),

            When(StockReservationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
                .Publish(ctx => new OrderCompensationEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.ProductId,
                    ctx.Saga.Quantity))
        );

        SetCompletedWhenFinalized();
    }
}
