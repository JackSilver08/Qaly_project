using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class PrivacyRetentionActionConfiguration : IEntityTypeConfiguration<PrivacyRetentionAction>
{
    public void Configure(EntityTypeBuilder<PrivacyRetentionAction> builder)
    {
        builder.HasKey(action => action.Id);
        builder.Property(action => action.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(action => action.ActionType).HasMaxLength(30).IsRequired();
        builder.Property(action => action.Status).HasMaxLength(30).IsRequired();
        builder.Property(action => action.LeaseOwner).HasMaxLength(200);
        builder.Property(action => action.LastErrorCode).HasMaxLength(100);
        builder.Property(action => action.AvailableAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(action => action.MaxAttempts).HasDefaultValue(5);
        builder.Property(action => action.RowVersion).IsRowVersion();

        builder.HasOne(action => action.RetentionPolicy)
            .WithMany(policy => policy.RetentionActions)
            .HasForeignKey(action => action.RetentionPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(action => new { action.Status, action.AvailableAt, action.LeaseExpiresAt });
        builder.HasIndex(action => new { action.TenantId, action.ProjectId, action.DueAt });
        builder.HasIndex(action => new { action.EntityType, action.EntityId, action.ActionType }).IsUnique();
    }
}
