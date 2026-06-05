using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data;

public class QalyDbContext : DbContext
{
    public QalyDbContext(DbContextOptions<QalyDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<WorkGroup> WorkGroups => Set<WorkGroup>();
    public DbSet<WorkGroupMember> WorkGroupMembers => Set<WorkGroupMember>();
    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
    public DbSet<GroupMessage> GroupMessages => Set<GroupMessage>();
    public DbSet<GroupMessageUserState> GroupMessageUserStates => Set<GroupMessageUserState>();
    public DbSet<GroupAttachment> GroupAttachments => Set<GroupAttachment>();
    public DbSet<GroupPoll> GroupPolls => Set<GroupPoll>();
    public DbSet<GroupPollOption> GroupPollOptions => Set<GroupPollOption>();
    public DbSet<GroupPollVote> GroupPollVotes => Set<GroupPollVote>();
    public DbSet<GroupMeetingSession> GroupMeetingSessions => Set<GroupMeetingSession>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskViewEvent> TaskViewEvents => Set<TaskViewEvent>();
    public DbSet<TaskAttentionSignal> TaskAttentionSignals => Set<TaskAttentionSignal>();
    public DbSet<ProjectLabel> ProjectLabels => Set<ProjectLabel>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiJob> AiJobs => Set<AiJob>();
    public DbSet<AiGeneratedDraft> AiGeneratedDrafts => Set<AiGeneratedDraft>();
    public DbSet<MeetingImport> MeetingImports => Set<MeetingImport>();
    public DbSet<MeetingActionItemMapping> MeetingActionItemMappings => Set<MeetingActionItemMapping>();
    public DbSet<VectorSyncOutbox> VectorSyncOutbox => Set<VectorSyncOutbox>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ImportSession> ImportSessions => Set<ImportSession>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<AiBudgetPolicy> AiBudgetPolicies => Set<AiBudgetPolicy>();
    public DbSet<AiUsageLedger> AiUsageLedger => Set<AiUsageLedger>();
    public DbSet<AiPromptCache> AiPromptCache => Set<AiPromptCache>();
    public DbSet<AiJobItem> AiJobQueue => Set<AiJobItem>();
    public DbSet<AiAuditEvent> AiAuditEvents => Set<AiAuditEvent>();
    public DbSet<PrivacyConsent> PrivacyConsents => Set<PrivacyConsent>();
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QalyDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ISoftDeleteEntity).IsAssignableFrom(clrType))
            {
                continue;
            }

            modelBuilder.Entity(clrType)
                .Property<bool>(nameof(ISoftDeleteEntity.IsDeleted))
                .HasDefaultValue(false);

            var parameter = Expression.Parameter(clrType, "entity");
            var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeleteEntity.IsDeleted));
            var filter = Expression.Lambda(
                Expression.Equal(isDeletedProperty, Expression.Constant(false)),
                parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }
}
