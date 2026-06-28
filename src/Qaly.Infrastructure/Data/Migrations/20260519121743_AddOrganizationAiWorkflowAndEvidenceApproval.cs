using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAiWorkflowAndEvidenceApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAttachments_TaskItemId'
                      AND object_id = OBJECT_ID(N'[TaskAttachments]')
                )
                BEGIN
                    DROP INDEX [IX_TaskAttachments_TaskItemId] ON [TaskAttachments];
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'EvidenceApprovalStatus') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [EvidenceApprovalStatus] nvarchar(20) NOT NULL CONSTRAINT [DF_TaskAttachments_EvidenceApprovalStatus] DEFAULT N'None';
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'EvidenceReviewNote') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [EvidenceReviewNote] nvarchar(1000) NULL;
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'EvidenceReviewedAt') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [EvidenceReviewedAt] datetimeoffset NULL;
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'EvidenceReviewedById') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [EvidenceReviewedById] uniqueidentifier NULL;
                END;

                IF COL_LENGTH(N'[TaskAttachments]', N'IsEvidence') IS NULL
                BEGIN
                    ALTER TABLE [TaskAttachments] ADD [IsEvidence] bit NOT NULL CONSTRAINT [DF_TaskAttachments_IsEvidence] DEFAULT 0;
                END;

                IF COL_LENGTH(N'[Projects]', N'OrganizationId') IS NULL
                BEGIN
                    ALTER TABLE [Projects] ADD [OrganizationId] uniqueidentifier NULL;
                END;

                IF OBJECT_ID(N'[AiJobs]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AiJobs] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_AiJobs] PRIMARY KEY DEFAULT NEWID(),
                        [JobType] nvarchar(80) NOT NULL,
                        [ProjectId] uniqueidentifier NOT NULL,
                        [SourceType] nvarchar(80) NOT NULL,
                        [SourceId] nvarchar(200) NULL,
                        [ProviderHint] nvarchar(40) NOT NULL,
                        [Sensitive] bit NOT NULL,
                        [Status] nvarchar(40) NOT NULL,
                        [EstimatedCostUsd] decimal(18,6) NOT NULL,
                        [CacheKey] nvarchar(200) NOT NULL,
                        [RequestedById] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_AiJobs_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF OBJECT_ID(N'[Organizations]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Organizations] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Organizations] PRIMARY KEY DEFAULT NEWID(),
                        [Name] nvarchar(200) NOT NULL,
                        [Code] nvarchar(80) NOT NULL,
                        [Description] nvarchar(2000) NULL,
                        [IsActive] bit NOT NULL CONSTRAINT [DF_Organizations_IsActive] DEFAULT 1,
                        [OwnerId] uniqueidentifier NOT NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Organizations_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF OBJECT_ID(N'[TaskAttentionSignals]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TaskAttentionSignals] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAttentionSignals] PRIMARY KEY DEFAULT NEWID(),
                        [TaskItemId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [SignalType] nvarchar(50) NOT NULL,
                        [FirstDetectedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAttentionSignals_FirstDetectedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [LastSentAt] datetimeoffset NULL,
                        [CooldownHours] int NOT NULL CONSTRAINT [DF_TaskAttentionSignals_CooldownHours] DEFAULT 24,
                        [ResolvedAt] datetimeoffset NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAttentionSignals_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF OBJECT_ID(N'[AiGeneratedDrafts]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AiGeneratedDrafts] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_AiGeneratedDrafts] PRIMARY KEY DEFAULT NEWID(),
                        [AiJobId] uniqueidentifier NOT NULL,
                        [ProjectId] uniqueidentifier NOT NULL,
                        [DraftType] nvarchar(80) NOT NULL,
                        [PayloadJson] nvarchar(max) NOT NULL,
                        [Status] nvarchar(40) NOT NULL,
                        [ConfirmedById] uniqueidentifier NULL,
                        [ConfirmedAt] datetimeoffset NULL,
                        [ConfirmAction] nvarchar(80) NULL,
                        [ConfirmationNote] nvarchar(1000) NULL,
                        [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_AiGeneratedDrafts_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF OBJECT_ID(N'[OrganizationMembers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [OrganizationMembers] (
                        [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_OrganizationMembers] PRIMARY KEY DEFAULT NEWID(),
                        [OrganizationId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [Role] nvarchar(20) NOT NULL,
                        [JoinedAt] datetimeoffset NOT NULL CONSTRAINT [DF_OrganizationMembers_JoinedAt] DEFAULT SYSDATETIMEOFFSET(),
                        [CreatedAt] datetimeoffset NOT NULL,
                        [UpdatedAt] datetimeoffset NULL
                    );
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAttachments_EvidenceReviewedById'
                      AND object_id = OBJECT_ID(N'[TaskAttachments]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAttachments_EvidenceReviewedById] ON [TaskAttachments]([EvidenceReviewedById]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAttachments_TaskItemId_IsEvidence_EvidenceApprovalStatus'
                      AND object_id = OBJECT_ID(N'[TaskAttachments]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAttachments_TaskItemId_IsEvidence_EvidenceApprovalStatus] ON [TaskAttachments]([TaskItemId], [IsEvidence], [EvidenceApprovalStatus]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Projects_OrganizationId'
                      AND object_id = OBJECT_ID(N'[Projects]')
                )
                BEGIN
                    CREATE INDEX [IX_Projects_OrganizationId] ON [Projects]([OrganizationId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiGeneratedDrafts_AiJobId'
                      AND object_id = OBJECT_ID(N'[AiGeneratedDrafts]')
                )
                BEGIN
                    CREATE INDEX [IX_AiGeneratedDrafts_AiJobId] ON [AiGeneratedDrafts]([AiJobId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiGeneratedDrafts_ConfirmedById'
                      AND object_id = OBJECT_ID(N'[AiGeneratedDrafts]')
                )
                BEGIN
                    CREATE INDEX [IX_AiGeneratedDrafts_ConfirmedById] ON [AiGeneratedDrafts]([ConfirmedById]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiGeneratedDrafts_ProjectId_Status'
                      AND object_id = OBJECT_ID(N'[AiGeneratedDrafts]')
                )
                BEGIN
                    CREATE INDEX [IX_AiGeneratedDrafts_ProjectId_Status] ON [AiGeneratedDrafts]([ProjectId], [Status]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiJobs_CacheKey'
                      AND object_id = OBJECT_ID(N'[AiJobs]')
                )
                BEGIN
                    CREATE INDEX [IX_AiJobs_CacheKey] ON [AiJobs]([CacheKey]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiJobs_ProjectId_CreatedAt'
                      AND object_id = OBJECT_ID(N'[AiJobs]')
                )
                BEGIN
                    CREATE INDEX [IX_AiJobs_ProjectId_CreatedAt] ON [AiJobs]([ProjectId], [CreatedAt]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_AiJobs_RequestedById'
                      AND object_id = OBJECT_ID(N'[AiJobs]')
                )
                BEGIN
                    CREATE INDEX [IX_AiJobs_RequestedById] ON [AiJobs]([RequestedById]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OrganizationMembers_OrganizationId_UserId'
                      AND object_id = OBJECT_ID(N'[OrganizationMembers]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_OrganizationMembers_OrganizationId_UserId] ON [OrganizationMembers]([OrganizationId], [UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OrganizationMembers_UserId'
                      AND object_id = OBJECT_ID(N'[OrganizationMembers]')
                )
                BEGIN
                    CREATE INDEX [IX_OrganizationMembers_UserId] ON [OrganizationMembers]([UserId]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Organizations_Code'
                      AND object_id = OBJECT_ID(N'[Organizations]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Organizations_Code] ON [Organizations]([Code]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Organizations_OwnerId_IsActive'
                      AND object_id = OBJECT_ID(N'[Organizations]')
                )
                BEGIN
                    CREATE INDEX [IX_Organizations_OwnerId_IsActive] ON [Organizations]([OwnerId], [IsActive]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAttentionSignals_TaskItemId_UserId_SignalType'
                      AND object_id = OBJECT_ID(N'[TaskAttentionSignals]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_TaskAttentionSignals_TaskItemId_UserId_SignalType] ON [TaskAttentionSignals]([TaskItemId], [UserId], [SignalType]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TaskAttentionSignals_UserId_ResolvedAt_LastSentAt'
                      AND object_id = OBJECT_ID(N'[TaskAttentionSignals]')
                )
                BEGIN
                    CREATE INDEX [IX_TaskAttentionSignals_UserId_ResolvedAt_LastSentAt] ON [TaskAttentionSignals]([UserId], [ResolvedAt], [LastSentAt]);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Projects_Organizations_OrganizationId'
                )
                BEGIN
                    ALTER TABLE [Projects]
                        ADD CONSTRAINT [FK_Projects_Organizations_OrganizationId]
                        FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAttachments_Users_EvidenceReviewedById'
                )
                BEGIN
                    ALTER TABLE [TaskAttachments]
                        ADD CONSTRAINT [FK_TaskAttachments_Users_EvidenceReviewedById]
                        FOREIGN KEY ([EvidenceReviewedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_AiJobs_Projects_ProjectId'
                )
                BEGIN
                    ALTER TABLE [AiJobs]
                        ADD CONSTRAINT [FK_AiJobs_Projects_ProjectId]
                        FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_AiJobs_Users_RequestedById'
                )
                BEGIN
                    ALTER TABLE [AiJobs]
                        ADD CONSTRAINT [FK_AiJobs_Users_RequestedById]
                        FOREIGN KEY ([RequestedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAttentionSignals_TaskItems_TaskItemId'
                )
                BEGIN
                    ALTER TABLE [TaskAttentionSignals]
                        ADD CONSTRAINT [FK_TaskAttentionSignals_TaskItems_TaskItemId]
                        FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_TaskAttentionSignals_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [TaskAttentionSignals]
                        ADD CONSTRAINT [FK_TaskAttentionSignals_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_AiGeneratedDrafts_AiJobs_AiJobId'
                )
                BEGIN
                    ALTER TABLE [AiGeneratedDrafts]
                        ADD CONSTRAINT [FK_AiGeneratedDrafts_AiJobs_AiJobId]
                        FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_AiGeneratedDrafts_Projects_ProjectId'
                )
                BEGIN
                    ALTER TABLE [AiGeneratedDrafts]
                        ADD CONSTRAINT [FK_AiGeneratedDrafts_Projects_ProjectId]
                        FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_AiGeneratedDrafts_Users_ConfirmedById'
                )
                BEGIN
                    ALTER TABLE [AiGeneratedDrafts]
                        ADD CONSTRAINT [FK_AiGeneratedDrafts_Users_ConfirmedById]
                        FOREIGN KEY ([ConfirmedById]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_OrganizationMembers_Organizations_OrganizationId'
                )
                BEGIN
                    ALTER TABLE [OrganizationMembers]
                        ADD CONSTRAINT [FK_OrganizationMembers_Organizations_OrganizationId]
                        FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations]([Id]) ON DELETE CASCADE;
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_OrganizationMembers_Users_UserId'
                )
                BEGIN
                    ALTER TABLE [OrganizationMembers]
                        ADD CONSTRAINT [FK_OrganizationMembers_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAttachments_Users_EvidenceReviewedById",
                table: "TaskAttachments");

            migrationBuilder.DropTable(
                name: "AiGeneratedDrafts");

            migrationBuilder.DropTable(
                name: "OrganizationMembers");

            migrationBuilder.DropTable(
                name: "TaskAttentionSignals");

            migrationBuilder.DropTable(
                name: "AiJobs");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_EvidenceReviewedById",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAttachments_TaskItemId_IsEvidence_EvidenceApprovalStatus",
                table: "TaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EvidenceApprovalStatus",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "EvidenceReviewNote",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "EvidenceReviewedAt",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "EvidenceReviewedById",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "IsEvidence",
                table: "TaskAttachments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttachments_TaskItemId",
                table: "TaskAttachments",
                column: "TaskItemId");
        }
    }
}
