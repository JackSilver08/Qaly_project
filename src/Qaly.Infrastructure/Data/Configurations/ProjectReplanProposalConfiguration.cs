using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectReplanProposalConfiguration : IEntityTypeConfiguration<ProjectReplanProposal>
{
    public void Configure(EntityTypeBuilder<ProjectReplanProposal> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.State).HasMaxLength(40).IsRequired();
        builder.Property(item => item.BaselineHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.CurrentHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.TriggerCodesJson).IsRequired();
        builder.Property(item => item.ProposalJson).IsRequired();
        builder.Property(item => item.SourceSnapshotJson).IsRequired();
        builder.HasOne(item => item.ProjectLaunchPlanArtifact).WithMany(item => item.ReplanProposals).HasForeignKey(item => item.ProjectLaunchPlanArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectLaunchExecution).WithMany(item => item.ReplanProposals).HasForeignKey(item => item.ProjectLaunchExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ProjectLaunchPlanArtifactId, item.Revision }).IsUnique();
        builder.HasIndex(item => new { item.ProjectLaunchExecutionId, item.CreatedAt });
    }
}
