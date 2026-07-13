using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiProviderAttemptConfiguration : IEntityTypeConfiguration<AiProviderAttempt>
{
    public void Configure(EntityTypeBuilder<AiProviderAttempt> builder)
    {
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Status).HasMaxLength(40).IsRequired();
        builder.Property(attempt => attempt.ProviderName).HasMaxLength(80).IsRequired();
        builder.Property(attempt => attempt.ModelName).HasMaxLength(160).IsRequired();
        builder.Property(attempt => attempt.ProviderRequestId).HasMaxLength(200);
        builder.Property(attempt => attempt.EstimatedCostUsd).HasPrecision(18, 6);
        builder.Property(attempt => attempt.ActualCostUsd).HasPrecision(18, 6);
        builder.Property(attempt => attempt.MockReason).HasMaxLength(200);
        builder.Property(attempt => attempt.RequestHash).HasMaxLength(64);
        builder.Property(attempt => attempt.ResponseHash).HasMaxLength(64);
        builder.Property(attempt => attempt.ErrorCode).HasMaxLength(80);
        builder.Property(attempt => attempt.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(attempt => attempt.AiJob)
            .WithMany(job => job.ProviderAttempts)
            .HasForeignKey(attempt => attempt.AiJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attempt => new { attempt.AiJobId, attempt.AttemptNumber }).IsUnique();
    }
}
