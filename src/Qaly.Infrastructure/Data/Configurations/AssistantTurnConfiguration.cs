using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AssistantTurnConfiguration : IEntityTypeConfiguration<AssistantTurn>
{
    public void Configure(EntityTypeBuilder<AssistantTurn> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(item => item.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.UserMessage).HasMaxLength(8000).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.Disposition).HasMaxLength(40);
        builder.Property(item => item.Intent).HasMaxLength(80);
        builder.Property(item => item.ExecutionPolicy).HasMaxLength(40);
        builder.Property(item => item.ModelProfile).HasMaxLength(60).IsRequired();
        builder.Property(item => item.ActualProvider).HasMaxLength(80);
        builder.Property(item => item.ActualModel).HasMaxLength(120);
        builder.Property(item => item.CorrelationId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.SafeErrorCode).HasMaxLength(100);

        builder.HasOne(item => item.Session)
            .WithMany(session => session.Turns)
            .HasForeignKey(item => item.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.SessionId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.SessionId, item.ClientTurnId }).IsUnique();
        builder.HasIndex(item => new { item.SessionId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.SessionId, item.CreatedAt });
    }
}
