using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class WikiPageConfiguration : IEntityTypeConfiguration<WikiPage>
{
    public void Configure(EntityTypeBuilder<WikiPage> builder)
    {
        builder.Property(page => page.IsPublic).HasDefaultValue(false);
        builder.Property(page => page.Visibility).HasMaxLength(50).HasDefaultValue("internal");

        builder.HasIndex(page => new { page.ProjectId, page.Visibility });
    }
}
