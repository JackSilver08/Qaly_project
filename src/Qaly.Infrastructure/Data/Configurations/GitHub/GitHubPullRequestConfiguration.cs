using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubPullRequestConfiguration : IEntityTypeConfiguration<GitHubPullRequest>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequest> builder)
    {
        builder.ToTable("GitHubPullRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.State).HasMaxLength(20).IsRequired().HasDefaultValue("Open");
        builder.Property(x => x.AuthorLogin).HasMaxLength(255);
        builder.Property(x => x.HeadBranch).HasMaxLength(255);
        builder.Property(x => x.BaseBranch).HasMaxLength(255);
        builder.Property(x => x.MergedByLogin).HasMaxLength(255);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.RepositoryConnectionId, x.Number }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.State);

        builder.HasOne(x => x.RepositoryConnection)
            .WithMany(r => r.PullRequests)
            .HasForeignKey(x => x.RepositoryConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
