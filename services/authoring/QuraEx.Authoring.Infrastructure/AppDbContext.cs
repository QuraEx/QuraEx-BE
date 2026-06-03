using Microsoft.EntityFrameworkCore;
using QuraEx.Authoring.Domain.Entities;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.Authoring.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserStory> UserStories => Set<UserStory>();
    public DbSet<AcceptanceCriteria> AcceptanceCriteria => Set<AcceptanceCriteria>();
    public DbSet<BusinessRule> BusinessRules => Set<BusinessRule>();
    public DbSet<MembershipSnapshot> MembershipSnapshots => Set<MembershipSnapshot>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // DomainEvent is in-process only — never persisted; prevent EF from treating it as an entity
        modelBuilder.Ignore<QuraEx.BuildingBlocks.DomainEvent>();
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyQuraExConventions();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.ApplyQuraExConventions();
    }
}
