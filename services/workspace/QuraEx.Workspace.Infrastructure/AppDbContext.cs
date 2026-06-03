using Microsoft.EntityFrameworkCore;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.Workspace.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // DomainEvent is in-process only — never persisted; prevent EF from treating it as an entity
        modelBuilder.Ignore<QuraEx.BuildingBlocks.DomainEvent>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyQuraExConventions();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.ApplyQuraExConventions();
    }
}
