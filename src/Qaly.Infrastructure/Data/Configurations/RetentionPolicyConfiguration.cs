using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Name).HasMaxLength(120).IsRequired();
        builder.Property(policy => policy.DataClassification).HasMaxLength(50).IsRequired();
        builder.Property(policy => policy.Purpose).HasMaxLength(80).IsRequired();
        builder.Property(policy => policy.AllowedRetentionDaysJson).HasMaxLength(200).IsRequired();
        builder.Property(policy => policy.ExpiryAction).HasMaxLength(30).IsRequired();
        builder.Property(policy => policy.LegalHoldBehavior).HasMaxLength(40).IsRequired();
        builder.Property(policy => policy.PolicyVersion).HasMaxLength(80).IsRequired();
        builder.Property(policy => policy.RowVersion).IsRowVersion();

        builder.HasOne(policy => policy.Project)
            .WithMany()
            .HasForeignKey(policy => policy.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(policy => new
        {
            policy.TenantId,
            policy.ProjectId,
            policy.DataClassification,
            policy.Purpose,
            policy.IsActive
        });
        builder.HasIndex(policy => new { policy.TenantId, policy.PolicyVersion }).IsUnique();
    }
}
