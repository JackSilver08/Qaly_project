using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class VectorSyncOutboxConfiguration : IEntityTypeConfiguration<VectorSyncOutbox>
{
    public void Configure(EntityTypeBuilder<VectorSyncOutbox> builder)
    {
        builder.ToTable("VectorSyncOutbox");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.SequenceNumber)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEXT VALUE FOR [VectorSyncOutboxSequence]");
        builder.Property(item => item.EventType).HasMaxLength(64).IsRequired();
        builder.Property(item => item.AggregateType).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Payload).IsRequired();
        builder.Property(item => item.ErrorMessage).HasMaxLength(2000);
        // The owner participates in EF optimistic concurrency so a stale worker
        // cannot mark a reclaimed event complete.
        builder.Property(item => item.LeaseOwner).HasMaxLength(200).IsConcurrencyToken();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(item => item.NextAttemptAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(item => item.SequenceNumber).IsUnique();
        builder.HasIndex(item => new
        {
            item.ProcessedAt,
            item.DeadLetteredAt,
            item.NextAttemptAt,
            item.LeaseExpiresAt,
            item.SequenceNumber
        });
        builder.HasIndex(item => new { item.AggregateType, item.AggregateId, item.SequenceNumber });
    }
}
