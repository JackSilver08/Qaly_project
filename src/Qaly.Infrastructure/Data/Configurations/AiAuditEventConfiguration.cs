using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiAuditEventConfiguration : IEntityTypeConfiguration<AiAuditEvent>
{
    public void Configure(EntityTypeBuilder<AiAuditEvent> builder)
    {
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.EntityKey).HasMaxLength(200);
        builder.Property(audit => audit.Purpose).HasMaxLength(80);
        builder.Property(audit => audit.PolicyVersion).HasMaxLength(80);
        builder.Property(audit => audit.DataClassification).HasMaxLength(50);
        builder.Property(audit => audit.ProviderClass).HasMaxLength(30);
        builder.Property(audit => audit.Outcome).HasMaxLength(40);
        builder.Property(audit => audit.FailureCode).HasMaxLength(100);
        builder.Property(audit => audit.RequestId).HasMaxLength(120);

        builder.HasIndex(audit => new { audit.AiJobId, audit.CreatedAt });
        builder.HasIndex(audit => audit.ProviderAttemptId);
        builder.HasIndex(audit => audit.PrivacyConsentId);
        builder.HasIndex(audit => audit.RetentionPolicyId);
        builder.HasIndex(audit => audit.DataSubjectRequestId);
    }
}
