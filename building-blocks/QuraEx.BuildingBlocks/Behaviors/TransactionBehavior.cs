using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace QuraEx.BuildingBlocks.Behaviors;

/// <summary>Wraps the handler and any domain-event dispatching in a single EF Core transaction.
/// Requires the service to register its DbContext both as the concrete type AND as DbContext:
///   services.AddScoped&lt;DbContext&gt;(sp => sp.GetRequiredService&lt;TServiceDbContext&gt;()).
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    DbContext dbContext,
    IPublisher publisher,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (dbContext.Database.CurrentTransaction is not null)
            {
                // Already inside a transaction — just continue
                return await next();
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            logger.LogDebug("Started transaction {TransactionId}", transaction.TransactionId);

            try
            {
                var response = await next();

                // Dispatch domain events within the same transaction before committing
                await DispatchDomainEventsAsync(cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogDebug("Committed transaction {TransactionId}", transaction.TransactionId);
                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogDebug(ex, "Rolled back transaction {TransactionId}", transaction.TransactionId);
                throw;
            }
        });
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.Entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
