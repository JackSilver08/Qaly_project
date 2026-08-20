using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProjectDigestSubscriptionConfiguration : IEntityTypeConfiguration<ProjectDigestSubscription>
{
    public void Configure(EntityTypeBuilder<ProjectDigestSubscription> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Cadence).HasMaxLength(20).IsRequired();
        builder.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.LastDeliveryStatus).HasMaxLength(30).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(1000);
        builder.Property(item => item.LastDeliveryKey).HasMaxLength(160);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.UserId, item.ProjectId }).IsUnique();
        builder.HasIndex(item => new { item.IsEnabled, item.NextDeliveryAt });
    }
}
