using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiJobConfiguration : IEntityTypeConfiguration<AiJob>
{
    public void Configure(EntityTypeBuilder<AiJob> builder)
    {
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasDefaultValueSql("NEWID()");

        builder.Property(job => job.JobType).HasMaxLength(80).IsRequired();
        builder.Property(job => job.SourceType).HasMaxLength(80).IsRequired();
        builder.Property(job => job.SourceId).HasMaxLength(200);
        builder.Property(job => job.ProviderHint).HasMaxLength(40).IsRequired();
        builder.Property(job => job.Status).HasMaxLength(40).IsRequired();
        builder.Property(job => job.CacheKey).HasMaxLength(200).IsRequired();
        builder.Property(job => job.EstimatedCostUsd).HasPrecision(18, 6);
        builder.Property(job => job.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(job => job.Project)
            .WithMany(project => project.AiJobs)
            .HasForeignKey(job => job.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(job => job.RequestedBy)
            .WithMany(user => user.RequestedAiJobs)
            .HasForeignKey(job => job.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(job => new { job.ProjectId, job.CreatedAt });
        builder.HasIndex(job => job.CacheKey);
    }
}
