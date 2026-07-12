using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubCommitConfiguration : IEntityTypeConfiguration<GitHubCommit>
{
    public void Configure(EntityTypeBuilder<GitHubCommit> builder)
    {
        builder.ToTable("GitHubCommits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Sha).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.AuthorLogin).HasMaxLength(255);
        builder.Property(x => x.AuthorEmailHash).HasMaxLength(128);
        builder.Property(x => x.BranchName).HasMaxLength(255);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.RepositoryConnectionId, x.Sha }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.HasOne(x => x.RepositoryConnection)
            .WithMany(r => r.Commits)
            .HasForeignKey(x => x.RepositoryConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
