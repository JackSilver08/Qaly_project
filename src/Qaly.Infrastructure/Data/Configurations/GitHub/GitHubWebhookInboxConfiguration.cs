using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubWebhookInboxConfiguration : IEntityTypeConfiguration<GitHubWebhookInbox>
{
    public void Configure(EntityTypeBuilder<GitHubWebhookInbox> builder)
    {
        builder.ToTable("GitHubWebhookInbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.DeliveryId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired()
            .HasDefaultValue(GitHubWebhookInboxStatuses.Pending);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        // The owner is the compare-and-swap token used when the processor commits
        // canonical side effects and the terminal inbox state in one SaveChanges.
        builder.Property(x => x.LeaseOwner).HasMaxLength(200).IsConcurrencyToken();
        // Payload can be large; keep as nvarchar(max).
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(x => x.NextAttemptAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Idempotency / anti-replay guarantee.
        builder.HasIndex(x => x.DeliveryId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LeaseExpiresAt, x.ReceivedAt });
    }
}
