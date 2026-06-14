using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiProviderConfigConfiguration : IEntityTypeConfiguration<AiProviderConfig>
{
    public void Configure(EntityTypeBuilder<AiProviderConfig> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CostInputPer1MUsd).HasPrecision(18, 2);
        builder.Property(item => item.CostOutputPer1MUsd).HasPrecision(18, 2);
    }
}
