using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.BuildingBlocks.Messaging;

/// <summary>MassTransit consume filter that deduplicates redelivered messages using the processed_message table.
/// Registered per receive endpoint via: cfg.UseConsumeFilter(typeof(IdempotentConsumerFilter&lt;&gt;), context)
///
/// Requires the service's DbContext to be registered as DbContext in DI:
///   services.AddScoped&lt;DbContext&gt;(sp => sp.GetRequiredService&lt;TServiceDbContext&gt;()).
/// </summary>
public sealed class IdempotentConsumerFilter<T>(
    DbContext dbContext,
    ILogger<IdempotentConsumerFilter<T>> logger)
    : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();

        var alreadyProcessed = await dbContext.Set<ProcessedMessage>()
            .AnyAsync(m => m.MessageId == messageId, context.CancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Skipping duplicate message {MessageId} ({MessageType})",
                messageId,
                typeof(T).Name);
            return;
        }

        await next.Send(context);

        // Write idempotency record after successful processing (same transaction as handler via EF)
        dbContext.Set<ProcessedMessage>().Add(ProcessedMessage.Create(messageId));
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("idempotent-consumer");
}
