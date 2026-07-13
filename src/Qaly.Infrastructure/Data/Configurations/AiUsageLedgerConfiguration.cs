using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiUsageLedgerConfiguration : IEntityTypeConfiguration<AiUsageLedger>
{
    public void Configure(EntityTypeBuilder<AiUsageLedger> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EstimatedCostUsd).HasPrecision(18, 6);
        builder.Property(entry => entry.ActualCostUsd).HasPrecision(18, 6);
        builder.Property(entry => entry.PricingVersion).HasMaxLength(80);

        builder.HasOne(entry => entry.AiJob)
            .WithMany(job => job.UsageEntries)
            .HasForeignKey(entry => entry.AiJobId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(entry => entry.ProviderAttempt)
            .WithMany(attempt => attempt.UsageEntries)
            .HasForeignKey(entry => entry.ProviderAttemptId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(entry => new { entry.AiJobId, entry.ProviderAttemptId });
    }
}
