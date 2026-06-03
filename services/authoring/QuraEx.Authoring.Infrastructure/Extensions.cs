using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuraEx.BuildingBlocks.Messaging;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.Authoring.Infrastructure;

public static class Extensions
{
    public static IServiceCollection AddAuthoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        // EF Core with Aspire Npgsql integration + snake_case naming
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("postgres-authoring"),
                npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null));
            options.UseSnakeCaseNamingConvention();
        });

        // Register AppDbContext as base DbContext for BuildingBlocks behaviors (transaction, idempotency)
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Messaging
        services.AddQuraExMessaging<AppDbContext>(
            configuration,
            serviceName: "authoring",
            typeof(Extensions));

        // Idempotency retention (7-day default)
        services.AddProcessedMessageRetention<AppDbContext>();

        // Dev-only: auto-apply migrations on startup
        // Hard-guarded to IsDevelopment — prod uses dedicated migration job
        if (env.IsDevelopment())
        {
            services.AddHostedService<MigrationHostedService>();
        }

        return services;
    }
}

/// <summary>Dev-only migration runner with pg_advisory_lock to prevent concurrent migration race.</summary>
internal sealed class MigrationHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // pg_advisory_lock serializes migrations across replicas booting concurrently
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_lock(hashtext('authoring_migration'))", stoppingToken);
        try
        {
            await db.Database.MigrateAsync(stoppingToken);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_unlock(hashtext('authoring_migration'))", stoppingToken);
        }
    }
}
