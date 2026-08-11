using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AssistantTestRunConfiguration : IEntityTypeConfiguration<AssistantTestRun>
{
    public void Configure(EntityTypeBuilder<AssistantTestRun> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ManifestId).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(32).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(180);
        builder.Property(item => item.EventsJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.SafeSummary).HasMaxLength(600);
        builder.Property(item => item.SafeErrorCode).HasMaxLength(80);
        builder.Property(item => item.Revision).IsConcurrencyToken();

        builder.HasOne<User>().WithMany().HasForeignKey(item => item.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssistantSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AssistantTurn>().WithMany().HasForeignKey(item => item.OriginTurnId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(item => new { item.OwnerUserId, item.CreatedAt });
        builder.HasIndex(item => item.OriginTurnId).IsUnique();
        builder.HasIndex(item => item.IdempotencyKey).HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
