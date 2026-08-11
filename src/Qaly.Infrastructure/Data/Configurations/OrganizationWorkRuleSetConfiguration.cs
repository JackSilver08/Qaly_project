using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class OrganizationWorkRuleSetConfiguration : IEntityTypeConfiguration<OrganizationWorkRuleSet>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkRuleSet> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.RulesJson).IsRequired();
        builder.Property(item => item.Revision).IsConcurrencyToken();
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.OrganizationId, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.Status, item.EffectiveFrom });
    }
}
