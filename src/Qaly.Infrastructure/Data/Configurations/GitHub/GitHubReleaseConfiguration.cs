using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubReleaseConfiguration : IEntityTypeConfiguration<GitHubRelease>
{
    public void Configure(EntityTypeBuilder<GitHubRelease> builder)
    {
        builder.ToTable("GitHubReleases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TagName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(500);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.RepositoryConnectionId, x.ReleaseExternalId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.HasOne(x => x.RepositoryConnection)
            .WithMany(r => r.Releases)
            .HasForeignKey(x => x.RepositoryConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
