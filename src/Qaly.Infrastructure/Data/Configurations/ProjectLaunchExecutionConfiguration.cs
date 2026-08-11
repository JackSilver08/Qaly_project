using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectLaunchExecutionConfiguration : IEntityTypeConfiguration<ProjectLaunchExecution>
{
    public void Configure(EntityTypeBuilder<ProjectLaunchExecution> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(item => item.RollbackReason).HasMaxLength(1000);
        builder.Property(item => item.RollbackIdempotencyKey).HasMaxLength(200);
        builder.Property(item => item.RollbackPayloadHash).HasMaxLength(128);
        builder.Property(item => item.ReceiptJson).IsRequired();
        builder.HasOne(item => item.ProjectLaunchPlanArtifact).WithMany(item => item.Executions).HasForeignKey(item => item.ProjectLaunchPlanArtifactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ProjectLaunchPlanArtifactId).IsUnique();
        builder.HasIndex(item => item.IdempotencyKey).IsUnique();
        builder.HasIndex(item => item.RollbackIdempotencyKey).IsUnique().HasFilter("[RollbackIdempotencyKey] IS NOT NULL");
        builder.HasIndex(item => new { item.MonitoringEnabled, item.NextMonitorAt });
    }
}
