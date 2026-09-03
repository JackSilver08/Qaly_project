using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectLaunchTaskTraceConfiguration : IEntityTypeConfiguration<ProjectLaunchTaskTrace>
{
    public void Configure(EntityTypeBuilder<ProjectLaunchTaskTrace> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.FeatureId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SprintClientId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.TaskClientId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ObjectiveMetricIdsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.SourceRefsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(item => item.TaskItem)
            .WithMany()
            .HasForeignKey(item => item.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ProjectLaunchBrief)
            .WithMany()
            .HasForeignKey(item => item.ProjectLaunchBriefId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.TaskItemId).IsUnique();
        builder.HasIndex(item => new { item.ProjectLaunchBriefId, item.FeatureId });
        builder.HasIndex(item => new { item.ProjectLaunchBriefId, item.TaskClientId }).IsUnique();
    }
}
