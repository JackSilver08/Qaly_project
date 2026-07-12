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
        builder.Property(job => job.SchemaId).HasMaxLength(120).IsRequired();
        builder.Property(job => job.SchemaVersion).HasMaxLength(40).IsRequired();
        builder.Property(job => job.RequestJson).IsRequired();
        builder.Property(job => job.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(job => job.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(job => job.ProviderHint).HasMaxLength(40).IsRequired();
        builder.Property(job => job.Status).HasMaxLength(40).IsRequired();
        builder.Property(job => job.LegacyStatus).HasMaxLength(40);
        builder.Property(job => job.LastErrorCode).HasMaxLength(80);
        builder.Property(job => job.LastErrorMessage).HasMaxLength(2000);
        builder.Property(job => job.ResultHash).HasMaxLength(64);
        builder.Property(job => job.SelectedProvider).HasMaxLength(80);
        builder.Property(job => job.SelectedModel).HasMaxLength(160);
        builder.Property(job => job.ProviderRequestId).HasMaxLength(200);
        builder.Property(job => job.PricingVersion).HasMaxLength(80);
        builder.Property(job => job.MockReason).HasMaxLength(200);
        builder.Property(job => job.CacheKey).HasMaxLength(200).IsRequired();
        builder.Property(job => job.EstimatedCostUsd).HasPrecision(18, 6);
        builder.Property(job => job.ActualCostUsd).HasPrecision(18, 6);
        builder.Property(job => job.MaximumCostUsd).HasPrecision(18, 6);
        builder.Property(job => job.RowVersion).IsRowVersion();
        builder.Property(job => job.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(job => job.Project)
            .WithMany(project => project.AiJobs)
            .HasForeignKey(job => job.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(job => job.RequestedBy)
            .WithMany(user => user.RequestedAiJobs)
            .HasForeignKey(job => job.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(job => new { job.ProjectId, job.CreatedAt });
        builder.HasIndex(job => new { job.Status, job.AvailableAt });
        builder.HasIndex(job => new { job.RequestedById, job.IdempotencyKey }).IsUnique();
        builder.HasIndex(job => job.CacheKey);
        builder.HasIndex(job => job.LegacyQueueItemId)
            .IsUnique()
            .HasFilter("[LegacyQueueItemId] IS NOT NULL");
    }
}
