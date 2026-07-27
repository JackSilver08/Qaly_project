using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiBudgetPolicyConfiguration : IEntityTypeConfiguration<AiBudgetPolicy>
{
    public void Configure(EntityTypeBuilder<AiBudgetPolicy> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DailyBudgetUsd).HasPrecision(18, 2);
        builder.Property(item => item.MonthlyBudgetUsd).HasPrecision(18, 2);
        builder.Property(item => item.UpdatedAt).IsConcurrencyToken();

        builder.HasIndex(item => item.ProjectId)
            .IsUnique()
            .HasFilter("[ProjectId] IS NOT NULL");
        builder.HasIndex(item => item.TenantId)
            .IsUnique()
            .HasFilter("[ProjectId] IS NULL AND [TenantId] IS NOT NULL");
    }
}
