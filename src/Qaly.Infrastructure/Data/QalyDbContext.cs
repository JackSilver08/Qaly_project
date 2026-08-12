using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data;

public class QalyDbContext : DbContext
{
    private static readonly string[] LowConfidenceDraftWarnings = ["low_confidence"];

    public QalyDbContext(DbContextOptions<QalyDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationSkill> OrganizationSkills => Set<OrganizationSkill>();
    public DbSet<ModeratorAssignment> ModeratorAssignments => Set<ModeratorAssignment>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<SystemModulePermission> SystemModulePermissions => Set<SystemModulePermission>();
    public DbSet<ProjectCustomRole> ProjectCustomRoles => Set<ProjectCustomRole>();
    public DbSet<ProjectMemberRoleHistory> ProjectMemberRoleHistories => Set<ProjectMemberRoleHistory>();
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
    public DbSet<PhysicalFile> PhysicalFiles => Set<PhysicalFile>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskViewEvent> TaskViewEvents => Set<TaskViewEvent>();
    public DbSet<TaskAttentionSignal> TaskAttentionSignals => Set<TaskAttentionSignal>();
    public DbSet<ProjectLabel> ProjectLabels => Set<ProjectLabel>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<TaskSkillRequirement> TaskSkillRequirements => Set<TaskSkillRequirement>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WikiPage> WikiPages => Set<WikiPage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiJob> AiJobs => Set<AiJob>();
    public DbSet<AiJobSource> AiJobSources => Set<AiJobSource>();
    public DbSet<AiJobDispatch> AiJobDispatches => Set<AiJobDispatch>();
    public DbSet<AiProviderAttempt> AiProviderAttempts => Set<AiProviderAttempt>();
    public DbSet<AiJobMigrationRecord> AiJobMigrationRecords => Set<AiJobMigrationRecord>();
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
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<PrivacyRetentionAction> PrivacyRetentionActions => Set<PrivacyRetentionAction>();
    public DbSet<PrivacyLegalHold> PrivacyLegalHolds => Set<PrivacyLegalHold>();

    // GitHub integration (read-only metadata)
    public DbSet<GitHubInstallation> GitHubInstallations => Set<GitHubInstallation>();
    public DbSet<GitHubRepositoryConnection> GitHubRepositoryConnections => Set<GitHubRepositoryConnection>();
    public DbSet<GitHubCommit> GitHubCommits => Set<GitHubCommit>();
    public DbSet<GitHubPullRequest> GitHubPullRequests => Set<GitHubPullRequest>();
    public DbSet<GitHubPullRequestReview> GitHubPullRequestReviews => Set<GitHubPullRequestReview>();
    public DbSet<GitHubRelease> GitHubReleases => Set<GitHubRelease>();
    public DbSet<TaskDevelopmentLink> TaskDevelopmentLinks => Set<TaskDevelopmentLink>();
    public DbSet<GitHubWebhookInbox> GitHubWebhookInbox => Set<GitHubWebhookInbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QalyDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyPreSaveConventions();
        await AssignPendingTaskNumbersAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyPreSaveConventions();
        AssignPendingTaskNumbers();
        return base.SaveChanges();
    }

    private void ApplyPreSaveConventions()
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

