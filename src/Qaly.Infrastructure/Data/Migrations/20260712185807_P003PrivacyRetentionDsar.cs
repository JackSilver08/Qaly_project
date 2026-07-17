using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class P003PrivacyRetentionDsar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeniedAt",
                table: "PrivacyConsents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "PrivacyConsents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "PrivacyConsents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedById",
                table: "PrivacyConsents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticeVersion",
                table: "PrivacyConsents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "legacy-unknown");

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "PrivacyConsents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "legacy-unknown");

            migrationBuilder.AddColumn<string>(
                name: "ProviderClass",
                table: "PrivacyConsents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "PrivacyConsents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetentionPolicyId",
                table: "PrivacyConsents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedById",
                table: "PrivacyConsents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PrivacyConsents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEntityId",
                table: "PrivacyConsents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "PrivacyConsents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsentId",
                table: "MeetingImports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentDeletedAt",
                table: "MeetingImports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentRedactedAt",
                table: "MeetingImports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataClassification",
                table: "MeetingImports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "unknown_sensitive");

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "MeetingImports",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyState",
                table: "MeetingImports",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "migration_review");

            migrationBuilder.AddColumn<string>(
                name: "ProcessingPurpose",
                table: "MeetingImports",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "meeting_action_extraction");

            migrationBuilder.AddColumn<string>(
                name: "ProviderClass",
                table: "MeetingImports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetentionExpiresAt",
                table: "MeetingImports",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetentionPolicyId",
                table: "MeetingImports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MeetingImports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "DataSubjectRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSDATETIMEOFFSET()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadlineAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "DataSubjectRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedResultPayload",
                table: "DataSubjectRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "DataSubjectRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IdentityVerifiedAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdentityVerifiedById",
                table: "DataSubjectRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDownloadedAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "DataSubjectRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "DataSubjectRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "DataSubjectRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegalHoldDetected",
                table: "DataSubjectRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegalHoldReason",
                table: "DataSubjectRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "DataSubjectRequests",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "DataSubjectRequests",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "DataSubjectRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultContentType",
                table: "DataSubjectRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResultExpiresAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultFileName",
                table: "DataSubjectRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultSummaryJson",
                table: "DataSubjectRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DataSubjectRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "DataSubjectRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectUserId",
                table: "DataSubjectRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataClassification",
                table: "AiAuditEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DataSubjectRequestId",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "AiAuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "AiAuditEvents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "AiAuditEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrivacyConsentId",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderClass",
                table: "AiAuditEvents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "AiAuditEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "AiAuditEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetentionPolicyId",
                table: "AiAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrivacyLegalHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    HeldAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HeldById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReleasedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyLegalHolds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DataClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AllowedRetentionDaysJson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultRetentionDays = table.Column<int>(type: "int", nullable: false),
                    ExpiryAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LegalHoldBehavior = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AllowCloudProcessing = table.Column<bool>(type: "bit", nullable: false),
                    AllowLocalProcessing = table.Column<bool>(type: "bit", nullable: false),
                    RequireExplicitConsent = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyRetentionActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetentionPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    LeaseOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyRetentionActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyRetentionActions_RetentionPolicies_RetentionPolicyId",
                        column: x => x.RetentionPolicyId,
                        principalTable: "RetentionPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE consent
                SET consent.[PolicyVersion] = N'legacy-unknown',
                    consent.[NoticeVersion] = N'legacy-unknown',
                    consent.[ProviderClass] = N'unknown',
                    consent.[SourceType] = N'legacy'
                FROM [PrivacyConsents] AS consent;

                UPDATE meetingImport
                SET meetingImport.[TenantId] = COALESCE(project.[OrganizationId], meetingImport.[ProjectId]),
                    meetingImport.[DataClassification] = N'unknown_sensitive',
                    meetingImport.[PrivacyState] = N'migration_review',
                    meetingImport.[ProcessingPurpose] = N'meeting_action_extraction',
                    meetingImport.[ProviderClass] = N'unknown'
                FROM [MeetingImports] AS meetingImport
                INNER JOIN [Projects] AS project ON project.[Id] = meetingImport.[ProjectId];

                UPDATE request
                SET request.[SubjectUserId] = COALESCE(request.[SubjectUserId], request.[RequesterUserId]),
                    request.[AvailableAt] = COALESCE(request.[RequestedAt], SYSDATETIMEOFFSET()),
                    request.[MaxAttempts] = 5,
                    request.[Status] = CASE LOWER(request.[Status])
                        WHEN N'pending' THEN N'submitted'
                        WHEN N'approved' THEN N'accepted'
                        WHEN N'processing' THEN N'collecting'
                        ELSE LOWER(request.[Status])
                    END
                FROM [DataSubjectRequests] AS request;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyConsents_RetentionPolicyId",
                table: "PrivacyConsents",
                column: "RetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyConsents_SourceType_SourceEntityId",
                table: "PrivacyConsents",
                columns: new[] { "SourceType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyConsents_TenantId_ProjectId_UserId",
                table: "PrivacyConsents",
                columns: new[] { "TenantId", "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_ConsentId",
                table: "MeetingImports",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_PrivacyState_RetentionExpiresAt",
                table: "MeetingImports",
                columns: new[] { "PrivacyState", "RetentionExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingImports_RetentionPolicyId",
                table: "MeetingImports",
                column: "RetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_AvailableAt_LeaseExpiresAt",
                table: "DataSubjectRequests",
                columns: new[] { "AvailableAt", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_TenantId_RequesterUserId_IdempotencyKey",
                table: "DataSubjectRequests",
                columns: new[] { "TenantId", "RequesterUserId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_TenantId_SubjectUserId_RequestedAt",
                table: "DataSubjectRequests",
                columns: new[] { "TenantId", "SubjectUserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_DataSubjectRequestId",
                table: "AiAuditEvents",
                column: "DataSubjectRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_PrivacyConsentId",
                table: "AiAuditEvents",
                column: "PrivacyConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAuditEvents_RetentionPolicyId",
                table: "AiAuditEvents",
                column: "RetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyLegalHolds_EntityType_EntityId_Status",
                table: "PrivacyLegalHolds",
                columns: new[] { "EntityType", "EntityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyLegalHolds_TenantId_ProjectId_SubjectUserId_Status",
                table: "PrivacyLegalHolds",
                columns: new[] { "TenantId", "ProjectId", "SubjectUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRetentionActions_EntityType_EntityId_ActionType",
                table: "PrivacyRetentionActions",
                columns: new[] { "EntityType", "EntityId", "ActionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRetentionActions_RetentionPolicyId",
                table: "PrivacyRetentionActions",
                column: "RetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRetentionActions_Status_AvailableAt_LeaseExpiresAt",
                table: "PrivacyRetentionActions",
                columns: new[] { "Status", "AvailableAt", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRetentionActions_TenantId_ProjectId_DueAt",
                table: "PrivacyRetentionActions",
                columns: new[] { "TenantId", "ProjectId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_ProjectId",
                table: "RetentionPolicies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_TenantId_PolicyVersion",
                table: "RetentionPolicies",
                columns: new[] { "TenantId", "PolicyVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_TenantId_ProjectId_DataClassification_Purpose_IsActive",
                table: "RetentionPolicies",
                columns: new[] { "TenantId", "ProjectId", "DataClassification", "Purpose", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingImports_PrivacyConsents_ConsentId",
                table: "MeetingImports",
                column: "ConsentId",
                principalTable: "PrivacyConsents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingImports_RetentionPolicies_RetentionPolicyId",
                table: "MeetingImports",
                column: "RetentionPolicyId",
                principalTable: "RetentionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrivacyConsents_RetentionPolicies_RetentionPolicyId",
                table: "PrivacyConsents",
                column: "RetentionPolicyId",
                principalTable: "RetentionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingImports_PrivacyConsents_ConsentId",
                table: "MeetingImports");

            migrationBuilder.DropForeignKey(
                name: "FK_MeetingImports_RetentionPolicies_RetentionPolicyId",
                table: "MeetingImports");

            migrationBuilder.DropForeignKey(
                name: "FK_PrivacyConsents_RetentionPolicies_RetentionPolicyId",
                table: "PrivacyConsents");

            migrationBuilder.DropTable(
                name: "PrivacyLegalHolds");

            migrationBuilder.DropTable(
                name: "PrivacyRetentionActions");

            migrationBuilder.DropTable(
                name: "RetentionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyConsents_RetentionPolicyId",
                table: "PrivacyConsents");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyConsents_SourceType_SourceEntityId",
                table: "PrivacyConsents");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyConsents_TenantId_ProjectId_UserId",
                table: "PrivacyConsents");

            migrationBuilder.DropIndex(
                name: "IX_MeetingImports_ConsentId",
                table: "MeetingImports");

            migrationBuilder.DropIndex(
                name: "IX_MeetingImports_PrivacyState_RetentionExpiresAt",
                table: "MeetingImports");

            migrationBuilder.DropIndex(
                name: "IX_MeetingImports_RetentionPolicyId",
                table: "MeetingImports");

            migrationBuilder.DropIndex(
                name: "IX_DataSubjectRequests_AvailableAt_LeaseExpiresAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropIndex(
                name: "IX_DataSubjectRequests_TenantId_RequesterUserId_IdempotencyKey",
                table: "DataSubjectRequests");

            migrationBuilder.DropIndex(
                name: "IX_DataSubjectRequests_TenantId_SubjectUserId_RequestedAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropIndex(
                name: "IX_AiAuditEvents_DataSubjectRequestId",
                table: "AiAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AiAuditEvents_PrivacyConsentId",
                table: "AiAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AiAuditEvents_RetentionPolicyId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "DeniedAt",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "EvidenceJson",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "GrantedById",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "NoticeVersion",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "ProviderClass",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "RevokedById",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "SourceEntityId",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "PrivacyConsents");

            migrationBuilder.DropColumn(
                name: "ConsentId",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "ContentDeletedAt",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "ContentRedactedAt",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "DataClassification",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "PrivacyState",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "ProcessingPurpose",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "ProviderClass",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "RetentionExpiresAt",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MeetingImports");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "DeadlineAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "DownloadCount",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "EncryptedResultPayload",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "IdentityVerifiedAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "IdentityVerifiedById",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LastDownloadedAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LegalHoldDetected",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "LegalHoldReason",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "ResultContentType",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "ResultExpiresAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "ResultFileName",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "ResultSummaryJson",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "SubjectUserId",
                table: "DataSubjectRequests");

            migrationBuilder.DropColumn(
                name: "DataClassification",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "DataSubjectRequestId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "PrivacyConsentId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "ProviderClass",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "AiAuditEvents");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyId",
                table: "AiAuditEvents");
        }
    }
}
