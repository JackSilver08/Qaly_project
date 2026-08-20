using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class AiNativeActionDraftConfiguration : IEntityTypeConfiguration<AiNativeActionDraft>
{
    public void Configure(EntityTypeBuilder<AiNativeActionDraft> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.CapabilityId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SchemaId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.RendererId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.TargetType).HasMaxLength(40).IsRequired();
        builder.Property(item => item.PayloadJson).IsRequired();
        builder.Property(item => item.SourceVersion).HasMaxLength(128).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(40).IsRequired();
        builder.Property(item => item.ConfirmationIdempotencyKey).HasMaxLength(180);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Project).WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Organization).WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(item => new { item.UserId, item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.Id, item.ConfirmationIdempotencyKey });
    }
}
