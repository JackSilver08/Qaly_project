using System.Linq.Expressions;
using System.Text.Json;
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

        foreach (var entry in ChangeTracker.Entries<AiGeneratedDraft>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                PopulateDraftMetadata(entry.Entity);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void PopulateDraftMetadata(AiGeneratedDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.PayloadJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(draft.PayloadJson);
            var root = doc.RootElement;
            
            // 1. Try to parse schema_id or version
            if (string.IsNullOrEmpty(draft.SchemaId))
            {
                if (root.TryGetProperty("schema_id", out var schemaProp) && schemaProp.ValueKind == JsonValueKind.String)
                {
                    draft.SchemaId = schemaProp.GetString();
                }
                else if (root.TryGetProperty("SchemaVersion", out var verProp) && verProp.ValueKind == JsonValueKind.String)
                {
                    draft.SchemaId = verProp.GetString();
                }
                else if (root.TryGetProperty("$id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    draft.SchemaId = idProp.GetString();
                }
            }

            // 2. Try to parse confidence
            if (!draft.Confidence.HasValue)
            {
                if (root.TryGetProperty("confidence", out var confProp))
                {
                    if (confProp.ValueKind == JsonValueKind.Number)
                    {
                        draft.Confidence = confProp.GetDecimal();
                    }
                    else if (confProp.ValueKind == JsonValueKind.String && decimal.TryParse(confProp.GetString(), out var parsedConf))
                    {
                        draft.Confidence = parsedConf;
                    }
                }
            }

            // 3. Status logic: Confidence under 0.6 must mark draft as needs_manual_review
            if (draft.Confidence.HasValue && draft.Confidence.Value < 0.6m)
            {
                draft.Status = "needs_manual_review";
            }
        }
        catch
        {
            // Ignore parsing errors
        }
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
