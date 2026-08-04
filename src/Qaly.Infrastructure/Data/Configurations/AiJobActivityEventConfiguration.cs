using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AiJobActivityEventConfiguration : IEntityTypeConfiguration<AiJobActivityEvent>
{
    public void Configure(EntityTypeBuilder<AiJobActivityEvent> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Stage).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(30).IsRequired();
        builder.Property(item => item.PublicLabel).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ReceiptLink).HasMaxLength(500);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(item => item.AiJob)
            .WithMany(job => job.ActivityEvents)
            .HasForeignKey(item => item.AiJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.AiJobId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.AiJobId, item.CreatedAt });
    }
}

