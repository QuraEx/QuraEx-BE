using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.Authoring.Domain.Entities;

namespace QuraEx.Authoring.Infrastructure.EntityConfigurations;

internal sealed class AcceptanceCriteriaConfiguration : IEntityTypeConfiguration<AcceptanceCriteria>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriteria> builder)
    {
        builder.ToTable("acceptance_criteria");
        builder.HasKey(x => x.Id);

        builder.HasOne<AcceptanceCriteria>()
               .WithMany()
               .HasForeignKey(x => x.ParentId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
