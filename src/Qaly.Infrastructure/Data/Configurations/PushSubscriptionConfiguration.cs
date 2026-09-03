using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(item => item.Endpoint).IsRequired().HasMaxLength(2048);
        builder.Property(item => item.P256dh).IsRequired().HasMaxLength(1024);
        builder.Property(item => item.Auth).IsRequired().HasMaxLength(1024);
        builder.Property(item => item.Device).IsRequired().HasMaxLength(200);
        builder.Property<byte[]>("EndpointHash")
            .HasColumnType("binary(32)")
            .HasComputedColumnSql("CONVERT(binary(32), HASHBYTES('SHA2_256', [Endpoint]))", stored: true);
        builder.HasIndex("EndpointHash")
            .IsUnique()
            .HasFilter(null);
    }
}
