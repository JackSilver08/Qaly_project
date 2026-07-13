using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class PrivacyLegalHoldConfiguration : IEntityTypeConfiguration<PrivacyLegalHold>
{
    public void Configure(EntityTypeBuilder<PrivacyLegalHold> builder)
    {
        builder.HasKey(hold => hold.Id);
        builder.Property(hold => hold.EntityType).HasMaxLength(80);
        builder.Property(hold => hold.Status).HasMaxLength(30).IsRequired();
        builder.Property(hold => hold.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(hold => hold.RowVersion).IsRowVersion();

        builder.HasIndex(hold => new
        {
            hold.TenantId,
            hold.ProjectId,
            hold.SubjectUserId,
            hold.Status
        });
        builder.HasIndex(hold => new { hold.EntityType, hold.EntityId, hold.Status });
    }
}
