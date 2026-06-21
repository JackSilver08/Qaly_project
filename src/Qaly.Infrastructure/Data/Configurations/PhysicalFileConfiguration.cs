using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class PhysicalFileConfiguration : IEntityTypeConfiguration<PhysicalFile>
{
    public void Configure(EntityTypeBuilder<PhysicalFile> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("NEWID()");
        builder.Property(f => f.ContentHash).HasMaxLength(256).IsRequired();
        builder.Property(f => f.FilePath).IsRequired();
        builder.Property(f => f.FileSize).IsRequired();
        builder.Property(f => f.ReferenceCount).HasDefaultValue(1);

        builder.HasIndex(f => f.ContentHash).IsUnique();
    }
}
