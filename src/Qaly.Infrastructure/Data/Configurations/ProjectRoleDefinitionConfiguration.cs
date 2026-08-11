using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectRoleDefinitionConfiguration : IEntityTypeConfiguration<ProjectRoleDefinition>
{
    public void Configure(EntityTypeBuilder<ProjectRoleDefinition> builder)
    {
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).HasDefaultValueSql("NEWID()");
        builder.Property(definition => definition.Key).HasMaxLength(64).IsRequired();
        builder.Property(definition => definition.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(definition => definition.Description).HasMaxLength(400);
        builder.Property(definition => definition.BaseRole).HasMaxLength(32).IsRequired();
        builder.Property(definition => definition.SkillTags).HasMaxLength(400);
        builder.Property(definition => definition.IsActive).HasDefaultValue(true);

        builder.HasOne(definition => definition.Organization)
            .WithMany()
            .HasForeignKey(definition => definition.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(definition => definition.CreatedByUser)
            .WithMany()
            .HasForeignKey(definition => definition.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // A role key must be unambiguous inside one organization.
        builder.HasIndex(definition => new { definition.OrganizationId, definition.Key }).IsUnique();
    }
}
