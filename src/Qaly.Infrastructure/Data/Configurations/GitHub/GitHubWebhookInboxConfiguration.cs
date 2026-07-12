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
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        builder.Property(x => x.LastError).HasMaxLength(2000);
        // Payload can be large; keep as nvarchar(max).
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Idempotency / anti-replay guarantee.
        builder.HasIndex(x => x.DeliveryId).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
