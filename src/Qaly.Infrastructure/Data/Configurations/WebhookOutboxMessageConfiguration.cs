using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class WebhookOutboxMessageConfiguration : IEntityTypeConfiguration<WebhookOutboxMessage>
{
    public void Configure(EntityTypeBuilder<WebhookOutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasDefaultValueSql("NEWID()");
        builder.Property(message => message.EventType).IsRequired().HasMaxLength(100);
        builder.Property(message => message.Payload).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(message => message.LeaseOwner).HasMaxLength(200);
        builder.Property(message => message.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(message => new
        {
            message.ProcessedAt,
            message.DeadLetteredAt,
            message.NextAttemptAt,
            message.LockedUntil
        });
        builder.HasIndex(message => new { message.ProjectId, message.CreatedAt });
    }
}
