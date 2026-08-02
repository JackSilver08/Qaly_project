using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class MemberAvailabilityWindowConfiguration : IEntityTypeConfiguration<MemberAvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<MemberAvailabilityWindow> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Kind).HasMaxLength(32).IsRequired();
        builder.Property(item => item.AvailableHours).HasPrecision(6, 2);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.Profile)
            .WithMany(item => item.AvailabilityWindows)
            .HasForeignKey(item => item.OrganizationMemberCapacityProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.OrganizationMemberCapacityProfileId, item.StartsAt, item.EndsAt });
    }
}
