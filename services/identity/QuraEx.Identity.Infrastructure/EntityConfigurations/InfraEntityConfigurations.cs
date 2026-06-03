using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.BuildingBlocks.Persistence;

namespace QuraEx.Identity.Infrastructure.EntityConfigurations;

// Infra tables: no ISoftDeletable, no audit columns, no soft-delete filter.

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("identity_outbox_message");
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
        builder.ToTable("identity_processed_message");
        builder.HasKey(x => x.MessageId);
        builder.HasIndex(x => x.ProcessedAt);
    }
}
