using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AssistantSessionConfiguration : IEntityTypeConfiguration<AssistantSession>
{
    public void Configure(EntityTypeBuilder<AssistantSession> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.ClarificationDraftJson).HasColumnType("nvarchar(max)");
        builder.Property(item => item.Version).IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(item => new { item.OwnerUserId, item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.TenantId, item.OwnerUserId });
    }
}