        foreach (var entry in ChangeTracker.Entries<AiJob>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.Status = NormalizeJobStatus(entry.Entity.Status);
            }
        }
    }

    /// <summary>
    /// Cấp Task Key number tuần tự theo từng project cho các task mới (Number == 0).
    /// Chạy tập trung ở đây để mọi đường tạo task (TaskService, Import, Meeting,
    /// AiTools) đều nhận key mà không phải lặp lại logic.
    /// </summary>
    private async Task AssignPendingTaskNumbersAsync(CancellationToken cancellationToken)
    {
        var groups = GetPendingTaskGroups();
        foreach (var group in groups)
        {
            var start = TryReserveFromTrackedNewProject(group.ProjectId, group.Tasks.Count)
                ?? await ReserveTaskNumbersAsync(group.ProjectId, group.Tasks.Count, cancellationToken);
            AssignSequential(group.Tasks, start);
        }
    }

    private void AssignPendingTaskNumbers()
    {
        var groups = GetPendingTaskGroups();
        foreach (var group in groups)
        {
            var start = TryReserveFromTrackedNewProject(group.ProjectId, group.Tasks.Count)
                ?? ReserveTaskNumbers(group.ProjectId, group.Tasks.Count);
            AssignSequential(group.Tasks, start);
        }
    }

    private List<(Guid ProjectId, List<TaskItem> Tasks)> GetPendingTaskGroups()
    {
        return ChangeTracker.Entries<TaskItem>()
            .Where(e => e.State == EntityState.Added && e.Entity.Number == 0)
            .GroupBy(e => e.Entity.ProjectId)
            .Select(g => (g.Key, g.Select(e => e.Entity).ToList()))
            .ToList();
    }

    /// <summary>
    /// Nếu project cũng đang được tạo mới trong cùng unit of work, dòng chưa tồn
    /// tại trong DB nên cấp số ngay trên entity đang theo dõi (không có tranh chấp
    /// vì chưa ai tham chiếu được project mới).
    /// </summary>
    private int? TryReserveFromTrackedNewProject(Guid projectId, int count)
    {
        var projectEntry = ChangeTracker.Entries<Project>()
            .FirstOrDefault(e => e.Entity.Id == projectId);

        if (projectEntry is null || projectEntry.State != EntityState.Added)
        {
            return null;
        }

        var start = projectEntry.Entity.TaskSequence;
        projectEntry.Entity.TaskSequence = start + count;
        return start;
    }

    private static void AssignSequential(List<TaskItem> tasks, int start)
    {
        var next = start;
        foreach (var task in tasks)
        {
            task.Number = ++next;
        }
    }

    // Atomic reservation of a contiguous number block on an existing project row.
    // OUTPUT deleted.TaskSequence returns the value before the increment; the row
    // lock serialises concurrent SaveChanges so ranges never overlap.
    private const string ReserveSql =
        "UPDATE Projects SET TaskSequence = TaskSequence + {0} OUTPUT deleted.TaskSequence AS Value WHERE Id = {1}";

    private async Task<int> ReserveTaskNumbersAsync(Guid projectId, int count, CancellationToken cancellationToken)
    {
        // Non-relational providers (e.g. InMemory used in tests) do not support
        // raw SQL; fall back to incrementing the tracked/loaded project row.
        if (!Database.IsRelational())
        {
            return ReserveViaEntity(projectId, count);
        }

        var rows = await Database
            .SqlQueryRaw<int>(ReserveSql, count, projectId)
            .ToListAsync(cancellationToken);
        return rows.Count > 0 ? rows[0] : 0;
    }

    private int ReserveTaskNumbers(Guid projectId, int count)
    {
        if (!Database.IsRelational())
        {
            return ReserveViaEntity(projectId, count);
        }

        var rows = Database
            .SqlQueryRaw<int>(ReserveSql, count, projectId)
            .ToList();
        return rows.Count > 0 ? rows[0] : 0;
    }

    private int ReserveViaEntity(Guid projectId, int count)
    {
        var project = Projects.Find(projectId);
        if (project is null)
        {
            return 0;
        }

        var start = project.TaskSequence;
        project.TaskSequence = start + count;
        return start;
    }

    private static void PopulateDraftMetadata(AiGeneratedDraft draft)
    {
        draft.Status = NormalizeDraftStatus(draft.Status);
        if (string.IsNullOrWhiteSpace(draft.OriginalPayloadJson) ||
            string.Equals(draft.OriginalPayloadJson, "{}", StringComparison.Ordinal))
        {
            draft.OriginalPayloadJson = draft.PayloadJson;
        }

        if (string.IsNullOrWhiteSpace(draft.WorkingPayloadJson) ||
            string.Equals(draft.WorkingPayloadJson, "{}", StringComparison.Ordinal))
        {
            draft.WorkingPayloadJson = draft.PayloadJson;
        }

        draft.PayloadJson = draft.WorkingPayloadJson;
        if (string.IsNullOrWhiteSpace(draft.WorkingPayloadJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(draft.WorkingPayloadJson);
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
                draft.WarningsJson ??= JsonSerializer.Serialize(LowConfidenceDraftWarnings);
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }

    private static string NormalizeJobStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "queued" => AiJobStatuses.Queued,
            "running" => AiJobStatuses.Running,
            "retrying" => AiJobStatuses.Retrying,
            "failed" => AiJobStatuses.Failed,
            "canceled" or "cancelled" => AiJobStatuses.Canceled,
            "draftready" or "confirmed" or "rejected" or "succeeded" or "success" => AiJobStatuses.Succeeded,
            _ => AiJobStatuses.Failed
        };

    private static string NormalizeDraftStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "confirmed" => AiDraftStatuses.Confirmed,
            "rejected" => AiDraftStatuses.Rejected,
            "expired" => AiDraftStatuses.Expired,
            _ => AiDraftStatuses.PendingReview
        };

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
