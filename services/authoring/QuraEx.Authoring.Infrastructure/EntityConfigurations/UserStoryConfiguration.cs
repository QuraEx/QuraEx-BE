using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.Authoring.Domain.Entities;

namespace QuraEx.Authoring.Infrastructure.EntityConfigurations;

internal sealed class UserStoryConfiguration : IEntityTypeConfiguration<UserStory>
{
    public void Configure(EntityTypeBuilder<UserStory> builder)
    {
        builder.ToTable("user_story");
        builder.HasKey(x => x.Id);

        // xmin as optimistic concurrency token — zero extra column (Postgres system column)
        builder.HasAnnotation("Npgsql:UseXminAsConcurrencyToken", true);

        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExternalRef).HasMaxLength(100);

        // EF auto-discovers AcceptanceCriteria and BusinessRule relationships via FK convention.
        // Explicit HasMany with private fields causes conflict — FK-convention is sufficient here.
    }
}
