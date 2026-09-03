using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class OrganizationMemberProfessionalProfileConfiguration : IEntityTypeConfiguration<OrganizationMemberProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberProfessionalProfile> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationMemberProfessionalProfiles_Proficiency",
                "[Proficiency] IN (N'Foundation', N'Practitioner', N'Proficient', N'Expert')");
            table.HasCheckConstraint(
                "CK_OrganizationMemberProfessionalProfiles_VerificationStatus",
                "[VerificationStatus] IN (N'Declared', N'Verified', N'Rejected')");
            table.HasCheckConstraint(
                "CK_OrganizationMemberProfessionalProfiles_Source",
                "[Source] IN (N'MemberDeclared', N'ManagerConfirmed', N'Imported')");
            table.HasCheckConstraint(
                "CK_OrganizationMemberProfessionalProfiles_EffectiveWindow",
                "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint(
                "CK_OrganizationMemberProfessionalProfiles_Verifier",
                "([VerificationStatus] <> N'Verified') OR ([VerifiedByUserId] IS NOT NULL AND [VerifiedAt] IS NOT NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Proficiency).HasMaxLength(30).IsRequired();
        builder.Property(item => item.VerificationStatus).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Source).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.Organization)
            .WithMany()
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProfessionalProfileDefinition)
            .WithMany(item => item.MemberProfiles)
            .HasForeignKey(item => item.ProfessionalProfileDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.VerifiedByUser)
            .WithMany()
            .HasForeignKey(item => item.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new
        {
            item.OrganizationId,
            item.UserId,
            item.ProfessionalProfileDefinitionId
        }).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.UserId, item.VerificationStatus });
        builder.HasIndex(item => item.ProfessionalProfileDefinitionId);
    }
}
