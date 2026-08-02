using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AssistantProcessEventConfiguration : IEntityTypeConfiguration<AssistantProcessEvent>
{
    public void Configure(EntityTypeBuilder<AssistantProcessEvent> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Stage).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(30).IsRequired();
        builder.Property(item => item.PublicLabel).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SafeErrorCode).HasMaxLength(100);

        builder.HasOne(item => item.Turn)
            .WithMany(turn => turn.ProcessEvents)
            .HasForeignKey(item => item.TurnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.TurnId, item.Sequence }).IsUnique();
    }
}
