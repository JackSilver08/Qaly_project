using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectLaunchPlanArtifactConfiguration : IEntityTypeConfiguration<ProjectLaunchPlanArtifact>
{
    public void Configure(EntityTypeBuilder<ProjectLaunchPlanArtifact> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.State).HasMaxLength(40).IsRequired();
        builder.Property(item => item.SourceVersionHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.SelectedScenarioId).HasMaxLength(80);
        builder.Property(item => item.ScoringVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PromptVersion).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ActualProvider).HasMaxLength(80).IsRequired();
        builder.Property(item => item.ActualModel).HasMaxLength(120).IsRequired();
        builder.Property(item => item.StaffingScenariosJson).IsRequired();
        builder.Property(item => item.DeliveryPlanJson).IsRequired();
        builder.Property(item => item.BlockingReasonsJson).IsRequired();
        builder.Property(item => item.WarningsJson).IsRequired();
        builder.Property(item => item.SourceSnapshotJson).IsRequired();
        builder.HasOne(item => item.ProjectLaunchBrief).WithMany().HasForeignKey(item => item.ProjectLaunchBriefId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AssistantSession).WithMany().HasForeignKey(item => item.AssistantSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AssistantTurn).WithMany().HasForeignKey(item => item.AssistantTurnId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RuleSet).WithMany().HasForeignKey(item => item.RuleSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.AssistantTurnId).IsUnique();
        builder.HasIndex(item => new { item.ProjectLaunchBriefId, item.Revision }).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.CreatedAt });
    }
}
