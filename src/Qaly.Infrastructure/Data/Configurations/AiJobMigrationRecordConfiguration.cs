using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiJobMigrationRecordConfiguration : IEntityTypeConfiguration<AiJobMigrationRecord>
{
    public void Configure(EntityTypeBuilder<AiJobMigrationRecord> builder)
    {
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Classification).HasMaxLength(80).IsRequired();
        builder.Property(record => record.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(record => record.LegacyQueueItemId).IsUnique();
        builder.HasIndex(record => record.CanonicalAiJobId);
    }
}
