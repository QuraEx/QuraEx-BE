using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuraEx.Authoring.Domain.Entities;

namespace QuraEx.Authoring.Infrastructure.EntityConfigurations;

internal sealed class BusinessRuleConfiguration : IEntityTypeConfiguration<BusinessRule>
{
    public void Configure(EntityTypeBuilder<BusinessRule> builder)
    {
        builder.ToTable("business_rule");
        builder.HasKey(x => x.Id);
    }
}
