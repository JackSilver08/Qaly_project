using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data;

public class QalyDbContext : DbContext
{
    public QalyDbContext(DbContextOptions<QalyDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<VectorSyncOutbox> VectorSyncOutbox => Set<VectorSyncOutbox>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply táº¥t cáº£ IEntityTypeConfiguration tá»« assembly nÃ y
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QalyDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Tá»± Ä‘á»™ng cáº­p nháº­t UpdatedAt cho cÃ¡c entity bá»‹ modify
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}


