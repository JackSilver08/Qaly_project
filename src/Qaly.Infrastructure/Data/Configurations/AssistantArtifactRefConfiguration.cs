using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AssistantArtifactRefConfiguration : IEntityTypeConfiguration<AssistantArtifactRef>
{
    public void Configure(EntityTypeBuilder<AssistantArtifactRef> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SchemaId).HasMaxLength(120).IsRequired();
        builder.Property(item => item.SchemaVersion).HasMaxLength(30).IsRequired();
        builder.Property(item => item.RendererId).HasMaxLength(120).IsRequired();

        builder.HasOne(item => item.Turn)
            .WithMany(turn => turn.ArtifactRefs)
            .HasForeignKey(item => item.TurnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.TurnId, item.SchemaId });
        builder.HasIndex(item => item.AiJobId);
        builder.HasIndex(item => item.DraftId);
    }
}
