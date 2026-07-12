using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P002CanonicalAiJobPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiJobs_Projects_ProjectId",
                table: "AiJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_RequestedById",
                table: "AiJobs");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiUsageLedger",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCostUsd",
                table: "AiUsageLedger",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiJobId",
                table: "AiUsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingVersion",
                table: "AiUsageLedger",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderAttemptId",
                table: "AiUsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCostUsd",
                table: "AiJobs",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AiJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetPolicyId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CacheHit",
                table: "AiJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CanceledAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanceledById",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CloudEligible",
                table: "AiJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ConsentId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinishedAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AiJobs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValueSql: "CONCAT(N'legacy:', CONVERT(nvarchar(36), NEWID()))");

            migrationBuilder.AddColumn<bool>(
                name: "IsMock",
                table: "AiJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "AiJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "AiJobs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastErrorRetryable",
                table: "AiJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LegacyQueueItemId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyStatus",
                table: "AiJobs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "AiJobs",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumCostUsd",
                table: "AiJobs",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MockReason",
                table: "AiJobs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PolicyCheckedAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyDecisionJson",
                table: "AiJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingVersion",
                table: "AiJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "AiJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "AiJobs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "AiJobs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestJson",
                table: "AiJobs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ResultHash",
                table: "AiJobs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "AiJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetentionPolicyId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiJobs",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<string>(
                name: "SchemaId",
                table: "AiJobs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "legacy.unresolved");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "AiJobs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "SelectedModel",
                table: "AiJobs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedProvider",
                table: "AiJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "AiJobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationIdempotencyKey",
                table: "AiGeneratedDrafts",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationResultJson",
                table: "AiGeneratedDrafts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "AiGeneratedDrafts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalPayloadJson",
                table: "AiGeneratedDrafts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAt",
                table: "AiGeneratedDrafts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "AiGeneratedDrafts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AiGeneratedDrafts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiGeneratedDrafts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<string>(
                name: "SourceHashAtGeneration",
                table: "AiGeneratedDrafts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarningsJson",
                table: "AiGeneratedDrafts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkingPayloadJson",
                table: "AiGeneratedDrafts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "AiJobId",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntityGuid",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityKey",
                table: "AiAuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderAttemptId",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiJobDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseOwner = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryCount = table.Column<int>(type: "int", nullable: false),
                    LastDispatchErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastDispatchError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiJobDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiJobDispatches_AiJobs_AiJobId",
                        column: x => x.AiJobId,
                        principalTable: "AiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiJobMigrationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyQueueItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalAiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LegacySnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiJobMigrationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiJobSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegacySourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SourceTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiJobSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiJobSources_AiJobs_AiJobId",
                        column: x => x.AiJobId,
                        principalTable: "AiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiProviderAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ActualCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CacheHit = table.Column<bool>(type: "bit", nullable: false),
                    IsMock = table.Column<bool>(type: "bit", nullable: false),
                    MockReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResponseHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Retryable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiProviderAttempts_AiJobs_AiJobId",
                        column: x => x.AiJobId,
                        principalTable: "AiJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                UPDATE job
                SET
                    [TenantId] = project.[OrganizationId],
                    [LegacyStatus] = COALESCE(NULLIF(job.[LegacyStatus], N''), job.[Status]),
                    [Status] = CASE
                        WHEN normalized.[Value] IN (N'draftready', N'confirmed', N'rejected', N'succeeded', N'success') THEN N'succeeded'
                        WHEN normalized.[Value] = N'queued' THEN N'queued'
                        WHEN normalized.[Value] = N'retrying' THEN N'retrying'
                        WHEN normalized.[Value] IN (N'canceled', N'cancelled') THEN N'canceled'
                        WHEN normalized.[Value] = N'failed' THEN N'failed'
                        ELSE N'failed'
                    END,
                    [AvailableAt] = job.[CreatedAt],
                    [FinishedAt] = CASE
                        WHEN normalized.[Value] IN (N'draftready', N'confirmed', N'rejected', N'succeeded', N'success', N'failed', N'canceled', N'cancelled', N'running')
                            THEN COALESCE(job.[FinishedAt], job.[UpdatedAt], job.[CreatedAt])
                        ELSE job.[FinishedAt]
                    END,
                    [MaxAttempts] = CASE WHEN job.[MaxAttempts] < 1 THEN 3 ELSE job.[MaxAttempts] END,
                    [ProgressPercent] = CASE
                        WHEN normalized.[Value] IN (N'draftready', N'confirmed', N'rejected', N'succeeded', N'success') THEN 100
                        ELSE job.[ProgressPercent]
                    END,
                    [IdempotencyKey] = CASE
                        WHEN NULLIF(job.[IdempotencyKey], N'') IS NULL THEN CONCAT(N'legacy:', CONVERT(nvarchar(36), job.[Id]))
                        ELSE job.[IdempotencyKey]
                    END,
                    [SchemaId] = COALESCE(NULLIF(job.[SchemaId], N''), NULLIF(draft.[SchemaId], N''), N'legacy.unresolved'),
                    [SchemaVersion] = CASE WHEN NULLIF(job.[SchemaVersion], N'') IS NULL THEN N'legacy' ELSE job.[SchemaVersion] END,
                    [RequestJson] = CASE WHEN NULLIF(job.[RequestJson], N'') IS NULL THEN N'{}' ELSE job.[RequestJson] END,
                    [RequestHash] = CASE
                        WHEN NULLIF(job.[RequestHash], N'') IS NULL THEN LOWER(CONVERT(varchar(64), HASHBYTES(
                            'SHA2_256',
                            CONCAT(job.[JobType], N'|', job.[ProjectId], N'|', job.[SourceType], N'|', COALESCE(job.[SourceId], N''), N'|', job.[CreatedAt])
                        ), 2))
                        ELSE job.[RequestHash]
                    END,
                    [CloudEligible] = CASE WHEN job.[Sensitive] = 0 THEN 1 ELSE job.[CloudEligible] END,
                    [PolicyDecisionJson] = COALESCE(job.[PolicyDecisionJson], N'{"source":"legacy_migration","decision":"unknown"}'),
                    [LastErrorCode] = CASE
                        WHEN normalized.[Value] = N'running' THEN N'AI_LEGACY_RUNNING_REQUIRES_RETRY'
                        WHEN normalized.[Value] NOT IN (
                            N'draftready', N'confirmed', N'rejected', N'succeeded', N'success', N'queued', N'retrying', N'failed', N'canceled', N'cancelled'
                        ) THEN N'AI_LEGACY_STATUS_UNKNOWN'
                        ELSE job.[LastErrorCode]
                    END,
                    [LastErrorMessage] = CASE
                        WHEN normalized.[Value] = N'running' THEN N'Legacy running job was not resumed automatically.'
                        WHEN normalized.[Value] NOT IN (
                            N'draftready', N'confirmed', N'rejected', N'succeeded', N'success', N'queued', N'retrying', N'failed', N'canceled', N'cancelled'
                        ) THEN N'Legacy status requires reconciliation.'
                        ELSE job.[LastErrorMessage]
                    END
                FROM [AiJobs] AS job
                LEFT JOIN [Projects] AS project ON project.[Id] = job.[ProjectId]
                OUTER APPLY (
                    SELECT TOP (1) generated.[SchemaId]
                    FROM [AiGeneratedDrafts] AS generated
                    WHERE generated.[AiJobId] = job.[Id]
                    ORDER BY generated.[CreatedAt]
                ) AS draft
                CROSS APPLY (SELECT LOWER(LTRIM(RTRIM(job.[Status]))) AS [Value]) AS normalized;

                UPDATE draft
                SET
                    [OriginalPayloadJson] = draft.[PayloadJson],
                    [WorkingPayloadJson] = draft.[PayloadJson],
                    [Status] = CASE
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'confirmed' THEN N'confirmed'
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'rejected' THEN N'rejected'
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'expired' THEN N'expired'
                        ELSE N'pending_review'
                    END,
                    [RejectedById] = CASE
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'rejected' THEN draft.[ConfirmedById]
                        ELSE draft.[RejectedById]
                    END,
                    [RejectedAt] = CASE
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'rejected' THEN draft.[ConfirmedAt]
                        ELSE draft.[RejectedAt]
                    END,
                    [RejectionReason] = CASE
                        WHEN LOWER(LTRIM(RTRIM(draft.[Status]))) = N'rejected' THEN draft.[ConfirmationNote]
                        ELSE draft.[RejectionReason]
                    END,
                    [SourceHashAtGeneration] = job.[RequestHash]
                FROM [AiGeneratedDrafts] AS draft
                INNER JOIN [AiJobs] AS job ON job.[Id] = draft.[AiJobId];

                INSERT INTO [AiJobSources] (
                    [Id], [AiJobId], [SourceType], [SourceEntityId], [LegacySourceKey], [SourceVersion], [SourceHash],
                    [SourceTimestamp], [SortOrder], [CreatedAt], [UpdatedAt]
                )
                SELECT
                    NEWID(),
                    job.[Id],
                    job.[SourceType],
                    TRY_CONVERT(uniqueidentifier, job.[SourceId]),
                    CASE WHEN TRY_CONVERT(uniqueidentifier, job.[SourceId]) IS NULL THEN job.[SourceId] ELSE NULL END,
                    N'legacy',
                    LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT(job.[SourceType], N'|', job.[SourceId])), 2)),
                    job.[CreatedAt],
                    0,
                    SYSDATETIMEOFFSET(),
                    NULL
                FROM [AiJobs] AS job
                WHERE NULLIF(job.[SourceId], N'') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [AiJobSources] AS source WHERE source.[AiJobId] = job.[Id]);

                INSERT INTO [AiJobDispatches] (
                    [Id], [AiJobId], [Priority], [AvailableAt], [LeaseOwner], [LeaseExpiresAt], [DeliveryCount],
                    [LastDispatchErrorCode], [LastDispatchError], [CompletedAt], [CreatedAt], [UpdatedAt]
                )
                SELECT
                    NEWID(), job.[Id], 100, job.[AvailableAt], NULL, NULL, 0, NULL, NULL, NULL, SYSDATETIMEOFFSET(), NULL
                FROM [AiJobs] AS job
                WHERE job.[Status] IN (N'queued', N'retrying')
                  AND NOT EXISTS (SELECT 1 FROM [AiJobDispatches] AS dispatch WHERE dispatch.[AiJobId] = job.[Id]);

                INSERT INTO [AiJobMigrationRecords] (
                    [Id], [LegacyQueueItemId], [CanonicalAiJobId], [Classification], [Reason], [LegacySnapshotJson],
                    [ReconciledAt], [CreatedAt], [UpdatedAt]
                )
                SELECT
                    NEWID(),
                    queueItem.[Id],
                    NULL,
                    CASE
                        WHEN LOWER(queueItem.[Status]) IN (N'queued', N'running', N'retrying') THEN N'quarantined'
                        ELSE N'historical_unlinked'
                    END,
                    CASE
                        WHEN LOWER(queueItem.[Status]) IN (N'queued', N'running', N'retrying')
                            THEN N'Legacy queue item requires an allowlisted handler and explicit reconciliation.'
                        ELSE N'Legacy terminal queue item retained without fabricated canonical linkage.'
                    END,
                    (
                        SELECT
                            queueItem.[Id] AS [id],
                            queueItem.[TenantId] AS [tenant_id],
                            queueItem.[ProjectId] AS [project_id],
                            queueItem.[RequestedBy] AS [requested_by],
                            queueItem.[JobType] AS [job_type],
                            queueItem.[SchemaId] AS [schema_id],
                            queueItem.[Status] AS [status],
                            queueItem.[RetryCount] AS [retry_count],
                            queueItem.[MaxRetry] AS [max_retry],
                            CASE WHEN queueItem.[PayloadJson] IS NULL THEN NULL ELSE LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', queueItem.[PayloadJson]), 2)) END AS [payload_hash],
                            CASE WHEN queueItem.[ResultJson] IS NULL THEN NULL ELSE LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', queueItem.[ResultJson]), 2)) END AS [result_hash]
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    ),
                    NULL,
                    SYSDATETIMEOFFSET(),
                    NULL
                FROM [AiJobQueue] AS queueItem
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AiJobMigrationRecords] AS record WHERE record.[LegacyQueueItemId] = queueItem.[Id]
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLedger_AiJobId_ProviderAttemptId",
                table: "AiUsageLedger",
                columns: new[] { "AiJobId", "ProviderAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLedger_ProviderAttemptId",
                table: "AiUsageLedger",
                column: "ProviderAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_LegacyQueueItemId",
                table: "AiJobs",
                column: "LegacyQueueItemId",
                unique: true,
                filter: "[LegacyQueueItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_RequestedById_IdempotencyKey",
                table: "AiJobs",
                columns: new[] { "RequestedById", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_Status_AvailableAt",
                table: "AiJobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedDrafts_Id_ConfirmationIdempotencyKey",
                table: "AiGeneratedDrafts",
                columns: new[] { "Id", "ConfirmationIdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedDrafts_RejectedById",
                table: "AiGeneratedDrafts",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_AiJobId_CreatedAt",
                table: "AiAuditEvents",
                columns: new[] { "AiJobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_ProviderAttemptId",
                table: "AiAuditEvents",
                column: "ProviderAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobDispatches_AiJobId",
                table: "AiJobDispatches",
                column: "AiJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobDispatches_AvailableAt_LeaseExpiresAt_Priority",
                table: "AiJobDispatches",
                columns: new[] { "AvailableAt", "LeaseExpiresAt", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_AiJobMigrationRecords_CanonicalAiJobId",
                table: "AiJobMigrationRecords",
                column: "CanonicalAiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobMigrationRecords_LegacyQueueItemId",
                table: "AiJobMigrationRecords",
                column: "LegacyQueueItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobSources_AiJobId_SortOrder",
                table: "AiJobSources",
                columns: new[] { "AiJobId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobSources_SourceType_SourceEntityId",
                table: "AiJobSources",
                columns: new[] { "SourceType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderAttempts_AiJobId_AttemptNumber",
                table: "AiProviderAttempts",
                columns: new[] { "AiJobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGeneratedDrafts_Users_RejectedById",
                table: "AiGeneratedDrafts",
                column: "RejectedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiJobs_Projects_ProjectId",
                table: "AiJobs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiUsageLedger_AiJobs_AiJobId",
                table: "AiUsageLedger",
                column: "AiJobId",
                principalTable: "AiJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiUsageLedger_AiProviderAttempts_ProviderAttemptId",
                table: "AiUsageLedger",
                column: "ProviderAttemptId",
                principalTable: "AiProviderAttempts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiGeneratedDrafts_Users_RejectedById",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropForeignKey(
                name: "FK_AiJobs_Projects_ProjectId",
                table: "AiJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_AiUsageLedger_AiJobs_AiJobId",
                table: "AiUsageLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_AiUsageLedger_AiProviderAttempts_ProviderAttemptId",
                table: "AiUsageLedger");

            migrationBuilder.DropTable(
                name: "AiJobDispatches");

            migrationBuilder.DropTable(
                name: "AiJobMigrationRecords");

            migrationBuilder.DropTable(
                name: "AiJobSources");

            migrationBuilder.DropTable(
                name: "AiProviderAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AiUsageLedger_AiJobId_ProviderAttemptId",
                table: "AiUsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_AiUsageLedger_ProviderAttemptId",
                table: "AiUsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_LegacyQueueItemId",
                table: "AiJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_RequestedById_IdempotencyKey",
                table: "AiJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_Status_AvailableAt",
                table: "AiJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiGeneratedDrafts_Id_ConfirmationIdempotencyKey",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropIndex(
                name: "IX_AiGeneratedDrafts_RejectedById",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropIndex(
                name: "IX_AiAuditEvents_AiJobId_CreatedAt",
                table: "AiAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AiAuditEvents_ProviderAttemptId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "ActualCostUsd",
                table: "AiUsageLedger");

            migrationBuilder.DropColumn(
                name: "AiJobId",
                table: "AiUsageLedger");

            migrationBuilder.DropColumn(
                name: "PricingVersion",
                table: "AiUsageLedger");

            migrationBuilder.DropColumn(
                name: "ProviderAttemptId",
                table: "AiUsageLedger");

            migrationBuilder.DropColumn(
                name: "ActualCostUsd",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "BudgetPolicyId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "CacheHit",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "CanceledAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "CanceledById",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "CloudEligible",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ConsentId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "IsMock",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastErrorRetryable",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LegacyQueueItemId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LegacyStatus",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MaximumCostUsd",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MockReason",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "PolicyCheckedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "PolicyDecisionJson",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "PricingVersion",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "RequestJson",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ResultHash",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ResultJson",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "SchemaId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "SelectedModel",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "SelectedProvider",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ConfirmationIdempotencyKey",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "ConfirmationResultJson",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "OriginalPayloadJson",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "SourceHashAtGeneration",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "WarningsJson",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "WorkingPayloadJson",
                table: "AiGeneratedDrafts");

            migrationBuilder.DropColumn(
                name: "AiJobId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "EntityGuid",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "EntityKey",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "ProviderAttemptId",
                table: "AiAuditEvents");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AiUsageLedger",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_RequestedById",
                table: "AiJobs",
                column: "RequestedById");

            migrationBuilder.AddForeignKey(
                name: "FK_AiJobs_Projects_ProjectId",
                table: "AiJobs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
