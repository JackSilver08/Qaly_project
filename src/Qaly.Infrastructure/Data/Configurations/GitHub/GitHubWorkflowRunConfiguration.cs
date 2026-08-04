using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public sealed class GitHubWorkflowRunConfiguration : IEntityTypeConfiguration<GitHubWorkflowRun>
{
    public void Configure(EntityTypeBuilder<GitHubWorkflowRun> builder)
    {
        builder.ToTable("GitHubWorkflowRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");
        builder.Property(x => x.WorkflowName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.DisplayTitle).HasMaxLength(500);
        builder.Property(x => x.Branch).HasMaxLength(255);
        builder.Property(x => x.CommitSha).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Conclusion).HasMaxLength(40);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.HasIndex(x => new { x.RepositoryConnectionId, x.RunExternalId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => new { x.Status, x.Conclusion });
        builder.HasOne(x => x.RepositoryConnection).WithMany(x => x.WorkflowRuns)
            .HasForeignKey(x => x.RepositoryConnectionId).OnDelete(DeleteBehavior.Cascade);
    }
}
