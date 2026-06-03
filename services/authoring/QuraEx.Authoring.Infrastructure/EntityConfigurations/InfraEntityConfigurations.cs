using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.Authoring.Infrastructure.EntityConfigurations;

// Infra tables: no ISoftDeletable, no audit columns, no soft-delete filter.

internal sealed class MembershipSnapshotConfiguration : IEntityTypeConfiguration<MembershipSnapshot>
{
    public void Configure(EntityTypeBuilder<MembershipSnapshot> builder)
    {
        builder.ToTable("membership_snapshot");
        builder.HasKey(x => new { x.ProjectId, x.UserId });
        builder.Property(x => x.Role).HasMaxLength(20).IsRequired();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("authoring_outbox_message");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Seq).UseIdentityByDefaultColumn();
        builder.Property(x => x.Type).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.HasIndex(x => x.ProcessedAt);
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("authoring_processed_message");
        builder.HasKey(x => x.MessageId);
        builder.HasIndex(x => x.ProcessedAt);
    }
}
