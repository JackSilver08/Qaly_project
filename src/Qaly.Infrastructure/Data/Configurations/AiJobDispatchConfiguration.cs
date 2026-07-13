using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiJobDispatchConfiguration : IEntityTypeConfiguration<AiJobDispatch>
{
    public void Configure(EntityTypeBuilder<AiJobDispatch> builder)
    {
        builder.HasKey(dispatch => dispatch.Id);
        builder.Property(dispatch => dispatch.LeaseOwner).HasMaxLength(160);
        builder.Property(dispatch => dispatch.LastDispatchErrorCode).HasMaxLength(80);
        builder.Property(dispatch => dispatch.LastDispatchError).HasMaxLength(2000);
        builder.Property(dispatch => dispatch.RowVersion).IsRowVersion();

        builder.HasOne(dispatch => dispatch.AiJob)
            .WithOne(job => job.Dispatch)
            .HasForeignKey<AiJobDispatch>(dispatch => dispatch.AiJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(dispatch => dispatch.AiJobId).IsUnique();
        builder.HasIndex(dispatch => new { dispatch.AvailableAt, dispatch.LeaseExpiresAt, dispatch.Priority });
    }
}
