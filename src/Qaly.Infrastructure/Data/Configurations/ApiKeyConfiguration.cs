using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name).IsRequired().HasMaxLength(100);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(200);
        builder.Property(k => k.Prefix).IsRequired().HasMaxLength(16);
        builder.Property(k => k.Scopes).HasMaxLength(1000);

        // Index on Prefix for fast lookup (avoid full table scan)
        builder.HasIndex(k => k.Prefix);

        // Index on UserId for listing user's keys
        builder.HasIndex(k => k.UserId);

        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
