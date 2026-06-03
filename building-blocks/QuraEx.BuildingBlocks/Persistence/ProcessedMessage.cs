using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QuraEx.BuildingBlocks.Persistence;

/// <summary>Idempotency record written by each consumer on first processing.
/// Re-delivery check: consumer queries this table before processing — skip if row exists.</summary>
public class ProcessedMessage
{
    /// <summary>Gets the broker message id used as the dedup key.</summary>
    public Guid MessageId { get; private init; }
    public DateTime ProcessedAt { get; private init; } = DateTime.UtcNow;

    protected ProcessedMessage() { }

    public static ProcessedMessage Create(Guid messageId) => new() { MessageId = messageId };
}

/// <summary>Background service that prunes processed_message rows older than the configured retention window.
/// Register per service: services.AddProcessedMessageRetention&lt;TDbContext&gt;()
/// Retention window default = 7 days (broker redelivery window well under 7d).</summary>
public sealed class ProcessedMessageRetentionJob<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessedMessageRetentionJob<TDbContext>> logger,
    TimeSpan? retentionWindow = null)
    : BackgroundService
    where TDbContext : DbContext
{
    private readonly TimeSpan _retentionWindow = retentionWindow ?? TimeSpan.FromDays(7);
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);
            await PruneAsync(stoppingToken);
        }
    }

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var cutoff = DateTime.UtcNow - _retentionWindow;

            var deleted = await db.Set<ProcessedMessage>()
                .Where(m => m.ProcessedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Pruned {Count} processed_message rows older than {Cutoff:O}",
                    deleted,
                    cutoff);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Error during processed_message pruning — will retry in {Interval}", _interval);
        }
    }
}

public static class ProcessedMessageRetentionExtensions
{
    public static IServiceCollection AddProcessedMessageRetention<TDbContext>(
        this IServiceCollection services,
        TimeSpan? retentionWindow = null)
        where TDbContext : DbContext
    {
        services.AddHostedService(sp =>
            new ProcessedMessageRetentionJob<TDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<ProcessedMessageRetentionJob<TDbContext>>>(),
                retentionWindow));
        return services;
    }
}
