using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupAttachmentConfiguration : IEntityTypeConfiguration<GroupAttachment>
{
    public void Configure(EntityTypeBuilder<GroupAttachment> builder)
    {
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).HasDefaultValueSql("NEWID()");
        builder.Property(attachment => attachment.FileName).HasMaxLength(500).IsRequired();
        builder.Property(attachment => attachment.FilePath).HasMaxLength(1000).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(attachment => attachment.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(attachment => attachment.WorkGroup)
            .WithMany()
            .HasForeignKey(attachment => attachment.WorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(attachment => attachment.UploadedBy)
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attachment => new { attachment.WorkGroupId, attachment.CreatedAt });
    }
}
