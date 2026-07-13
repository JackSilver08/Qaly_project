using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiJobSourceConfiguration : IEntityTypeConfiguration<AiJobSource>
{
    public void Configure(EntityTypeBuilder<AiJobSource> builder)
    {
        builder.HasKey(source => source.Id);
        builder.Property(source => source.SourceType).HasMaxLength(80).IsRequired();
        builder.Property(source => source.LegacySourceKey).HasMaxLength(200);
        builder.Property(source => source.SourceVersion).HasMaxLength(120);
        builder.Property(source => source.SourceHash).HasMaxLength(64);

        builder.HasOne(source => source.AiJob)
            .WithMany(job => job.Sources)
            .HasForeignKey(source => source.AiJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(source => new { source.AiJobId, source.SortOrder }).IsUnique();
        builder.HasIndex(source => new { source.SourceType, source.SourceEntityId });
    }
}
