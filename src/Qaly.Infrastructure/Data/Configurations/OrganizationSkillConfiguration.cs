using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class OrganizationSkillConfiguration : IEntityTypeConfiguration<OrganizationSkill>
{
    public void Configure(EntityTypeBuilder<OrganizationSkill> builder)
    {
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Id).HasDefaultValueSql("NEWID()");
        builder.Property(skill => skill.Name).HasMaxLength(100).IsRequired();
        builder.Property(skill => skill.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(skill => skill.Description).HasMaxLength(500);
        builder.Property(skill => skill.Category).HasMaxLength(100).HasDefaultValue("Chuyên môn").IsRequired();
        builder.Property(skill => skill.AliasesJson).HasColumnType("nvarchar(max)").HasDefaultValue("[]").IsRequired();
        builder.Property(skill => skill.DefaultRequiredLevel).HasMaxLength(30).HasDefaultValue("Intermediate").IsRequired();
        builder.Property(skill => skill.IsSystemSeed).HasDefaultValue(false);
        builder.Property(skill => skill.IsActive).HasDefaultValue(true);
        builder.Property(skill => skill.RowVersion).IsRowVersion();
        builder.Property(skill => skill.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(skill => skill.Organization)
            .WithMany(organization => organization.Skills)
            .HasForeignKey(skill => skill.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(skill => new { skill.OrganizationId, skill.NormalizedName }).IsUnique();
        builder.HasIndex(skill => new { skill.OrganizationId, skill.IsActive });
        builder.HasIndex(skill => new { skill.OrganizationId, skill.Category });
    }
}
