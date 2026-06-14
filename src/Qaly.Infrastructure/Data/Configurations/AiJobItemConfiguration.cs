using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiJobItemConfiguration : IEntityTypeConfiguration<AiJobItem>
{
    public void Configure(EntityTypeBuilder<AiJobItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EstimatedCostUsd).HasPrecision(18, 2);
    }
}
