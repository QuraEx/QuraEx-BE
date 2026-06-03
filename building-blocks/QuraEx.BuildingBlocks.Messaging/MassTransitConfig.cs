using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QuraEx.BuildingBlocks.Messaging;

/// <summary>Convention-based MassTransit + RabbitMQ registration shared by all services.
///
/// Dead-letter: poison message exceeding retry budget lands in {queue}_error, never loops.
/// Retry: bounded exponential (5 attempts) → scheduled redelivery (3x) → dead-letter.
/// Transactional Outbox: MassTransit EF outbox relay (FOR UPDATE SKIP LOCKED).
/// Idempotent consumer: UseConsumeFilter applied after ConfigureEndpoints for all endpoints.</summary>
public static class MassTransitConfig
{
    /// <param name="serviceName">Queue/exchange name prefix (e.g. "authoring").</param>
    /// <param name="consumerAssemblyMarkers">Assembly markers for auto-discovered consumers.</param>
    /// <returns></returns>
    public static IServiceCollection AddQuraExMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        params Type[] consumerAssemblyMarkers)
        where TDbContext : DbContext
    {
        var rabbitMqSection = configuration.GetSection("RabbitMQ");

        services.AddMassTransit(bus =>
        {
            foreach (var marker in consumerAssemblyMarkers)
            {
                bus.AddConsumers(marker.Assembly);
            }

            // Transactional Outbox — EF relay with FOR UPDATE SKIP LOCKED dispatch
            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox(busOutbox =>
                {
                    busOutbox.MessageDeliveryLimit = 10;
                });
            });

            bus.SetEndpointNameFormatter(new DefaultEndpointNameFormatter(serviceName + "-", false));

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    rabbitMqSection["Host"] ?? "localhost",
                    rabbitMqSection["VirtualHost"] ?? "/",
                    h =>
                    {
                        h.Username(rabbitMqSection["Username"] ?? "guest");
                        h.Password(rabbitMqSection["Password"] ?? "guest");
                    });

                // Bounded exponential retry — prevents thundering herd on transient failures
                cfg.UseMessageRetry(r =>
                {
                    r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3));
                    r.Ignore<ArgumentException>();
                    r.Ignore<InvalidOperationException>();
                });

                // Scheduled redelivery for longer-lived transient failures
                cfg.UseScheduledRedelivery(r =>
                {
                    r.Intervals(
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(15),
                        TimeSpan.FromMinutes(30));
                });

                // Idempotent consumer filter — deduplicates redeliveries using processed_message table
                cfg.UseConsumeFilter(typeof(IdempotentConsumerFilter<>), context);

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
