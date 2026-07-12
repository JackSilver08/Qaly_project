using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubPullRequestReviewConfiguration : IEntityTypeConfiguration<GitHubPullRequestReview>
{
    public void Configure(EntityTypeBuilder<GitHubPullRequestReview> builder)
    {
        builder.ToTable("GitHubPullRequestReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.ReviewerLogin).HasMaxLength(255);
        builder.Property(x => x.State).HasMaxLength(20).IsRequired().HasDefaultValue("Commented");
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.PullRequestId, x.ReviewExternalId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.HasOne(x => x.PullRequest)
            .WithMany(p => p.Reviews)
            .HasForeignKey(x => x.PullRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
