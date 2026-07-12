using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubRepositoryConnectionConfiguration : IEntityTypeConfiguration<GitHubRepositoryConnection>
{
    public void Configure(EntityTypeBuilder<GitHubRepositoryConnection> builder)
    {
        builder.ToTable("GitHubRepositoryConnections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Owner).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.DefaultBranch).HasMaxLength(255).IsRequired().HasDefaultValue("main");
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.ProjectId, x.RepositoryExternalId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.GitHubInstallationId);

        // OrganizationId kept as a plain tenant-scoped column (indexed), enforced
        // at the data-access layer. The org relationship is owned via Installation.
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Installation)
            .WithMany(i => i.RepositoryConnections)
            .HasForeignKey(x => x.GitHubInstallationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
