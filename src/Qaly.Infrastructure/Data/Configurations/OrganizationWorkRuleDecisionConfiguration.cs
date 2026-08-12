using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class OrganizationWorkRuleDecisionConfiguration : IEntityTypeConfiguration<OrganizationWorkRuleDecision>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkRuleDecision> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Result).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Severity).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Explanation).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.SourceFreshness).HasMaxLength(120).IsRequired();
        builder.HasOne(item => item.ProjectLaunchBrief).WithMany(item => item.RuleDecisions).HasForeignKey(item => item.ProjectLaunchBriefId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ProjectLaunchBriefId, item.RuleKey }).IsUnique();
    }
}
