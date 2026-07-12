using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class TaskDevelopmentLinkConfiguration : IEntityTypeConfiguration<TaskDevelopmentLink>
{
    public void Configure(EntityTypeBuilder<TaskDevelopmentLink> builder)
    {
        builder.ToTable("TaskDevelopmentLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalEntityId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LinkSource).HasMaxLength(40).IsRequired().HasDefaultValue("TaskKey");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => new { x.TaskId, x.EntityType, x.ExternalEntityId }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
