using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class PrivacyConsentConfiguration : IEntityTypeConfiguration<PrivacyConsent>
{
    public void Configure(EntityTypeBuilder<PrivacyConsent> builder)
    {
        builder.HasKey(consent => consent.Id);
        builder.Property(consent => consent.SourceType).HasMaxLength(50).HasDefaultValue("legacy").IsRequired();
        builder.Property(consent => consent.ProviderClass).HasMaxLength(30).HasDefaultValue(PrivacyProviderClasses.Unknown).IsRequired();
        builder.Property(consent => consent.PolicyVersion).HasMaxLength(80).HasDefaultValue("legacy-unknown").IsRequired();
        builder.Property(consent => consent.NoticeVersion).HasMaxLength(80).HasDefaultValue("legacy-unknown").IsRequired();
        builder.Property(consent => consent.RequestId).HasMaxLength(120);
        builder.Property(consent => consent.RowVersion).IsRowVersion();

        builder.HasOne(consent => consent.RetentionPolicy)
            .WithMany(policy => policy.Consents)
            .HasForeignKey(consent => consent.RetentionPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(consent => new { consent.TenantId, consent.ProjectId, consent.UserId });
        builder.HasIndex(consent => consent.RetentionPolicyId);
        builder.HasIndex(consent => new { consent.SourceType, consent.SourceEntityId });
    }
}
