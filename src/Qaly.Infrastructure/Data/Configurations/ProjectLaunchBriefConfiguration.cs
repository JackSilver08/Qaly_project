using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectLaunchBriefConfiguration : IEntityTypeConfiguration<ProjectLaunchBrief>
{
    public void Configure(EntityTypeBuilder<ProjectLaunchBrief> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.State).HasMaxLength(40).IsRequired();
        builder.Property(item => item.PromptVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ActualProvider).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ActualModel).HasMaxLength(120).IsRequired();
        builder.Property(item => item.BriefJson).IsRequired();
        builder.Property(item => item.SourceSnapshotJson).IsRequired();
        builder.HasOne(item => item.AssistantSession).WithMany().HasForeignKey(item => item.AssistantSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AssistantTurn).WithMany().HasForeignKey(item => item.AssistantTurnId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RuleSet).WithMany(item => item.LaunchBriefs).HasForeignKey(item => item.RuleSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.AssistantTurnId).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.CreatedAt });
    }
}
