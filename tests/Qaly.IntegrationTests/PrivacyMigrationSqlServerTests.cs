using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class PrivacyMigrationSqlServerTests
{
    private const string PreviousMigration = "20260712064020_AddGitHubIntegrationSchema";

    [Fact]
    public async Task P003Migration_ClassifiesLegacySensitiveDataWithoutSilentConsentOrDeletionSchedule()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await using var database = await SqlMigrationDatabase.CreateAsync();
        Guid meetingId;
        Guid consentId;
        Guid requestId;
        Guid projectId;
        Guid userId;

        await using (var legacyContext = database.CreateContext())
        {
            await legacyContext.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            var user = new User
            {
                FullName = "Privacy Migration Subject",
                Email = $"privacy-migration-{Guid.NewGuid():N}@qaly.test",
                PasswordHash = "not-used",
                Role = "Admin"
            };
            var project = new Project
            {
                Name = "Privacy Migration Project",
                Code = $"PM-{Guid.NewGuid():N}"[..12],
                OwnerId = user.Id
            };
            legacyContext.Users.Add(user);
            await legacyContext.SaveChangesAsync();
            // The current model includes ArchivedAt, but this fixture intentionally
            // stops before the later AddArchivedAtToProject migration.
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO [Projects] ([Id], [Name], [Code], [Status], [OwnerId], [CreatedAt])
                VALUES ({{project.Id}}, {{project.Name}}, {{project.Code}}, N'Active', {{user.Id}}, {{DateTimeOffset.UtcNow}});
                """);
            userId = user.Id;
            projectId = project.Id;
            meetingId = Guid.NewGuid();
            consentId = Guid.NewGuid();
            requestId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            await legacyContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO [MeetingImports] (
                    [Id], [ProjectId], [ImportedById], [SourceProvider], [SourceId], [SourceHash], [Title],
                    [MeetingStartedAt], [Summary], [TranscriptText], [ParticipantsJson], [RawPayloadJson],
                    [AiJobId], [AiDraftId], [CreatedAt], [UpdatedAt]
                ) VALUES (
                    {{meetingId}}, {{project.Id}}, {{user.Id}}, N'meetily', N'legacy-meeting',
                    N'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', N'Legacy sensitive meeting',
                    {{now}}, N'legacy summary', N'legacy transcript', N'[]', N'{}', NULL, NULL, {{now}}, NULL
                );

                INSERT INTO [PrivacyConsents] (
                    [Id], [TenantId], [ProjectId], [UserId], [ConsentType], [Purpose], [ScopeJson], [Status],
                    [GrantedAt], [RevokedAt], [IpAddress], [UserAgent], [CreatedAt], [UpdatedAt]
                ) VALUES (
                    {{consentId}}, NULL, {{project.Id}}, {{user.Id}}, N'ai_cloud_processing', N'meeting_action_extraction',
                    NULL, N'granted', {{now}}, NULL, NULL, NULL, {{now}}, NULL
                );

                INSERT INTO [DataSubjectRequests] (
                    [Id], [TenantId], [ProjectId], [RequesterUserId], [RequestType], [ScopeJson], [Status],
                    [RequestedAt], [ApprovedBy], [CompletedAt], [RejectionReason], [EvidenceUri], [CreatedAt], [UpdatedAt]
                ) VALUES (
                    {{requestId}}, {{project.Id}}, {{project.Id}}, {{user.Id}}, N'export', N'{"scope":"project"}',
                    N'pending', {{now}}, NULL, NULL, NULL, NULL, {{now}}, NULL
                );
                """);

            await legacyContext.Database.ExecuteSqlRawAsync(ReadScript("p003-preflight.sql"));
            await legacyContext.GetService<IMigrator>().MigrateAsync();
        }

        await using var verification = database.CreateContext();
        var meeting = await verification.MeetingImports.SingleAsync(item => item.Id == meetingId);
        meeting.TenantId.Should().Be(projectId);
        meeting.DataClassification.Should().Be(PrivacyDataClasses.UnknownSensitive);
        meeting.PrivacyState.Should().Be(MeetingPrivacyStates.MigrationReview);
        meeting.ProviderClass.Should().Be(PrivacyProviderClasses.Unknown);
        meeting.ConsentId.Should().BeNull();
        meeting.RetentionPolicyId.Should().BeNull();
        meeting.RetentionExpiresAt.Should().BeNull();

        var consent = await verification.PrivacyConsents.SingleAsync(item => item.Id == consentId);
        consent.PolicyVersion.Should().Be("legacy-unknown");
        consent.NoticeVersion.Should().Be("legacy-unknown");
        consent.ProviderClass.Should().Be(PrivacyProviderClasses.Unknown);
        consent.SourceType.Should().Be("legacy");
        consent.RetentionPolicyId.Should().BeNull();

        var request = await verification.DataSubjectRequests.SingleAsync(item => item.Id == requestId);
        request.SubjectUserId.Should().Be(userId);
        request.Status.Should().Be(DataSubjectRequestStatuses.Submitted);
        request.MaxAttempts.Should().Be(5);
        request.AvailableAt.Should().BeCloseTo(request.RequestedAt, TimeSpan.FromSeconds(1));
        (await verification.PrivacyRetentionActions.CountAsync()).Should().Be(0);

        await verification.Database.ExecuteSqlRawAsync(ReadScript("p003-postflight.sql"));
    }

    private static string ReadScript(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "scripts", "privacy", fileName));
        return File.ReadAllText(path);
    }

    private sealed class SqlMigrationDatabase : IAsyncDisposable
    {
        private readonly string _connectionString;

        private SqlMigrationDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static Task<SqlMigrationDatabase> CreateAsync()
        {
            var databaseName = $"QalyPrivacyMigrationTests_{Guid.NewGuid():N}";
            var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            var builder = string.IsNullOrWhiteSpace(configured)
                ? new SqlConnectionStringBuilder
                {
                    DataSource = "(localdb)\\MSSQLLocalDB",
                    InitialCatalog = databaseName,
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    MultipleActiveResultSets = true
                }
                : new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
            return Task.FromResult(new SqlMigrationDatabase(builder.ConnectionString));
        }

        public QalyDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<QalyDbContext>()
                .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure())
                .Options;
            return new QalyDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
