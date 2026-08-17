/*
    QALY DATABASE INSTALLATION SCRIPT
    Target: Microsoft SQL Server 2022 / SQL Server Express / LocalDB
    Database: QalyDb

    Generated from all EF Core migrations through:
    20260811120548_P014ProjectRoleDefinitions

    The script is idempotent: it can be executed again on a database that
    already contains some or all migrations.
*/

USE [master];
GO

IF DB_ID(N'QalyDb') IS NULL
BEGIN
    CREATE DATABASE [QalyDb];
END;
GO

USE [QalyDb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [AvatarUrl] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [Action] nvarchar(50) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [ChangesJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(45) NULL,
        [Timestamp] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UserId] uniqueidentifier NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Message] nvarchar(500) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RelatedEntityId] uniqueidentifier NULL,
        [RelatedEntityType] nvarchar(50) NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartDate] datetimeoffset NULL,
        [EndDate] datetimeoffset NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [ProjectMembers] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [JoinedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [TaskItems] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Todo',
        [Priority] nvarchar(20) NOT NULL DEFAULT N'Medium',
        [DueDate] datetimeoffset NULL,
        [EstimatedHours] int NULL,
        [ActualHours] int NULL,
        [IsPrivate] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ProjectId] uniqueidentifier NOT NULL,
        [AssigneeId] uniqueidentifier NULL,
        [ReporterId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskItems_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskItems_Users_AssigneeId] FOREIGN KEY ([AssigneeId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskItems_Users_ReporterId] FOREIGN KEY ([ReporterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [TaskAttachments] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [FileName] nvarchar(500) NOT NULL,
        [FilePath] nvarchar(max) NOT NULL,
        [FileSize] bigint NOT NULL,
        [ContentType] nvarchar(100) NULL,
        [UploadedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [TaskItemId] uniqueidentifier NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskAttachments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskAttachments_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE TABLE [TaskComments] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Content] nvarchar(max) NOT NULL,
        [TaskItemId] uniqueidentifier NOT NULL,
        [AuthorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskComments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskComments_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp] DESC);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectMembers_ProjectId_UserId] ON [ProjectMembers] ([ProjectId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_UserId] ON [ProjectMembers] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Projects_OwnerId] ON [Projects] ([OwnerId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskAttachments_TaskItemId] ON [TaskAttachments] ([TaskItemId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskAttachments_UploadedById] ON [TaskAttachments] ([UploadedById]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskComments_AuthorId] ON [TaskComments] ([AuthorId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskComments_TaskItemId] ON [TaskComments] ([TaskItemId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskItems_AssigneeId] ON [TaskItems] ([AssigneeId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ProjectId] ON [TaskItems] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ReporterId] ON [TaskItems] ([ReporterId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TaskItems_Status] ON [TaskItems] ([Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501140908_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501140908_InitialCreate', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505084038_AddWikiPages'
)
BEGIN
    CREATE TABLE [WikiPages] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AuthorId] uniqueidentifier NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WikiPages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WikiPages_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WikiPages_Users_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505084038_AddWikiPages'
)
BEGIN
    CREATE INDEX [IX_WikiPages_AuthorId] ON [WikiPages] ([AuthorId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505084038_AddWikiPages'
)
BEGIN
    CREATE INDEX [IX_WikiPages_ProjectId] ON [WikiPages] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505084038_AddWikiPages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505084038_AddWikiPages', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE TABLE [PushSubscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Endpoint] nvarchar(max) NOT NULL,
        [P256dh] nvarchar(max) NULL,
        [Auth] nvarchar(max) NULL,
        [Device] nvarchar(max) NOT NULL,
        [LastUsedAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PushSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PushSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE TABLE [TimeEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NULL,
        [ManualMinutes] int NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TimeEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeEntries_TaskItems_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TimeEntries_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE TABLE [VectorSyncOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [RetryCount] int NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_VectorSyncOutbox] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE INDEX [IX_PushSubscriptions_UserId] ON [PushSubscriptions] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_TaskId] ON [TimeEntries] ([TaskId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_UserId] ON [TimeEntries] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508043517_AddOperationalTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508043517_AddOperationalTables', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509024355_AddTaskSortOrder'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509024355_AddTaskSortOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509024355_AddTaskSortOrder', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    DROP INDEX [IX_TaskComments_TaskItemId] ON [TaskComments];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    DROP INDEX [IX_Notifications_UserId_IsRead] ON [Notifications];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ProjectId_Status] ON [TaskItems] ([ProjectId], [Status]) INCLUDE ([Title], [Priority], [AssigneeId], [DueDate]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    CREATE INDEX [IX_TaskComments_TaskItemId_CreatedAt] ON [TaskComments] ([TaskItemId], [CreatedAt] DESC);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]) INCLUDE ([CreatedAt], [Message]) WHERE [IsRead] = 0');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509054047_UpdateModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509054047_UpdateModel', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [StartDate] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE TABLE [ApiKeys] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [KeyHash] nvarchar(200) NOT NULL,
        [Prefix] nvarchar(16) NOT NULL,
        [Scopes] nvarchar(1000) NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [LastUsedAt] datetimeoffset NULL,
        [IsRevoked] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApiKeys_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE TABLE [TaskDependencies] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [PredecessorId] uniqueidentifier NOT NULL,
        [SuccessorId] uniqueidentifier NOT NULL,
        [DependencyType] nvarchar(20) NOT NULL DEFAULT N'FinishToStart',
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskDependencies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskDependencies_TaskItems_PredecessorId] FOREIGN KEY ([PredecessorId]) REFERENCES [TaskItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskDependencies_TaskItems_SuccessorId] FOREIGN KEY ([SuccessorId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE TABLE [WebhookSubscriptions] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ProjectId] uniqueidentifier NOT NULL,
        [PayloadUrl] nvarchar(500) NOT NULL,
        [Secret] nvarchar(100) NOT NULL,
        [Events] nvarchar(2000) NOT NULL,
        [IsActive] bit NOT NULL,
        [FailureCount] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebhookSubscriptions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE TABLE [WebhookDeliveryLogs] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WebhookId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [RequestPayload] nvarchar(max) NOT NULL,
        [ResponseStatusCode] int NULL,
        [ResponseBody] nvarchar(max) NULL,
        [DurationMs] bigint NOT NULL,
        [IsSuccess] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_WebhookDeliveryLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebhookDeliveryLogs_WebhookSubscriptions_WebhookId] FOREIGN KEY ([WebhookId]) REFERENCES [WebhookSubscriptions] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_Prefix] ON [ApiKeys] ([Prefix]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_UserId] ON [ApiKeys] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskDependencies_PredecessorId_SuccessorId] ON [TaskDependencies] ([PredecessorId], [SuccessorId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_TaskDependencies_SuccessorId] ON [TaskDependencies] ([SuccessorId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_WebhookDeliveryLogs_CreatedAt] ON [WebhookDeliveryLogs] ([CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_WebhookDeliveryLogs_WebhookId] ON [WebhookDeliveryLogs] ([WebhookId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    CREATE INDEX [IX_WebhookSubscriptions_ProjectId] ON [WebhookSubscriptions] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512013940_AddTaskStartDate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512013940_AddTaskStartDate', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[ImportSessions]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ImportSessions] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ImportSessions] PRIMARY KEY DEFAULT NEWID(),
            [ProjectId] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [FileName] nvarchar(256) NOT NULL,
            [TotalRows] int NOT NULL CONSTRAINT [DF_ImportSessions_TotalRows] DEFAULT 0,
            [ImportedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_ImportedCount] DEFAULT 0,
            [SkippedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_SkippedCount] DEFAULT 0,
            [IsUndone] bit NOT NULL CONSTRAINT [DF_ImportSessions_IsUndone] DEFAULT 0,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ImportSessions_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[ProjectLabels]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ProjectLabels] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ProjectLabels] PRIMARY KEY DEFAULT NEWID(),
            [Color] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectLabels_Color] DEFAULT N'#64748B',
            [Name] nvarchar(80) NOT NULL,
            [ProjectId] uniqueidentifier NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ProjectLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[TaskAssignments]', N'U') IS NULL
    BEGIN
        CREATE TABLE [TaskAssignments] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAssignments] PRIMARY KEY DEFAULT NEWID(),
            [TaskItemId] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[TaskLabels]', N'U') IS NULL
    BEGIN
        CREATE TABLE [TaskLabels] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskLabels] PRIMARY KEY DEFAULT NEWID(),
            [ProjectLabelId] uniqueidentifier NOT NULL,
            [TaskItemId] uniqueidentifier NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskLabels_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[Votes]', N'U') IS NULL
    BEGIN
        CREATE TABLE [Votes] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_Votes] PRIMARY KEY DEFAULT NEWID(),
            [TargetId] uniqueidentifier NOT NULL,
            [TargetType] nvarchar(20) NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [Value] int NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Votes_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[Projects]', N'Code') IS NULL
    BEGIN
        ALTER TABLE [Projects] ADD [Code] nvarchar(80) NULL;
    END;

    IF COL_LENGTH(N'[Projects]', N'LogoUrl') IS NULL
    BEGIN
        ALTER TABLE [Projects] ADD [LogoUrl] nvarchar(1000) NULL;
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    UPDATE [Projects]
    SET [Code] = CONCAT(N'project-', REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))
    WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = N'';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[Projects]')
          AND name = N'Code'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE [Projects] ALTER COLUMN [Code] nvarchar(80) NOT NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Projects_Code'
          AND object_id = OBJECT_ID(N'[Projects]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[TaskItems]', N'IsPinned') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [IsPinned] bit NOT NULL CONSTRAINT [DF_TaskItems_IsPinned] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[TaskItems]', N'ContributesToProgress') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [ContributesToProgress] bit NOT NULL CONSTRAINT [DF_TaskItems_ContributesToProgress] DEFAULT 1;
    END;

    IF COL_LENGTH(N'[TaskItems]', N'UpvoteCount') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_UpvoteCount] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[TaskItems]', N'DownvoteCount') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskItems_DownvoteCount] DEFAULT 0;
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[TaskComments]', N'ParentCommentId') IS NULL
    BEGIN
        ALTER TABLE [TaskComments] ADD [ParentCommentId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[TaskComments]', N'UpvoteCount') IS NULL
    BEGIN
        ALTER TABLE [TaskComments] ADD [UpvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_UpvoteCount] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[TaskComments]', N'DownvoteCount') IS NULL
    BEGIN
        ALTER TABLE [TaskComments] ADD [DownvoteCount] int NOT NULL CONSTRAINT [DF_TaskComments_DownvoteCount] DEFAULT 0;
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[TaskAttachments]', N'ProjectId') IS NULL
    BEGIN
        ALTER TABLE [TaskAttachments] ADD [ProjectId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[TaskAttachments]', N'CommentId') IS NULL
    BEGIN
        ALTER TABLE [TaskAttachments] ADD [CommentId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[TaskAttachments]', N'Scope') IS NULL
    BEGIN
        ALTER TABLE [TaskAttachments] ADD [Scope] nvarchar(20) NOT NULL CONSTRAINT [DF_TaskAttachments_Scope] DEFAULT N'Task';
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ProjectLabels_ProjectId_Name'
          AND object_id = OBJECT_ID(N'[ProjectLabels]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_ProjectLabels_ProjectId_Name] ON [ProjectLabels]([ProjectId], [Name]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ProjectLabels_Projects_ProjectId'
    )
    BEGIN
        ALTER TABLE [ProjectLabels] ADD CONSTRAINT [FK_ProjectLabels_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskAssignments_UserId'
          AND object_id = OBJECT_ID(N'[TaskAssignments]')
    )
    BEGIN
        CREATE INDEX [IX_TaskAssignments_UserId] ON [TaskAssignments]([UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskAssignments_TaskItemId_UserId'
          AND object_id = OBJECT_ID(N'[TaskAssignments]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_TaskAssignments_TaskItemId_UserId] ON [TaskAssignments]([TaskItemId], [UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskAssignments_TaskItems_TaskItemId'
    )
    BEGIN
        ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskAssignments_Users_UserId'
    )
    BEGIN
        ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskLabels_ProjectLabelId'
          AND object_id = OBJECT_ID(N'[TaskLabels]')
    )
    BEGIN
        CREATE INDEX [IX_TaskLabels_ProjectLabelId] ON [TaskLabels]([ProjectLabelId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskLabels_TaskItemId_ProjectLabelId'
          AND object_id = OBJECT_ID(N'[TaskLabels]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_TaskLabels_TaskItemId_ProjectLabelId] ON [TaskLabels]([TaskItemId], [ProjectLabelId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskLabels_ProjectLabels_ProjectLabelId'
    )
    BEGIN
        ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_ProjectLabels_ProjectLabelId] FOREIGN KEY ([ProjectLabelId]) REFERENCES [ProjectLabels]([Id]) ON DELETE NO ACTION;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskLabels_TaskItems_TaskItemId'
    )
    BEGIN
        ALTER TABLE [TaskLabels] ADD CONSTRAINT [FK_TaskLabels_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Votes_UserId'
          AND object_id = OBJECT_ID(N'[Votes]')
    )
    BEGIN
        CREATE INDEX [IX_Votes_UserId] ON [Votes]([UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Votes_TargetType_TargetId'
          AND object_id = OBJECT_ID(N'[Votes]')
    )
    BEGIN
        CREATE INDEX [IX_Votes_TargetType_TargetId] ON [Votes]([TargetType], [TargetId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Votes_TargetType_TargetId_UserId'
          AND object_id = OBJECT_ID(N'[Votes]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_Votes_TargetType_TargetId_UserId] ON [Votes]([TargetType], [TargetId], [UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Votes_Users_UserId'
    )
    BEGIN
        ALTER TABLE [Votes] ADD CONSTRAINT [FK_Votes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ImportSessions_Projects_ProjectId'
    )
    BEGIN
        ALTER TABLE [ImportSessions]
            ADD CONSTRAINT [FK_ImportSessions_Projects_ProjectId]
            FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_ImportSessions_Users_UserId'
    )
    BEGIN
        ALTER TABLE [ImportSessions]
            ADD CONSTRAINT [FK_ImportSessions_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE NO ACTION;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
    )
    BEGIN
        ALTER TABLE [TaskItems]
            ADD CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId]
            FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]) ON DELETE NO ACTION;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ImportSessions_ProjectId'
          AND object_id = OBJECT_ID(N'[ImportSessions]')
    )
    BEGIN
        CREATE INDEX [IX_ImportSessions_ProjectId] ON [ImportSessions]([ProjectId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_ImportSessions_UserId'
          AND object_id = OBJECT_ID(N'[ImportSessions]')
    )
    BEGIN
        CREATE INDEX [IX_ImportSessions_UserId] ON [ImportSessions]([UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskItems_ImportSessionId'
          AND object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        CREATE INDEX [IX_TaskItems_ImportSessionId] ON [TaskItems]([ImportSessionId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Projects_Code'
          AND object_id = OBJECT_ID(N'[Projects]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects]([Code]);
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515033249_PendingChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515033249_PendingChanges', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517084404_AddTaskTimelineAttention'
)
BEGIN
    IF OBJECT_ID(N'[ImportSessions]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ImportSessions] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_ImportSessions] PRIMARY KEY DEFAULT NEWID(),
            [ProjectId] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [FileName] nvarchar(256) NOT NULL,
            [TotalRows] int NOT NULL CONSTRAINT [DF_ImportSessions_TotalRows] DEFAULT 0,
            [ImportedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_ImportedCount] DEFAULT 0,
            [SkippedCount] int NOT NULL CONSTRAINT [DF_ImportSessions_SkippedCount] DEFAULT 0,
            [IsUndone] bit NOT NULL CONSTRAINT [DF_ImportSessions_IsUndone] DEFAULT 0,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_ImportSessions_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;

    IF OBJECT_ID(N'[TaskAssignments]', N'U') IS NULL
    BEGIN
        CREATE TABLE [TaskAssignments] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskAssignments] PRIMARY KEY DEFAULT NEWID(),
            [TaskItemId] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;

    IF COL_LENGTH(N'[TaskItems]', N'ImportSessionId') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [ImportSessionId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[TaskAssignments]', N'AssignedAt') IS NULL
    BEGIN
        ALTER TABLE [TaskAssignments] ADD [AssignedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskAssignments_AssignedAt] DEFAULT SYSDATETIMEOFFSET();
    END;

    IF COL_LENGTH(N'[TaskAssignments]', N'AssignedByUserId') IS NULL
    BEGIN
        ALTER TABLE [TaskAssignments] ADD [AssignedByUserId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[ProjectMembers]', N'CanNudgeAssignee') IS NULL
    BEGIN
        ALTER TABLE [ProjectMembers] ADD [CanNudgeAssignee] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanNudgeAssignee] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[ProjectMembers]', N'CanViewProjectTimeline') IS NULL
    BEGIN
        ALTER TABLE [ProjectMembers] ADD [CanViewProjectTimeline] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewProjectTimeline] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[ProjectMembers]', N'CanViewTaskRisk') IS NULL
    BEGIN
        ALTER TABLE [ProjectMembers] ADD [CanViewTaskRisk] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewTaskRisk] DEFAULT 0;
    END;

    IF COL_LENGTH(N'[ProjectMembers]', N'CanViewUnseenTaskSignal') IS NULL
    BEGIN
        ALTER TABLE [ProjectMembers] ADD [CanViewUnseenTaskSignal] bit NOT NULL CONSTRAINT [DF_ProjectMembers_CanViewUnseenTaskSignal] DEFAULT 0;
    END;

    IF OBJECT_ID(N'[TaskViewEvents]', N'U') IS NULL
    BEGIN
        CREATE TABLE [TaskViewEvents] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_TaskViewEvents] PRIMARY KEY DEFAULT NEWID(),
            [TaskItemId] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [ViewedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewedAt] DEFAULT SYSDATETIMEOFFSET(),
            [ViewCount] int NOT NULL CONSTRAINT [DF_TaskViewEvents_ViewCount] DEFAULT 1,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_TaskViewEvents_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL
        );
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517084404_AddTaskTimelineAttention'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
          AND parent_object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        ALTER TABLE [TaskItems] DROP CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId];
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517084404_AddTaskTimelineAttention'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskAssignments_AssignedAt'
          AND object_id = OBJECT_ID(N'[TaskAssignments]')
    )
    BEGIN
        CREATE INDEX [IX_TaskAssignments_AssignedAt] ON [TaskAssignments]([AssignedAt]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskAssignments_AssignedByUserId'
          AND object_id = OBJECT_ID(N'[TaskAssignments]')
    )
    BEGIN
        CREATE INDEX [IX_TaskAssignments_AssignedByUserId] ON [TaskAssignments]([AssignedByUserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskViewEvents_TaskItemId_UserId'
          AND object_id = OBJECT_ID(N'[TaskViewEvents]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_TaskViewEvents_TaskItemId_UserId]
        ON [TaskViewEvents]([TaskItemId], [UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskViewEvents_UserId'
          AND object_id = OBJECT_ID(N'[TaskViewEvents]')
    )
    BEGIN
        CREATE INDEX [IX_TaskViewEvents_UserId] ON [TaskViewEvents]([UserId]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskViewEvents_ViewedAt'
          AND object_id = OBJECT_ID(N'[TaskViewEvents]')
    )
    BEGIN
        CREATE INDEX [IX_TaskViewEvents_ViewedAt] ON [TaskViewEvents]([ViewedAt]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskAssignments_Users_AssignedByUserId'
    )
    BEGIN
        ALTER TABLE [TaskAssignments]
            ADD CONSTRAINT [FK_TaskAssignments_Users_AssignedByUserId]
            FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users]([Id]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskViewEvents_TaskItems_TaskItemId'
    )
    BEGIN
        ALTER TABLE [TaskViewEvents]
            ADD CONSTRAINT [FK_TaskViewEvents_TaskItems_TaskItemId]
            FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems]([Id]) ON DELETE CASCADE;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskViewEvents_Users_UserId'
    )
    BEGIN
        ALTER TABLE [TaskViewEvents]
            ADD CONSTRAINT [FK_TaskViewEvents_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE;
    END;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517084404_AddTaskTimelineAttention'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskItems_ImportSessions_ImportSessionId'
          AND parent_object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        ALTER TABLE [TaskItems]
            ADD CONSTRAINT [FK_TaskItems_ImportSessions_ImportSessionId]
            FOREIGN KEY ([ImportSessionId]) REFERENCES [ImportSessions]([Id]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517084404_AddTaskTimelineAttention'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517084404_AddTaskTimelineAttention', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519121743_AddOrganizationAiWorkflowAndEvidenceApproval'
)
BEGIN
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
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519121743_AddOrganizationAiWorkflowAndEvidenceApproval'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519121743_AddOrganizationAiWorkflowAndEvidenceApproval', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    CREATE TABLE [MeetingImports] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ProjectId] uniqueidentifier NOT NULL,
        [ImportedById] uniqueidentifier NOT NULL,
        [SourceProvider] nvarchar(40) NOT NULL,
        [SourceId] nvarchar(200) NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [MeetingStartedAt] datetimeoffset NULL,
        [Summary] nvarchar(4000) NULL,
        [TranscriptText] nvarchar(max) NOT NULL,
        [ParticipantsJson] nvarchar(max) NOT NULL,
        [RawPayloadJson] nvarchar(max) NOT NULL,
        [AiJobId] uniqueidentifier NULL,
        [AiDraftId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MeetingImports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingImports_AiGeneratedDrafts_AiDraftId] FOREIGN KEY ([AiDraftId]) REFERENCES [AiGeneratedDrafts] ([Id]),
        CONSTRAINT [FK_MeetingImports_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]),
        CONSTRAINT [FK_MeetingImports_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MeetingImports_Users_ImportedById] FOREIGN KEY ([ImportedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_AiDraftId] ON [MeetingImports] ([AiDraftId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_AiJobId] ON [MeetingImports] ([AiJobId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_ImportedById] ON [MeetingImports] ([ImportedById]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MeetingImports_ProjectId_SourceProvider_SourceHash] ON [MeetingImports] ([ProjectId], [SourceProvider], [SourceHash]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521074951_AddMeetilyMeetingImports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521074951_AddMeetilyMeetingImports', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    IF COL_LENGTH(N'TaskItems', N'RowVersion') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [RowVersion] rowversion NOT NULL;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    CREATE TABLE [MeetingActionItemMappings] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [MeetingImportId] uniqueidentifier NOT NULL,
        [ActionItemIndex] int NOT NULL,
        [TaskId] uniqueidentifier NULL,
        [Status] nvarchar(40) NOT NULL,
        [SourceTitle] nvarchar(300) NULL,
        [SourcePriority] nvarchar(20) NULL,
        [SourceDueDate] datetimeoffset NULL,
        [SourceQuote] nvarchar(2000) NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MeetingActionItemMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeetingActionItemMappings_MeetingImports_MeetingImportId] FOREIGN KEY ([MeetingImportId]) REFERENCES [MeetingImports] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MeetingActionItemMappings_TaskItems_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [TaskItems] ([Id]),
        CONSTRAINT [FK_MeetingActionItemMappings_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    CREATE INDEX [IX_MeetingActionItemMappings_CreatedById] ON [MeetingActionItemMappings] ([CreatedById]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MeetingActionItemMappings_MeetingImportId_ActionItemIndex] ON [MeetingActionItemMappings] ([MeetingImportId], [ActionItemIndex]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    CREATE INDEX [IX_MeetingActionItemMappings_TaskId] ON [MeetingActionItemMappings] ([TaskId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260522081213_AddMeetingActionItemMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260522081213_AddMeetingActionItemMapping', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523104818_FinalPendingChanges'
)
BEGIN
    CREATE INDEX [IX_TaskItems_DueDate] ON [TaskItems] ([DueDate]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523104818_FinalPendingChanges'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ProjectId_AssigneeId_Status] ON [TaskItems] ([ProjectId], [AssigneeId], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523104818_FinalPendingChanges'
)
BEGIN
    CREATE INDEX [IX_TaskItems_ProjectId_DueDate] ON [TaskItems] ([ProjectId], [DueDate]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523104818_FinalPendingChanges'
)
BEGIN
    CREATE INDEX [IX_TaskItems_StartDate] ON [TaskItems] ([StartDate]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523104818_FinalPendingChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523104818_FinalPendingChanges', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [AiBudgetPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [DailyBudgetUsd] decimal(18,2) NOT NULL,
        [MonthlyBudgetUsd] decimal(18,2) NOT NULL,
        [WarnAtPercent] int NOT NULL,
        [HardStopEnabled] bit NOT NULL,
        [AllowCloudForSensitive] bit NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiBudgetPolicies] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [AiJobQueue] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [RequestedBy] uniqueidentifier NULL,
        [JobType] nvarchar(max) NOT NULL,
        [SchemaId] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] int NOT NULL,
        [Sensitive] bit NOT NULL,
        [ConsentId] bigint NULL,
        [ProviderHint] nvarchar(max) NULL,
        [InputRefType] nvarchar(max) NULL,
        [InputRefId] bigint NULL,
        [PayloadJson] nvarchar(max) NULL,
        [ResultJson] nvarchar(max) NULL,
        [RetryCount] int NOT NULL,
        [MaxRetry] int NOT NULL,
        [ErrorCode] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [EstimatedCostUsd] decimal(18,2) NULL,
        [StartedAt] datetimeoffset NULL,
        [FinishedAt] datetimeoffset NULL,
        [CanceledAt] datetimeoffset NULL,
        [CanceledBy] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiJobQueue] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [AiPromptCache] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [CacheKey] nvarchar(max) NOT NULL,
        [JobType] nvarchar(max) NOT NULL,
        [SchemaId] nvarchar(max) NOT NULL,
        [ProviderName] nvarchar(max) NULL,
        [ModelName] nvarchar(max) NULL,
        [RequestHash] nvarchar(max) NOT NULL,
        [ResponseJson] nvarchar(max) NOT NULL,
        [HitCount] int NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiPromptCache] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [AiProviderConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProviderName] nvarchar(max) NOT NULL,
        [ModelName] nvarchar(max) NOT NULL,
        [Purpose] nvarchar(max) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [PriorityOrder] int NOT NULL,
        [MaxInputTokens] int NOT NULL,
        [MaxOutputTokens] int NOT NULL,
        [CostInputPer1MUsd] decimal(18,2) NULL,
        [CostOutputPer1MUsd] decimal(18,2) NULL,
        [DataPolicy] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiProviderConfigs] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [AiUsageLedger] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [JobId] bigint NULL,
        [JobType] nvarchar(max) NOT NULL,
        [ProviderName] nvarchar(max) NOT NULL,
        [ModelName] nvarchar(max) NOT NULL,
        [InputTokens] int NOT NULL,
        [OutputTokens] int NOT NULL,
        [EstimatedCostUsd] decimal(18,2) NOT NULL,
        [LatencyMs] int NULL,
        [Status] nvarchar(max) NOT NULL,
        [CacheHit] bit NOT NULL,
        [PromptHash] nvarchar(max) NULL,
        [ResponseHash] nvarchar(max) NULL,
        [ErrorCode] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiUsageLedger] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [DataSubjectRequests] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [RequesterUserId] uniqueidentifier NULL,
        [RequestType] nvarchar(max) NOT NULL,
        [ScopeJson] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [RequestedAt] datetimeoffset NOT NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [CompletedAt] datetimeoffset NULL,
        [RejectionReason] nvarchar(max) NULL,
        [EvidenceUri] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_DataSubjectRequests] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    CREATE TABLE [PrivacyConsents] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [ConsentType] nvarchar(max) NOT NULL,
        [Purpose] nvarchar(max) NOT NULL,
        [ScopeJson] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [GrantedAt] datetimeoffset NOT NULL,
        [RevokedAt] datetimeoffset NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PrivacyConsents] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114241_AddAiComplianceAndCostControl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523114241_AddAiComplianceAndCostControl', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114422_AddAiAuditEvents'
)
BEGIN
    CREATE TABLE [AiAuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NULL,
        [EventType] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NULL,
        [EntityId] bigint NULL,
        [JobId] bigint NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiAuditEvents] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523114422_AddAiAuditEvents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523114422_AddAiAuditEvents', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_WikiPages_ProjectId'
          AND object_id = OBJECT_ID(N'[WikiPages]')
    )
    BEGIN
        DROP INDEX [IX_WikiPages_ProjectId] ON [WikiPages];
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[WikiPages]', N'IsPublic') IS NULL
    BEGIN
        ALTER TABLE [WikiPages] ADD [IsPublic] bit NOT NULL CONSTRAINT [DF_WikiPages_IsPublic] DEFAULT 0;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[WikiPages]', N'Visibility') IS NULL
    BEGIN
        ALTER TABLE [WikiPages] ADD [Visibility] nvarchar(50) NOT NULL CONSTRAINT [DF_WikiPages_Visibility] DEFAULT N'internal';
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'AttemptCount') IS NULL
    BEGIN
        ALTER TABLE [WebhookDeliveryLogs] ADD [AttemptCount] int NOT NULL CONSTRAINT [DF_WebhookDeliveryLogs_AttemptCount] DEFAULT 1;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[WebhookDeliveryLogs]', N'IdempotencyKey') IS NULL
    BEGIN
        ALTER TABLE [WebhookDeliveryLogs] ADD [IdempotencyKey] nvarchar(200) NULL;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[TaskItems]', N'SprintId') IS NULL
    BEGIN
        ALTER TABLE [TaskItems] ADD [SprintId] uniqueidentifier NULL;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[Notifications]', N'IdempotencyKey') IS NULL
    BEGIN
        ALTER TABLE [Notifications] ADD [IdempotencyKey] nvarchar(200) NULL;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF COL_LENGTH(N'[Notifications]', N'Tone') IS NULL
    BEGIN
        ALTER TABLE [Notifications] ADD [Tone] nvarchar(max) NOT NULL CONSTRAINT [DF_Notifications_Tone] DEFAULT N'';
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF OBJECT_ID(N'[Sprint]', N'U') IS NULL
    BEGIN
        CREATE TABLE [Sprint] (
            [Id] uniqueidentifier NOT NULL CONSTRAINT [DF_Sprint_Id] DEFAULT NEWID(),
            [Name] nvarchar(200) NOT NULL,
            [StartDate] datetimeoffset NOT NULL,
            [EndDate] datetimeoffset NOT NULL,
            [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Sprint_Status] DEFAULT N'Planning',
            [Goal] nvarchar(1000) NULL,
            [ProjectId] uniqueidentifier NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Sprint_CreatedAt] DEFAULT SYSDATETIMEOFFSET(),
            [UpdatedAt] datetimeoffset NULL,
            CONSTRAINT [PK_Sprint] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_Sprint_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
        );
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_WikiPages_ProjectId_Visibility'
          AND object_id = OBJECT_ID(N'[WikiPages]')
    )
    BEGIN
        CREATE INDEX [IX_WikiPages_ProjectId_Visibility]
        ON [WikiPages] ([ProjectId], [Visibility]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess'
          AND object_id = OBJECT_ID(N'[WebhookDeliveryLogs]')
    )
    BEGIN
        CREATE INDEX [IX_WebhookDeliveryLogs_WebhookId_IdempotencyKey_IsSuccess]
        ON [WebhookDeliveryLogs] ([WebhookId], [IdempotencyKey], [IsSuccess]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskItems_ProjectId_SprintId_Status'
          AND object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        CREATE INDEX [IX_TaskItems_ProjectId_SprintId_Status]
        ON [TaskItems] ([ProjectId], [SprintId], [Status]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TaskItems_SprintId'
          AND object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        CREATE INDEX [IX_TaskItems_SprintId]
        ON [TaskItems] ([SprintId]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Notifications_UserId_IdempotencyKey'
          AND object_id = OBJECT_ID(N'[Notifications]')
    )
    BEGIN
        CREATE UNIQUE INDEX [IX_Notifications_UserId_IdempotencyKey]
        ON [Notifications] ([UserId], [IdempotencyKey])
        WHERE [IdempotencyKey] IS NOT NULL;
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Sprint_ProjectId'
          AND object_id = OBJECT_ID(N'[Sprint]')
    )
    BEGIN
        CREATE INDEX [IX_Sprint_ProjectId]
        ON [Sprint] ([ProjectId]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Sprint_ProjectId_Status'
          AND object_id = OBJECT_ID(N'[Sprint]')
    )
    BEGIN
        CREATE INDEX [IX_Sprint_ProjectId_Status]
        ON [Sprint] ([ProjectId], [Status]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Sprint_Status'
          AND object_id = OBJECT_ID(N'[Sprint]')
    )
    BEGIN
        CREATE INDEX [IX_Sprint_Status]
        ON [Sprint] ([Status]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_TaskItems_Sprint_SprintId'
          AND parent_object_id = OBJECT_ID(N'[TaskItems]')
    )
    BEGIN
        ALTER TABLE [TaskItems]
        ADD CONSTRAINT [FK_TaskItems_Sprint_SprintId]
        FOREIGN KEY ([SprintId]) REFERENCES [Sprint] ([Id]);
    END
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524182249_AddPendingChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524182249_AddPendingChanges', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    ALTER TABLE [Projects] ADD [SourceGroupId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [WorkGroups] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(160) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [AvatarUrl] nvarchar(1000) NULL,
        [Color] nvarchar(20) NULL,
        [Status] nvarchar(20) NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_WorkGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkGroups_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkGroups_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupInvitations] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Token] nvarchar(128) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [AcceptedAt] datetimeoffset NULL,
        [RejectedAt] datetimeoffset NULL,
        [RevokedAt] datetimeoffset NULL,
        [InvitedByUserId] uniqueidentifier NOT NULL,
        [InvitedUserId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupInvitations_Users_InvitedByUserId] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupInvitations_Users_InvitedUserId] FOREIGN KEY ([InvitedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupInvitations_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupMeetingSessions] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [StartedByUserId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(50) NOT NULL,
        [RoomId] nvarchar(200) NOT NULL,
        [JoinUrl] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [EndedAt] datetimeoffset NULL,
        [TranscriptSourceId] nvarchar(200) NULL,
        [Summary] nvarchar(4000) NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupMeetingSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupMeetingSessions_Users_StartedByUserId] FOREIGN KEY ([StartedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupMeetingSessions_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupMessages] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [MessageType] nvarchar(30) NOT NULL,
        [EditedAt] datetimeoffset NULL,
        [DeletedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupMessages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupMessages_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupPolls] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [Question] nvarchar(500) NOT NULL,
        [AllowMultiple] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] nvarchar(20) NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [ClosedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupPolls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupPolls_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupPolls_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [WorkGroupMembers] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [JoinedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_WorkGroupMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkGroupMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WorkGroupMembers_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupPollOptions] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [GroupPollId] uniqueidentifier NOT NULL,
        [Text] nvarchar(300) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupPollOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupPollOptions_GroupPolls_GroupPollId] FOREIGN KEY ([GroupPollId]) REFERENCES [GroupPolls] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE TABLE [GroupPollVotes] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [GroupPollId] uniqueidentifier NOT NULL,
        [GroupPollOptionId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupPollVotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupPollVotes_GroupPollOptions_GroupPollOptionId] FOREIGN KEY ([GroupPollOptionId]) REFERENCES [GroupPollOptions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GroupPollVotes_GroupPolls_GroupPollId] FOREIGN KEY ([GroupPollId]) REFERENCES [GroupPolls] ([Id]),
        CONSTRAINT [FK_GroupPollVotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_Projects_SourceGroupId] ON [Projects] ([SourceGroupId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupInvitations_InvitedByUserId] ON [GroupInvitations] ([InvitedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupInvitations_InvitedUserId] ON [GroupInvitations] ([InvitedUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GroupInvitations_Token] ON [GroupInvitations] ([Token]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupInvitations_WorkGroupId_Email_Status] ON [GroupInvitations] ([WorkGroupId], [Email], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupMeetingSessions_RoomId] ON [GroupMeetingSessions] ([RoomId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupMeetingSessions_StartedByUserId] ON [GroupMeetingSessions] ([StartedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupMeetingSessions_WorkGroupId_Status_StartedAt] ON [GroupMeetingSessions] ([WorkGroupId], [Status], [StartedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupMessages_UserId] ON [GroupMessages] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupMessages_WorkGroupId_CreatedAt] ON [GroupMessages] ([WorkGroupId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPollOptions_GroupPollId_SortOrder] ON [GroupPollOptions] ([GroupPollId], [SortOrder]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPolls_CreatedByUserId] ON [GroupPolls] ([CreatedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPolls_WorkGroupId_Status_CreatedAt] ON [GroupPolls] ([WorkGroupId], [Status], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GroupPollVotes_GroupPollId_GroupPollOptionId_UserId] ON [GroupPollVotes] ([GroupPollId], [GroupPollOptionId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPollVotes_GroupPollId_UserId] ON [GroupPollVotes] ([GroupPollId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPollVotes_GroupPollOptionId] ON [GroupPollVotes] ([GroupPollOptionId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_GroupPollVotes_UserId] ON [GroupPollVotes] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_WorkGroupMembers_UserId] ON [WorkGroupMembers] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_WorkGroupMembers_WorkGroupId_Role] ON [WorkGroupMembers] ([WorkGroupId], [Role]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WorkGroupMembers_WorkGroupId_UserId] ON [WorkGroupMembers] ([WorkGroupId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_WorkGroups_OrganizationId] ON [WorkGroups] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_WorkGroups_OwnerId] ON [WorkGroups] ([OwnerId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    CREATE INDEX [IX_WorkGroups_Status] ON [WorkGroups] ([Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_WorkGroups_SourceGroupId] FOREIGN KEY ([SourceGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE SET NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526052013_AddWorkGroups'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526052013_AddWorkGroups', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WorkGroups] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WorkGroups] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WikiPages] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WikiPages] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskComments] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskComments] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskAttachments] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [TaskAttachments] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [Projects] ADD [DeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    ALTER TABLE [Projects] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526093427_AddSoftDeleteColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526093427_AddSoftDeleteColumns', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    ALTER TABLE [GroupInvitations] DROP CONSTRAINT [FK_GroupInvitations_Users_InvitedByUserId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    ALTER TABLE [GroupInvitations] DROP CONSTRAINT [FK_GroupInvitations_Users_InvitedUserId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    ALTER TABLE [GroupInvitations] DROP CONSTRAINT [FK_GroupInvitations_WorkGroups_WorkGroupId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DROP INDEX [IX_GroupInvitations_InvitedByUserId] ON [GroupInvitations];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DROP INDEX [IX_GroupInvitations_InvitedUserId] ON [GroupInvitations];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DROP INDEX [IX_GroupInvitations_WorkGroupId_Email_Status] ON [GroupInvitations];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupInvitations]') AND [c].[name] = N'AcceptedAt');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [GroupInvitations] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [GroupInvitations] DROP COLUMN [AcceptedAt];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupInvitations]') AND [c].[name] = N'InvitedByUserId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [GroupInvitations] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [GroupInvitations] DROP COLUMN [InvitedByUserId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupInvitations]') AND [c].[name] = N'InvitedUserId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [GroupInvitations] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [GroupInvitations] DROP COLUMN [InvitedUserId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupInvitations]') AND [c].[name] = N'RejectedAt');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [GroupInvitations] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [GroupInvitations] DROP COLUMN [RejectedAt];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GroupInvitations]') AND [c].[name] = N'RevokedAt');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [GroupInvitations] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [GroupInvitations] DROP COLUMN [RevokedAt];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    EXEC sp_rename N'[GroupInvitations].[WorkGroupId]', N'GroupId', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    EXEC sp_rename N'[GroupInvitations].[ExpiresAt]', N'ExpiredAt', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    CREATE INDEX [IX_GroupInvitations_GroupId] ON [GroupInvitations] ([GroupId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    ALTER TABLE [GroupInvitations] ADD CONSTRAINT [FK_GroupInvitations_WorkGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528035041_G201_AddGroupInvitationModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260528035041_G201_AddGroupInvitationModel', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollOptions] DROP CONSTRAINT [FK_GroupPollOptions_GroupPolls_GroupPollId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPolls] DROP CONSTRAINT [FK_GroupPolls_WorkGroups_WorkGroupId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollVotes] DROP CONSTRAINT [FK_GroupPollVotes_GroupPollOptions_GroupPollOptionId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollVotes] DROP CONSTRAINT [FK_GroupPollVotes_GroupPolls_GroupPollId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    DROP INDEX [IX_GroupPollVotes_GroupPollId_GroupPollOptionId_UserId] ON [GroupPollVotes];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    DROP INDEX [IX_GroupPollVotes_GroupPollId_UserId] ON [GroupPollVotes];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    DROP INDEX [IX_GroupPolls_WorkGroupId_Status_CreatedAt] ON [GroupPolls];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollVotes].[GroupPollId]', N'PollId', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollVotes].[GroupPollOptionId]', N'OptionId', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollVotes].[IX_GroupPollVotes_GroupPollOptionId]', N'IX_GroupPollVotes_OptionId', 'INDEX';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPolls].[WorkGroupId]', N'GroupId', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPolls].[ExpiresAt]', N'ExpiredAt', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollOptions].[Text]', N'Content', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollOptions].[GroupPollId]', N'PollId', 'COLUMN';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    EXEC sp_rename N'[GroupPollOptions].[IX_GroupPollOptions_GroupPollId_SortOrder]', N'IX_GroupPollOptions_PollId_SortOrder', 'INDEX';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GroupPollVotes_PollId_OptionId_UserId] ON [GroupPollVotes] ([PollId], [OptionId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    CREATE INDEX [IX_GroupPollVotes_PollId_UserId] ON [GroupPollVotes] ([PollId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    CREATE INDEX [IX_GroupPolls_GroupId] ON [GroupPolls] ([GroupId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    CREATE INDEX [IX_GroupPollOptions_PollId] ON [GroupPollOptions] ([PollId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollOptions] ADD CONSTRAINT [FK_GroupPollOptions_GroupPolls_PollId] FOREIGN KEY ([PollId]) REFERENCES [GroupPolls] ([Id]) ON DELETE CASCADE;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPolls] ADD CONSTRAINT [FK_GroupPolls_WorkGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollVotes] ADD CONSTRAINT [FK_GroupPollVotes_GroupPollOptions_OptionId] FOREIGN KEY ([OptionId]) REFERENCES [GroupPollOptions] ([Id]) ON DELETE CASCADE;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    ALTER TABLE [GroupPollVotes] ADD CONSTRAINT [FK_GroupPollVotes_GroupPolls_PollId] FOREIGN KEY ([PollId]) REFERENCES [GroupPolls] ([Id]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528064034_G401_AddGroupPollModels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260528064034_G401_AddGroupPollModels', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [IsPinned] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [PinnedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [PinnedByUserId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    CREATE TABLE [GroupMessageUserStates] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [GroupMessageId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [HiddenAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupMessageUserStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupMessageUserStates_GroupMessages_GroupMessageId] FOREIGN KEY ([GroupMessageId]) REFERENCES [GroupMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GroupMessageUserStates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    CREATE INDEX [IX_GroupMessages_WorkGroupId_IsPinned] ON [GroupMessages] ([WorkGroupId], [IsPinned]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GroupMessageUserStates_GroupMessageId_UserId] ON [GroupMessageUserStates] ([GroupMessageId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    CREATE INDEX [IX_GroupMessageUserStates_UserId_HiddenAt] ON [GroupMessageUserStates] ([UserId], [HiddenAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605072526_AddGroupMessageActions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605072526_AddGroupMessageActions', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605081653_AddGroupAttachments'
)
BEGIN
    CREATE TABLE [GroupAttachments] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WorkGroupId] uniqueidentifier NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [FileName] nvarchar(500) NOT NULL,
        [FilePath] nvarchar(1000) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GroupAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GroupAttachments_Users_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GroupAttachments_WorkGroups_WorkGroupId] FOREIGN KEY ([WorkGroupId]) REFERENCES [WorkGroups] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605081653_AddGroupAttachments'
)
BEGIN
    CREATE INDEX [IX_GroupAttachments_UploadedById] ON [GroupAttachments] ([UploadedById]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605081653_AddGroupAttachments'
)
BEGIN
    CREATE INDEX [IX_GroupAttachments_WorkGroupId_CreatedAt] ON [GroupAttachments] ([WorkGroupId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605081653_AddGroupAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605081653_AddGroupAttachments', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605082908_RemoveWorkGroupDescription'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkGroups]') AND [c].[name] = N'Description');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [WorkGroups] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [WorkGroups] DROP COLUMN [Description];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605082908_RemoveWorkGroupDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605082908_RemoveWorkGroupDescription', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606030301_AddGroupMessageReactions'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [ReactionSummaryJson] nvarchar(4000) NOT NULL DEFAULT N'[]';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606030301_AddGroupMessageReactions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606030301_AddGroupMessageReactions', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614054718_FixAiDecimalPrecision'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [Confidence] decimal(5,4) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614054718_FixAiDecimalPrecision'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [SchemaId] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614054718_FixAiDecimalPrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260614054718_FixAiDecimalPrecision', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615065331_AddWorkGroupChatBackground'
)
BEGIN
    ALTER TABLE [WorkGroups] ADD [BackgroundImageUrl] nvarchar(1000) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615065331_AddWorkGroupChatBackground'
)
BEGIN
    ALTER TABLE [WorkGroups] ADD [BackgroundTheme] nvarchar(40) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615065331_AddWorkGroupChatBackground'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615065331_AddWorkGroupChatBackground', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Projects] ADD [EnableInReview] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Projects] ADD [EnableOnHold] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Projects] ADD [RequireEvidenceToDone] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Projects] ADD [RestrictTransitionsToAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Organizations] ADD [AllowedEmailDomains] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Organizations] ADD [WorkspaceCover] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [Organizations] ADD [WorkspaceIcon] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    CREATE TABLE [PhysicalFiles] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ContentHash] nvarchar(256) NOT NULL,
        [FilePath] nvarchar(max) NOT NULL,
        [FileSize] bigint NOT NULL,
        [ReferenceCount] int NOT NULL DEFAULT 1,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PhysicalFiles] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [TaskAttachments] ADD [PhysicalFileId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN

                    INSERT INTO [PhysicalFiles] (Id, FilePath, FileSize, ContentHash, ReferenceCount, CreatedAt)
                    SELECT 
                        NEWID(), 
                        FilePath, 
                        MAX(FileSize), 
                        LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', FilePath), 2)), 
                        COUNT(*), 
                        SYSDATETIMEOFFSET()
                    FROM [TaskAttachments]
                    WHERE FilePath IS NOT NULL AND FilePath <> ''
                    GROUP BY FilePath;
                
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN

                    UPDATE ta
                    SET ta.PhysicalFileId = pf.Id
                    FROM [TaskAttachments] ta
                    INNER JOIN [PhysicalFiles] pf ON ta.FilePath = pf.FilePath;
                
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM [TaskAttachments] WHERE PhysicalFileId IS NULL)
                    BEGIN
                        DECLARE @DummyId UNIQUEIDENTIFIER = NEWID();
                        INSERT INTO [PhysicalFiles] (Id, FilePath, FileSize, ContentHash, ReferenceCount, CreatedAt)
                        VALUES (@DummyId, 'dummy_path', 0, 'dummy_hash', 1, SYSDATETIMEOFFSET());

                        UPDATE [TaskAttachments]
                        SET PhysicalFileId = @DummyId
                        WHERE PhysicalFileId IS NULL;
                    END
                
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaskAttachments]') AND [c].[name] = N'PhysicalFileId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [TaskAttachments] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [TaskAttachments] ALTER COLUMN [PhysicalFileId] uniqueidentifier NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaskAttachments]') AND [c].[name] = N'FilePath');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [TaskAttachments] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [TaskAttachments] DROP COLUMN [FilePath];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaskAttachments]') AND [c].[name] = N'FileSize');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [TaskAttachments] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [TaskAttachments] DROP COLUMN [FileSize];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    CREATE INDEX [IX_TaskAttachments_PhysicalFileId] ON [TaskAttachments] ([PhysicalFileId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PhysicalFiles_ContentHash] ON [PhysicalFiles] ([ContentHash]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    ALTER TABLE [TaskAttachments] ADD CONSTRAINT [FK_TaskAttachments_PhysicalFiles_PhysicalFileId] FOREIGN KEY ([PhysicalFileId]) REFERENCES [PhysicalFiles] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621040819_AddPhysicalFileForCAS'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621040819_AddPhysicalFileForCAS', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [ForwardedFromMessageId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD [ReplyToMessageId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    CREATE INDEX [IX_GroupMessages_ForwardedFromMessageId] ON [GroupMessages] ([ForwardedFromMessageId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    CREATE INDEX [IX_GroupMessages_ReplyToMessageId] ON [GroupMessages] ([ReplyToMessageId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD CONSTRAINT [FK_GroupMessages_GroupMessages_ForwardedFromMessageId] FOREIGN KEY ([ForwardedFromMessageId]) REFERENCES [GroupMessages] ([Id]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    ALTER TABLE [GroupMessages] ADD CONSTRAINT [FK_GroupMessages_GroupMessages_ReplyToMessageId] FOREIGN KEY ([ReplyToMessageId]) REFERENCES [GroupMessages] ([Id]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622023658_AddGroupMessageReplyAndForward'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260622023658_AddGroupMessageReplyAndForward', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623014453_AddGroupMemberReadState'
)
BEGIN
    ALTER TABLE [WorkGroupMembers] ADD [IsMuted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623014453_AddGroupMemberReadState'
)
BEGIN
    ALTER TABLE [WorkGroupMembers] ADD [LastReadAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET());
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623014453_AddGroupMemberReadState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260623014453_AddGroupMemberReadState', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] DROP CONSTRAINT [FK_AiJobs_Projects_ProjectId];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    DROP INDEX [IX_AiJobs_RequestedById] ON [AiJobs];
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiUsageLedger]') AND [c].[name] = N'EstimatedCostUsd');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [AiUsageLedger] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [AiUsageLedger] ALTER COLUMN [EstimatedCostUsd] decimal(18,6) NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD [ActualCostUsd] decimal(18,6) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD [AiJobId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD [PricingVersion] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD [ProviderAttemptId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiJobs]') AND [c].[name] = N'ProjectId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [AiJobs] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [AiJobs] ALTER COLUMN [ProjectId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ActualCostUsd] decimal(18,6) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [AvailableAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET());
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [BudgetPolicyId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [CacheHit] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [CanceledAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [CanceledById] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [CancellationRequestedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [CloudEligible] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ConsentId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [FinishedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [IdempotencyKey] nvarchar(160) NOT NULL DEFAULT (CONCAT(N'legacy:', CONVERT(nvarchar(36), NEWID())));
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [IsMock] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [LastErrorCode] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [LastErrorMessage] nvarchar(2000) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [LastErrorRetryable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [LegacyQueueItemId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [LegacyStatus] nvarchar(40) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [MaxAttempts] int NOT NULL DEFAULT 3;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [MaximumCostUsd] decimal(18,6) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [MockReason] nvarchar(200) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [NextRetryAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [PolicyCheckedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [PolicyDecisionJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [PricingVersion] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ProgressPercent] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ProviderRequestId] nvarchar(200) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [RequestHash] nvarchar(64) NOT NULL DEFAULT N'';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [RequestJson] nvarchar(max) NOT NULL DEFAULT N'{}';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ResultHash] nvarchar(64) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [ResultJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [RowVersion] rowversion NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [SchemaId] nvarchar(120) NOT NULL DEFAULT N'legacy.unresolved';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [SchemaVersion] nvarchar(40) NOT NULL DEFAULT N'legacy';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [SelectedModel] nvarchar(160) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [SelectedProvider] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [StartedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD [TenantId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [ConfirmationIdempotencyKey] nvarchar(160) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [ConfirmationResultJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [ExpiresAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [OriginalPayloadJson] nvarchar(max) NOT NULL DEFAULT N'{}';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [RejectedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [RejectedById] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [RejectionReason] nvarchar(1000) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [RowVersion] rowversion NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [SourceHashAtGeneration] nvarchar(64) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [WarningsJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD [WorkingPayloadJson] nvarchar(max) NOT NULL DEFAULT N'{}';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [AiJobId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [EntityGuid] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [EntityKey] nvarchar(200) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [ProviderAttemptId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE TABLE [AiJobDispatches] (
        [Id] uniqueidentifier NOT NULL,
        [AiJobId] uniqueidentifier NOT NULL,
        [Priority] int NOT NULL,
        [AvailableAt] datetimeoffset NOT NULL,
        [LeaseOwner] nvarchar(160) NULL,
        [LeaseExpiresAt] datetimeoffset NULL,
        [DeliveryCount] int NOT NULL,
        [LastDispatchErrorCode] nvarchar(80) NULL,
        [LastDispatchError] nvarchar(2000) NULL,
        [CompletedAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiJobDispatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiJobDispatches_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE TABLE [AiJobMigrationRecords] (
        [Id] uniqueidentifier NOT NULL,
        [LegacyQueueItemId] uniqueidentifier NOT NULL,
        [CanonicalAiJobId] uniqueidentifier NULL,
        [Classification] nvarchar(80) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [LegacySnapshotJson] nvarchar(max) NULL,
        [ReconciledAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiJobMigrationRecords] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE TABLE [AiJobSources] (
        [Id] uniqueidentifier NOT NULL,
        [AiJobId] uniqueidentifier NOT NULL,
        [SourceType] nvarchar(80) NOT NULL,
        [SourceEntityId] uniqueidentifier NULL,
        [LegacySourceKey] nvarchar(200) NULL,
        [SourceVersion] nvarchar(120) NULL,
        [SourceHash] nvarchar(64) NULL,
        [SourceTimestamp] datetimeoffset NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiJobSources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiJobSources_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE TABLE [AiProviderAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [AiJobId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ProviderName] nvarchar(80) NOT NULL,
        [ModelName] nvarchar(160) NOT NULL,
        [ProviderRequestId] nvarchar(200) NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [FinishedAt] datetimeoffset NULL,
        [LatencyMs] int NULL,
        [InputTokens] int NOT NULL,
        [OutputTokens] int NOT NULL,
        [EstimatedCostUsd] decimal(18,6) NOT NULL,
        [ActualCostUsd] decimal(18,6) NULL,
        [CacheHit] bit NOT NULL,
        [IsMock] bit NOT NULL,
        [MockReason] nvarchar(200) NULL,
        [RequestHash] nvarchar(64) NULL,
        [ResponseHash] nvarchar(64) NULL,
        [ErrorCode] nvarchar(80) NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [Retryable] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiProviderAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiProviderAttempts_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
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
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiUsageLedger_AiJobId_ProviderAttemptId] ON [AiUsageLedger] ([AiJobId], [ProviderAttemptId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiUsageLedger_ProviderAttemptId] ON [AiUsageLedger] ([ProviderAttemptId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiJobs_LegacyQueueItemId] ON [AiJobs] ([LegacyQueueItemId]) WHERE [LegacyQueueItemId] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiJobs_RequestedById_IdempotencyKey] ON [AiJobs] ([RequestedById], [IdempotencyKey]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiJobs_Status_AvailableAt] ON [AiJobs] ([Status], [AvailableAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiGeneratedDrafts_Id_ConfirmationIdempotencyKey] ON [AiGeneratedDrafts] ([Id], [ConfirmationIdempotencyKey]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiGeneratedDrafts_RejectedById] ON [AiGeneratedDrafts] ([RejectedById]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_AiJobId_CreatedAt] ON [AiAuditEvents] ([AiJobId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_ProviderAttemptId] ON [AiAuditEvents] ([ProviderAttemptId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiJobDispatches_AiJobId] ON [AiJobDispatches] ([AiJobId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiJobDispatches_AvailableAt_LeaseExpiresAt_Priority] ON [AiJobDispatches] ([AvailableAt], [LeaseExpiresAt], [Priority]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiJobMigrationRecords_CanonicalAiJobId] ON [AiJobMigrationRecords] ([CanonicalAiJobId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiJobMigrationRecords_LegacyQueueItemId] ON [AiJobMigrationRecords] ([LegacyQueueItemId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiJobSources_AiJobId_SortOrder] ON [AiJobSources] ([AiJobId], [SortOrder]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE INDEX [IX_AiJobSources_SourceType_SourceEntityId] ON [AiJobSources] ([SourceType], [SourceEntityId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiProviderAttempts_AiJobId_AttemptNumber] ON [AiProviderAttempts] ([AiJobId], [AttemptNumber]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiGeneratedDrafts] ADD CONSTRAINT [FK_AiGeneratedDrafts_Users_RejectedById] FOREIGN KEY ([RejectedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiJobs] ADD CONSTRAINT [FK_AiJobs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD CONSTRAINT [FK_AiUsageLedger_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    ALTER TABLE [AiUsageLedger] ADD CONSTRAINT [FK_AiUsageLedger_AiProviderAttempts_ProviderAttemptId] FOREIGN KEY ([ProviderAttemptId]) REFERENCES [AiProviderAttempts] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711192726_P002CanonicalAiJobPlatform'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260711192726_P002CanonicalAiJobPlatform', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN
    ALTER TABLE [TaskItems] ADD [Number] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN
    ALTER TABLE [Projects] ADD [TaskSequence] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN

    ;WITH numbered AS (
        SELECT Id, ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY CreatedAt, Id) AS rn
        FROM TaskItems
    )
    UPDATE t SET t.Number = n.rn
    FROM TaskItems t
    INNER JOIN numbered n ON t.Id = n.Id;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN

    UPDATE p SET p.TaskSequence = agg.MaxNumber
    FROM Projects p
    INNER JOIN (
        SELECT ProjectId, MAX(Number) AS MaxNumber
        FROM TaskItems
        GROUP BY ProjectId
    ) agg ON agg.ProjectId = p.Id;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskItems_ProjectId_Number] ON [TaskItems] ([ProjectId], [Number]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712061513_AddTaskKeyNumbering'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712061513_AddTaskKeyNumbering', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubInstallations] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [InstallationId] bigint NOT NULL,
        [AccountId] bigint NOT NULL,
        [AccountLogin] nvarchar(255) NOT NULL,
        [AccountType] nvarchar(20) NOT NULL,
        [InstalledByUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubInstallations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubInstallations_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubWebhookInbox] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [DeliveryId] nvarchar(100) NOT NULL,
        [EventName] nvarchar(100) NOT NULL,
        [InstallationId] bigint NULL,
        [RepositoryExternalId] bigint NULL,
        [OrganizationId] uniqueidentifier NULL,
        [Payload] nvarchar(max) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [AttemptCount] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        [ProcessedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubWebhookInbox] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [TaskDevelopmentLinks] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(40) NOT NULL,
        [ExternalEntityId] nvarchar(200) NOT NULL,
        [LinkSource] nvarchar(40) NOT NULL DEFAULT N'TaskKey',
        [Confidence] float NOT NULL,
        [LinkedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskDevelopmentLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskDevelopmentLinks_TaskItems_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubRepositoryConnections] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [GitHubInstallationId] uniqueidentifier NOT NULL,
        [RepositoryExternalId] bigint NOT NULL,
        [Owner] nvarchar(255) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [FullName] nvarchar(512) NOT NULL,
        [DefaultBranch] nvarchar(255) NOT NULL DEFAULT N'main',
        [IsPrivate] bit NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LastSyncedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubRepositoryConnections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubRepositoryConnections_GitHubInstallations_GitHubInstallationId] FOREIGN KEY ([GitHubInstallationId]) REFERENCES [GitHubInstallations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GitHubRepositoryConnections_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubCommits] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [RepositoryConnectionId] uniqueidentifier NOT NULL,
        [Sha] nvarchar(64) NOT NULL,
        [Message] nvarchar(4000) NOT NULL,
        [AuthorLogin] nvarchar(255) NULL,
        [AuthorEmailHash] nvarchar(128) NULL,
        [CommittedAt] datetimeoffset NOT NULL,
        [BranchName] nvarchar(255) NULL,
        [Url] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubCommits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubCommits_GitHubRepositoryConnections_RepositoryConnectionId] FOREIGN KEY ([RepositoryConnectionId]) REFERENCES [GitHubRepositoryConnections] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubPullRequests] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [RepositoryConnectionId] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [State] nvarchar(20) NOT NULL DEFAULT N'Open',
        [AuthorLogin] nvarchar(255) NULL,
        [HeadBranch] nvarchar(255) NOT NULL,
        [BaseBranch] nvarchar(255) NOT NULL,
        [IsDraft] bit NOT NULL,
        [OpenedAt] datetimeoffset NOT NULL,
        [GitHubUpdatedAt] datetimeoffset NULL,
        [MergedAt] datetimeoffset NULL,
        [MergedByLogin] nvarchar(255) NULL,
        [Url] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubPullRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubPullRequests_GitHubRepositoryConnections_RepositoryConnectionId] FOREIGN KEY ([RepositoryConnectionId]) REFERENCES [GitHubRepositoryConnections] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubReleases] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [RepositoryConnectionId] uniqueidentifier NOT NULL,
        [ReleaseExternalId] bigint NOT NULL,
        [TagName] nvarchar(255) NOT NULL,
        [Name] nvarchar(500) NULL,
        [IsDraft] bit NOT NULL,
        [IsPrerelease] bit NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [Url] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubReleases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubReleases_GitHubRepositoryConnections_RepositoryConnectionId] FOREIGN KEY ([RepositoryConnectionId]) REFERENCES [GitHubRepositoryConnections] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE TABLE [GitHubPullRequestReviews] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [PullRequestId] uniqueidentifier NOT NULL,
        [ReviewExternalId] bigint NOT NULL,
        [ReviewerLogin] nvarchar(255) NULL,
        [State] nvarchar(20) NOT NULL DEFAULT N'Commented',
        [SubmittedAt] datetimeoffset NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubPullRequestReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubPullRequestReviews_GitHubPullRequests_PullRequestId] FOREIGN KEY ([PullRequestId]) REFERENCES [GitHubPullRequests] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubCommits_OrganizationId] ON [GitHubCommits] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubCommits_RepositoryConnectionId_Sha] ON [GitHubCommits] ([RepositoryConnectionId], [Sha]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubInstallations_InstallationId] ON [GitHubInstallations] ([InstallationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubInstallations_OrganizationId] ON [GitHubInstallations] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubPullRequestReviews_OrganizationId] ON [GitHubPullRequestReviews] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubPullRequestReviews_PullRequestId_ReviewExternalId] ON [GitHubPullRequestReviews] ([PullRequestId], [ReviewExternalId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubPullRequests_OrganizationId] ON [GitHubPullRequests] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubPullRequests_RepositoryConnectionId_Number] ON [GitHubPullRequests] ([RepositoryConnectionId], [Number]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubPullRequests_State] ON [GitHubPullRequests] ([State]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubReleases_OrganizationId] ON [GitHubReleases] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubReleases_RepositoryConnectionId_ReleaseExternalId] ON [GitHubReleases] ([RepositoryConnectionId], [ReleaseExternalId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubRepositoryConnections_GitHubInstallationId] ON [GitHubRepositoryConnections] ([GitHubInstallationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubRepositoryConnections_OrganizationId] ON [GitHubRepositoryConnections] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubRepositoryConnections_ProjectId_RepositoryExternalId] ON [GitHubRepositoryConnections] ([ProjectId], [RepositoryExternalId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubWebhookInbox_DeliveryId] ON [GitHubWebhookInbox] ([DeliveryId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_GitHubWebhookInbox_Status] ON [GitHubWebhookInbox] ([Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE INDEX [IX_TaskDevelopmentLinks_OrganizationId] ON [TaskDevelopmentLinks] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskDevelopmentLinks_TaskId_EntityType_ExternalEntityId] ON [TaskDevelopmentLinks] ([TaskId], [EntityType], [ExternalEntityId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712064020_AddGitHubIntegrationSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712064020_AddGitHubIntegrationSchema', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [DeniedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [EvidenceJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [ExpiresAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [GrantedById] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [NoticeVersion] nvarchar(80) NOT NULL DEFAULT N'legacy-unknown';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [PolicyVersion] nvarchar(80) NOT NULL DEFAULT N'legacy-unknown';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [ProviderClass] nvarchar(30) NOT NULL DEFAULT N'unknown';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RequestId] nvarchar(120) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RevokedById] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [RowVersion] rowversion NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [SourceEntityId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD [SourceType] nvarchar(50) NOT NULL DEFAULT N'legacy';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ConsentId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ContentDeletedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ContentRedactedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [DataClassification] nvarchar(50) NOT NULL DEFAULT N'unknown_sensitive';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [PolicyVersion] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [PrivacyState] nvarchar(40) NOT NULL DEFAULT N'migration_review';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ProcessingPurpose] nvarchar(80) NOT NULL DEFAULT N'meeting_action_extraction';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [ProviderClass] nvarchar(30) NOT NULL DEFAULT N'unknown';
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [RetentionExpiresAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD [TenantId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AcceptedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [AvailableAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET());
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [DeadlineAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [DownloadCount] int NOT NULL DEFAULT 0;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [EncryptedResultPayload] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdempotencyKey] nvarchar(160) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdentityVerifiedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [IdentityVerifiedById] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastDownloadedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastErrorCode] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LastErrorMessage] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LeaseExpiresAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LeaseOwner] nvarchar(200) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LegalHoldDetected] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [LegalHoldReason] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [MaxAttempts] int NOT NULL DEFAULT 5;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [PolicyVersion] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [RequestHash] nvarchar(64) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultContentType] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultExpiresAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultFileName] nvarchar(200) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [ResultSummaryJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [RowVersion] rowversion NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [StartedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [DataSubjectRequests] ADD [SubjectUserId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [DataClassification] nvarchar(50) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [DataSubjectRequestId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [FailureCode] nvarchar(100) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [Outcome] nvarchar(40) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [PolicyVersion] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [PrivacyConsentId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [ProviderClass] nvarchar(30) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [Purpose] nvarchar(80) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [RequestId] nvarchar(120) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [AiAuditEvents] ADD [RetentionPolicyId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [PrivacyLegalHolds] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [SubjectUserId] uniqueidentifier NULL,
        [EntityType] nvarchar(80) NULL,
        [EntityId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [HeldAt] datetimeoffset NOT NULL,
        [HeldById] uniqueidentifier NOT NULL,
        [ReleasedAt] datetimeoffset NULL,
        [ReleasedById] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PrivacyLegalHolds] PRIMARY KEY ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [RetentionPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Name] nvarchar(120) NOT NULL,
        [DataClassification] nvarchar(50) NOT NULL,
        [Purpose] nvarchar(80) NOT NULL,
        [AllowedRetentionDaysJson] nvarchar(200) NOT NULL,
        [DefaultRetentionDays] int NOT NULL,
        [ExpiryAction] nvarchar(30) NOT NULL,
        [LegalHoldBehavior] nvarchar(40) NOT NULL,
        [AllowCloudProcessing] bit NOT NULL,
        [AllowLocalProcessing] bit NOT NULL,
        [RequireExplicitConsent] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [PolicyVersion] nvarchar(80) NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [ApprovalOwnerUserId] uniqueidentifier NULL,
        [EffectiveFrom] datetimeoffset NOT NULL,
        [EffectiveUntil] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_RetentionPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RetentionPolicies_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE TABLE [PrivacyRetentionActions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [RetentionPolicyId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [DueAt] datetimeoffset NOT NULL,
        [AvailableAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL DEFAULT 5,
        [LeaseOwner] nvarchar(200) NULL,
        [LeaseExpiresAt] datetimeoffset NULL,
        [StartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [LastErrorCode] nvarchar(100) NULL,
        [LastErrorMessage] nvarchar(max) NULL,
        [EvidenceJson] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PrivacyRetentionActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrivacyRetentionActions_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
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
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_RetentionPolicyId] ON [PrivacyConsents] ([RetentionPolicyId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_SourceType_SourceEntityId] ON [PrivacyConsents] ([SourceType], [SourceEntityId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyConsents_TenantId_ProjectId_UserId] ON [PrivacyConsents] ([TenantId], [ProjectId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_ConsentId] ON [MeetingImports] ([ConsentId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_PrivacyState_RetentionExpiresAt] ON [MeetingImports] ([PrivacyState], [RetentionExpiresAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_MeetingImports_RetentionPolicyId] ON [MeetingImports] ([RetentionPolicyId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_AvailableAt_LeaseExpiresAt] ON [DataSubjectRequests] ([AvailableAt], [LeaseExpiresAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DataSubjectRequests_TenantId_RequesterUserId_IdempotencyKey] ON [DataSubjectRequests] ([TenantId], [RequesterUserId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_TenantId_SubjectUserId_RequestedAt] ON [DataSubjectRequests] ([TenantId], [SubjectUserId], [RequestedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_DataSubjectRequestId] ON [AiAuditEvents] ([DataSubjectRequestId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_PrivacyConsentId] ON [AiAuditEvents] ([PrivacyConsentId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_AiAuditEvents_RetentionPolicyId] ON [AiAuditEvents] ([RetentionPolicyId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyLegalHolds_EntityType_EntityId_Status] ON [PrivacyLegalHolds] ([EntityType], [EntityId], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyLegalHolds_TenantId_ProjectId_SubjectUserId_Status] ON [PrivacyLegalHolds] ([TenantId], [ProjectId], [SubjectUserId], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrivacyRetentionActions_EntityType_EntityId_ActionType] ON [PrivacyRetentionActions] ([EntityType], [EntityId], [ActionType]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_RetentionPolicyId] ON [PrivacyRetentionActions] ([RetentionPolicyId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_Status_AvailableAt_LeaseExpiresAt] ON [PrivacyRetentionActions] ([Status], [AvailableAt], [LeaseExpiresAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_PrivacyRetentionActions_TenantId_ProjectId_DueAt] ON [PrivacyRetentionActions] ([TenantId], [ProjectId], [DueAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_RetentionPolicies_ProjectId] ON [RetentionPolicies] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RetentionPolicies_TenantId_PolicyVersion] ON [RetentionPolicies] ([TenantId], [PolicyVersion]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    CREATE INDEX [IX_RetentionPolicies_TenantId_ProjectId_DataClassification_Purpose_IsActive] ON [RetentionPolicies] ([TenantId], [ProjectId], [DataClassification], [Purpose], [IsActive]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD CONSTRAINT [FK_MeetingImports_PrivacyConsents_ConsentId] FOREIGN KEY ([ConsentId]) REFERENCES [PrivacyConsents] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [MeetingImports] ADD CONSTRAINT [FK_MeetingImports_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    ALTER TABLE [PrivacyConsents] ADD CONSTRAINT [FK_PrivacyConsents_RetentionPolicies_RetentionPolicyId] FOREIGN KEY ([RetentionPolicyId]) REFERENCES [RetentionPolicies] ([Id]) ON DELETE NO ACTION;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260712185807_P003PrivacyRetentionDsar'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260712185807_P003PrivacyRetentionDsar', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716145712_AddArchivedAtToProject'
)
BEGIN
    ALTER TABLE [Projects] ADD [ArchivedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716145712_AddArchivedAtToProject'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716145712_AddArchivedAtToProject', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722141358_EnforceSingleAdminRole'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Users_SingleAdmin] ON [Users] ([Role]) WHERE [Role] = ''Admin''');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722141358_EnforceSingleAdminRole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722141358_EnforceSingleAdminRole', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722145803_AddModeratorAssignments'
)
BEGIN
    CREATE TABLE [ModeratorAssignments] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ModeratorUserId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [Capability] nvarchar(100) NOT NULL,
        [GrantedByUserId] uniqueidentifier NOT NULL,
        [ExpiresAt] datetimeoffset NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RevokedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ModeratorAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModeratorAssignments_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ModeratorAssignments_Users_GrantedByUserId] FOREIGN KEY ([GrantedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ModeratorAssignments_Users_ModeratorUserId] FOREIGN KEY ([ModeratorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722145803_AddModeratorAssignments'
)
BEGIN
    CREATE INDEX [IX_ModeratorAssignments_GrantedByUserId] ON [ModeratorAssignments] ([GrantedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722145803_AddModeratorAssignments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ModeratorAssignments_ModeratorUserId_OrganizationId_Capability] ON [ModeratorAssignments] ([ModeratorUserId], [OrganizationId], [Capability]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722145803_AddModeratorAssignments'
)
BEGIN
    CREATE INDEX [IX_ModeratorAssignments_OrganizationId_IsActive_ExpiresAt] ON [ModeratorAssignments] ([OrganizationId], [IsActive], [ExpiresAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722145803_AddModeratorAssignments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722145803_AddModeratorAssignments', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726202027_P004AiBudgetPolicyIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiBudgetPolicies_ProjectId] ON [AiBudgetPolicies] ([ProjectId]) WHERE [ProjectId] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726202027_P004AiBudgetPolicyIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiBudgetPolicies_TenantId] ON [AiBudgetPolicies] ([TenantId]) WHERE [ProjectId] IS NULL AND [TenantId] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726202027_P004AiBudgetPolicyIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726202027_P004AiBudgetPolicyIntegrity', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE TABLE [OrganizationSkills] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [NormalizedName] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_OrganizationSkills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationSkills_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE TABLE [TaskSkillRequirements] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [TaskItemId] uniqueidentifier NOT NULL,
        [OrganizationSkillId] uniqueidentifier NOT NULL,
        [RequiredLevel] nvarchar(20) NOT NULL,
        [Provenance] nvarchar(20) NOT NULL,
        [ConfirmedByUserId] uniqueidentifier NOT NULL,
        [ConfirmedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskSkillRequirements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskSkillRequirements_OrganizationSkills_OrganizationSkillId] FOREIGN KEY ([OrganizationSkillId]) REFERENCES [OrganizationSkills] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskSkillRequirements_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskSkillRequirements_Users_ConfirmedByUserId] FOREIGN KEY ([ConfirmedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE INDEX [IX_OrganizationSkills_OrganizationId_IsActive] ON [OrganizationSkills] ([OrganizationId], [IsActive]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationSkills_OrganizationId_NormalizedName] ON [OrganizationSkills] ([OrganizationId], [NormalizedName]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE INDEX [IX_TaskSkillRequirements_ConfirmedByUserId] ON [TaskSkillRequirements] ([ConfirmedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE INDEX [IX_TaskSkillRequirements_OrganizationSkillId] ON [TaskSkillRequirements] ([OrganizationSkillId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskSkillRequirements_TaskItemId_OrganizationSkillId] ON [TaskSkillRequirements] ([TaskItemId], [OrganizationSkillId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726224810_P005TaskSkillTaxonomy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726224810_P005TaskSkillTaxonomy', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801141320_P006AiActionComposerActivity'
)
BEGIN
    CREATE TABLE [AiJobActivityEvents] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [AiJobId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Stage] nvarchar(60) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [PublicLabel] nvarchar(200) NOT NULL,
        [SafeDetailJson] nvarchar(max) NULL,
        [Current] int NULL,
        [Total] int NULL,
        [Attempt] int NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [DurationMs] int NULL,
        [Retryable] bit NOT NULL,
        [ReceiptLink] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AiJobActivityEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiJobActivityEvents_AiJobs_AiJobId] FOREIGN KEY ([AiJobId]) REFERENCES [AiJobs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801141320_P006AiActionComposerActivity'
)
BEGIN
    CREATE INDEX [IX_AiJobActivityEvents_AiJobId_CreatedAt] ON [AiJobActivityEvents] ([AiJobId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801141320_P006AiActionComposerActivity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiJobActivityEvents_AiJobId_Sequence] ON [AiJobActivityEvents] ([AiJobId], [Sequence]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801141320_P006AiActionComposerActivity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801141320_P006AiActionComposerActivity', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE TABLE [AssistantSessions] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [Title] nvarchar(160) NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [Version] bigint NOT NULL,
        [LastSequence] int NOT NULL,
        [ArchivedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AssistantSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantSessions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AssistantSessions_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE TABLE [AssistantTurns] (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [ClientTurnId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [UserMessage] nvarchar(max) NOT NULL,
        [RequestContextJson] nvarchar(max) NULL,
        [Status] nvarchar(24) NOT NULL,
        [Disposition] nvarchar(40) NULL,
        [Intent] nvarchar(80) NULL,
        [ExecutionPolicy] nvarchar(40) NULL,
        [AssistantResponse] nvarchar(max) NULL,
        [ResponseJson] nvarchar(max) NULL,
        [SourceRefsJson] nvarchar(max) NULL,
        [ModelProfile] nvarchar(60) NOT NULL,
        [ActualProvider] nvarchar(80) NULL,
        [ActualModel] nvarchar(120) NULL,
        [CorrelationId] nvarchar(120) NOT NULL,
        [SafeErrorCode] nvarchar(100) NULL,
        [CompletedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AssistantTurns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantTurns_AssistantSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [AssistantSessions] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE TABLE [AssistantArtifactRefs] (
        [Id] uniqueidentifier NOT NULL,
        [TurnId] uniqueidentifier NOT NULL,
        [SchemaId] nvarchar(120) NOT NULL,
        [SchemaVersion] nvarchar(30) NOT NULL,
        [AiJobId] uniqueidentifier NULL,
        [DraftId] uniqueidentifier NULL,
        [ReceiptId] uniqueidentifier NULL,
        [RendererId] nvarchar(120) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AssistantArtifactRefs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantArtifactRefs_AssistantTurns_TurnId] FOREIGN KEY ([TurnId]) REFERENCES [AssistantTurns] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE TABLE [AssistantProcessEvents] (
        [Id] uniqueidentifier NOT NULL,
        [TurnId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Stage] nvarchar(60) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [PublicLabel] nvarchar(200) NOT NULL,
        [SafeDetailJson] nvarchar(max) NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [DurationMs] int NULL,
        [Retryable] bit NOT NULL,
        [SafeErrorCode] nvarchar(100) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AssistantProcessEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantProcessEvents_AssistantTurns_TurnId] FOREIGN KEY ([TurnId]) REFERENCES [AssistantTurns] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantArtifactRefs_AiJobId] ON [AssistantArtifactRefs] ([AiJobId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantArtifactRefs_DraftId] ON [AssistantArtifactRefs] ([DraftId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantArtifactRefs_TurnId_SchemaId] ON [AssistantArtifactRefs] ([TurnId], [SchemaId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssistantProcessEvents_TurnId_Sequence] ON [AssistantProcessEvents] ([TurnId], [Sequence]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantSessions_OwnerUserId_Status_UpdatedAt] ON [AssistantSessions] ([OwnerUserId], [Status], [UpdatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantSessions_ProjectId] ON [AssistantSessions] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantSessions_TenantId_OwnerUserId] ON [AssistantSessions] ([TenantId], [OwnerUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_ClientTurnId] ON [AssistantTurns] ([SessionId], [ClientTurnId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE INDEX [IX_AssistantTurns_SessionId_CreatedAt] ON [AssistantTurns] ([SessionId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_IdempotencyKey] ON [AssistantTurns] ([SessionId], [IdempotencyKey]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssistantTurns_SessionId_Sequence] ON [AssistantTurns] ([SessionId], [Sequence]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802063926_P007AssistantSessionTurnChain'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802063926_P007AssistantSessionTurnChain', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    CREATE TABLE [TaskCompletionAttributions] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [TaskItemId] uniqueidentifier NOT NULL,
        [ContributorUserId] uniqueidentifier NOT NULL,
        [ConfirmedByUserId] uniqueidentifier NOT NULL,
        [CompletedAt] datetimeoffset NOT NULL,
        [ConfirmedAt] datetimeoffset NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [AttributionPolicyVersion] nvarchar(80) NOT NULL,
        [CorrectionReason] nvarchar(500) NULL,
        [CorrectionRequestedAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_TaskCompletionAttributions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskCompletionAttributions_TaskItems_TaskItemId] FOREIGN KEY ([TaskItemId]) REFERENCES [TaskItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskCompletionAttributions_Users_ConfirmedByUserId] FOREIGN KEY ([ConfirmedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskCompletionAttributions_Users_ContributorUserId] FOREIGN KEY ([ContributorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    CREATE INDEX [IX_TaskCompletionAttributions_ConfirmedByUserId] ON [TaskCompletionAttributions] ([ConfirmedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    CREATE INDEX [IX_TaskCompletionAttributions_ContributorUserId_Status] ON [TaskCompletionAttributions] ([ContributorUserId], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskCompletionAttributions_TaskItemId_ContributorUserId] ON [TaskCompletionAttributions] ([TaskItemId], [ContributorUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    CREATE INDEX [IX_TaskCompletionAttributions_TaskItemId_Status] ON [TaskCompletionAttributions] ([TaskItemId], [Status]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802114055_P008MemberSkillEvidence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802114055_P008MemberSkillEvidence', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    CREATE TABLE [OrganizationMemberCapacityProfiles] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WeeklyCapacityHours] decimal(6,2) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_OrganizationMemberCapacityProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationMemberCapacityProfiles_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrganizationMemberCapacityProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    CREATE TABLE [MemberAvailabilityWindows] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationMemberCapacityProfileId] uniqueidentifier NOT NULL,
        [StartsAt] datetimeoffset NOT NULL,
        [EndsAt] datetimeoffset NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [AvailableHours] decimal(6,2) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MemberAvailabilityWindows] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MemberAvailabilityWindows_OrganizationMemberCapacityProfiles_OrganizationMemberCapacityProfileId] FOREIGN KEY ([OrganizationMemberCapacityProfileId]) REFERENCES [OrganizationMemberCapacityProfiles] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    CREATE INDEX [IX_MemberAvailabilityWindows_OrganizationMemberCapacityProfileId_StartsAt_EndsAt] ON [MemberAvailabilityWindows] ([OrganizationMemberCapacityProfileId], [StartsAt], [EndsAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationMemberCapacityProfiles_OrganizationId_UserId] ON [OrganizationMemberCapacityProfiles] ([OrganizationId], [UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    CREATE INDEX [IX_OrganizationMemberCapacityProfiles_UserId] ON [OrganizationMemberCapacityProfiles] ([UserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802124551_P009PortfolioCapacity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802124551_P009PortfolioCapacity', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804054010_AddGitHubWorkflowRuns'
)
BEGIN
    CREATE TABLE [GitHubWorkflowRuns] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [RepositoryConnectionId] uniqueidentifier NOT NULL,
        [RunExternalId] bigint NOT NULL,
        [WorkflowName] nvarchar(255) NOT NULL,
        [DisplayTitle] nvarchar(500) NULL,
        [Branch] nvarchar(255) NOT NULL,
        [CommitSha] nvarchar(64) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Conclusion] nvarchar(40) NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        [Url] nvarchar(1000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_GitHubWorkflowRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitHubWorkflowRuns_GitHubRepositoryConnections_RepositoryConnectionId] FOREIGN KEY ([RepositoryConnectionId]) REFERENCES [GitHubRepositoryConnections] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804054010_AddGitHubWorkflowRuns'
)
BEGIN
    CREATE INDEX [IX_GitHubWorkflowRuns_OrganizationId] ON [GitHubWorkflowRuns] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804054010_AddGitHubWorkflowRuns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GitHubWorkflowRuns_RepositoryConnectionId_RunExternalId] ON [GitHubWorkflowRuns] ([RepositoryConnectionId], [RunExternalId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804054010_AddGitHubWorkflowRuns'
)
BEGIN
    CREATE INDEX [IX_GitHubWorkflowRuns_Status_Conclusion] ON [GitHubWorkflowRuns] ([Status], [Conclusion]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804054010_AddGitHubWorkflowRuns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804054010_AddGitHubWorkflowRuns', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    ALTER TABLE [AssistantTurns] ADD [CancellationRequestedAt] datetimeoffset NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    ALTER TABLE [AssistantTurns] ADD [RequestPayloadJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    ALTER TABLE [AssistantTurns] ADD [ResumedFromTurnId] uniqueidentifier NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE TABLE [OrganizationWorkRuleSets] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [Status] nvarchar(24) NOT NULL,
        [EffectiveFrom] datetimeoffset NULL,
        [EffectiveUntil] datetimeoffset NULL,
        [RulesJson] nvarchar(max) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [ActivatedByUserId] uniqueidentifier NULL,
        [ActivatedAt] datetimeoffset NULL,
        [Revision] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_OrganizationWorkRuleSets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationWorkRuleSets_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE TABLE [ProjectLaunchBriefs] (
        [Id] uniqueidentifier NOT NULL,
        [AssistantSessionId] uniqueidentifier NOT NULL,
        [AssistantTurnId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [RuleSetId] uniqueidentifier NULL,
        [Revision] int NOT NULL,
        [State] nvarchar(40) NOT NULL,
        [BriefJson] nvarchar(max) NOT NULL,
        [SourceSnapshotJson] nvarchar(max) NOT NULL,
        [PromptVersion] nvarchar(100) NOT NULL,
        [ActualProvider] nvarchar(80) NOT NULL,
        [ActualModel] nvarchar(120) NOT NULL,
        [RowRevision] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectLaunchBriefs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectLaunchBriefs_AssistantSessions_AssistantSessionId] FOREIGN KEY ([AssistantSessionId]) REFERENCES [AssistantSessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchBriefs_AssistantTurns_AssistantTurnId] FOREIGN KEY ([AssistantTurnId]) REFERENCES [AssistantTurns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectLaunchBriefs_OrganizationWorkRuleSets_RuleSetId] FOREIGN KEY ([RuleSetId]) REFERENCES [OrganizationWorkRuleSets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchBriefs_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE TABLE [OrganizationWorkRuleDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectLaunchBriefId] uniqueidentifier NOT NULL,
        [RuleSetId] uniqueidentifier NULL,
        [RuleSetVersion] int NULL,
        [RuleKey] nvarchar(100) NOT NULL,
        [Result] nvarchar(20) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Explanation] nvarchar(1000) NOT NULL,
        [DeterministicFactsJson] nvarchar(max) NOT NULL,
        [ExceptionEligible] bit NOT NULL,
        [SourceFreshness] nvarchar(120) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_OrganizationWorkRuleDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationWorkRuleDecisions_ProjectLaunchBriefs_ProjectLaunchBriefId] FOREIGN KEY ([ProjectLaunchBriefId]) REFERENCES [ProjectLaunchBriefs] ([Id]) ON DELETE CASCADE
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationWorkRuleDecisions_ProjectLaunchBriefId_RuleKey] ON [OrganizationWorkRuleDecisions] ([ProjectLaunchBriefId], [RuleKey]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE INDEX [IX_OrganizationWorkRuleSets_OrganizationId_Status_EffectiveFrom] ON [OrganizationWorkRuleSets] ([OrganizationId], [Status], [EffectiveFrom]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationWorkRuleSets_OrganizationId_Version] ON [OrganizationWorkRuleSets] ([OrganizationId], [Version]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchBriefs_AssistantSessionId] ON [ProjectLaunchBriefs] ([AssistantSessionId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectLaunchBriefs_AssistantTurnId] ON [ProjectLaunchBriefs] ([AssistantTurnId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchBriefs_OrganizationId_CreatedAt] ON [ProjectLaunchBriefs] ([OrganizationId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchBriefs_RuleSetId] ON [ProjectLaunchBriefs] ([RuleSetId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809190001_P010AiNativeReadLoopConversationLaunchBrief', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE TABLE [ProjectLaunchPlanArtifacts] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectLaunchBriefId] uniqueidentifier NOT NULL,
        [AssistantSessionId] uniqueidentifier NOT NULL,
        [AssistantTurnId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [RuleSetId] uniqueidentifier NULL,
        [Revision] int NOT NULL,
        [State] nvarchar(40) NOT NULL,
        [StaffingScenariosJson] nvarchar(max) NOT NULL,
        [DeliveryPlanJson] nvarchar(max) NOT NULL,
        [BlockingReasonsJson] nvarchar(max) NOT NULL,
        [WarningsJson] nvarchar(max) NOT NULL,
        [SourceSnapshotJson] nvarchar(max) NOT NULL,
        [SourceVersionHash] nvarchar(128) NOT NULL,
        [SelectedScenarioId] nvarchar(80) NULL,
        [ScoringVersion] nvarchar(100) NOT NULL,
        [PromptVersion] nvarchar(100) NOT NULL,
        [ActualProvider] nvarchar(80) NOT NULL,
        [ActualModel] nvarchar(120) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [RowRevision] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectLaunchPlanArtifacts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectLaunchPlanArtifacts_AssistantSessions_AssistantSessionId] FOREIGN KEY ([AssistantSessionId]) REFERENCES [AssistantSessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchPlanArtifacts_AssistantTurns_AssistantTurnId] FOREIGN KEY ([AssistantTurnId]) REFERENCES [AssistantTurns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectLaunchPlanArtifacts_OrganizationWorkRuleSets_RuleSetId] FOREIGN KEY ([RuleSetId]) REFERENCES [OrganizationWorkRuleSets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchPlanArtifacts_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchPlanArtifacts_ProjectLaunchBriefs_ProjectLaunchBriefId] FOREIGN KEY ([ProjectLaunchBriefId]) REFERENCES [ProjectLaunchBriefs] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE TABLE [ProjectLaunchExecutions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectLaunchPlanArtifactId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [IdempotencyKey] nvarchar(200) NOT NULL,
        [PayloadHash] nvarchar(128) NOT NULL,
        [ReceiptJson] nvarchar(max) NOT NULL,
        [ExecutedByUserId] uniqueidentifier NOT NULL,
        [ExecutedAt] datetimeoffset NOT NULL,
        [VerifiedAt] datetimeoffset NULL,
        [MonitoringEnabled] bit NOT NULL,
        [NextMonitorAt] datetimeoffset NULL,
        [LastMonitoredAt] datetimeoffset NULL,
        [RolledBackAt] datetimeoffset NULL,
        [RollbackReason] nvarchar(1000) NULL,
        [RollbackIdempotencyKey] nvarchar(200) NULL,
        [RollbackPayloadHash] nvarchar(128) NULL,
        [RowRevision] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectLaunchExecutions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectLaunchExecutions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchExecutions_ProjectLaunchPlanArtifacts_ProjectLaunchPlanArtifactId] FOREIGN KEY ([ProjectLaunchPlanArtifactId]) REFERENCES [ProjectLaunchPlanArtifacts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectLaunchExecutions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE TABLE [ProjectReplanProposals] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectLaunchPlanArtifactId] uniqueidentifier NOT NULL,
        [ProjectLaunchExecutionId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Revision] int NOT NULL,
        [State] nvarchar(40) NOT NULL,
        [TriggerCodesJson] nvarchar(max) NOT NULL,
        [ProposalJson] nvarchar(max) NOT NULL,
        [SourceSnapshotJson] nvarchar(max) NOT NULL,
        [BaselineHash] nvarchar(128) NOT NULL,
        [CurrentHash] nvarchar(128) NOT NULL,
        [CreatedByUserId] uniqueidentifier NULL,
        [RowRevision] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectReplanProposals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectReplanProposals_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectReplanProposals_ProjectLaunchExecutions_ProjectLaunchExecutionId] FOREIGN KEY ([ProjectLaunchExecutionId]) REFERENCES [ProjectLaunchExecutions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectReplanProposals_ProjectLaunchPlanArtifacts_ProjectLaunchPlanArtifactId] FOREIGN KEY ([ProjectLaunchPlanArtifactId]) REFERENCES [ProjectLaunchPlanArtifacts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProjectReplanProposals_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectLaunchExecutions_IdempotencyKey] ON [ProjectLaunchExecutions] ([IdempotencyKey]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchExecutions_MonitoringEnabled_NextMonitorAt] ON [ProjectLaunchExecutions] ([MonitoringEnabled], [NextMonitorAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchExecutions_OrganizationId] ON [ProjectLaunchExecutions] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchExecutions_ProjectId] ON [ProjectLaunchExecutions] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectLaunchExecutions_ProjectLaunchPlanArtifactId] ON [ProjectLaunchExecutions] ([ProjectLaunchPlanArtifactId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProjectLaunchExecutions_RollbackIdempotencyKey] ON [ProjectLaunchExecutions] ([RollbackIdempotencyKey]) WHERE [RollbackIdempotencyKey] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchPlanArtifacts_AssistantSessionId] ON [ProjectLaunchPlanArtifacts] ([AssistantSessionId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectLaunchPlanArtifacts_AssistantTurnId] ON [ProjectLaunchPlanArtifacts] ([AssistantTurnId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchPlanArtifacts_OrganizationId_CreatedAt] ON [ProjectLaunchPlanArtifacts] ([OrganizationId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectLaunchPlanArtifacts_ProjectLaunchBriefId_Revision] ON [ProjectLaunchPlanArtifacts] ([ProjectLaunchBriefId], [Revision]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectLaunchPlanArtifacts_RuleSetId] ON [ProjectLaunchPlanArtifacts] ([RuleSetId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectReplanProposals_OrganizationId] ON [ProjectReplanProposals] ([OrganizationId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectReplanProposals_ProjectId] ON [ProjectReplanProposals] ([ProjectId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE INDEX [IX_ProjectReplanProposals_ProjectLaunchExecutionId_CreatedAt] ON [ProjectReplanProposals] ([ProjectLaunchExecutionId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectReplanProposals_ProjectLaunchPlanArtifactId_Revision] ON [ProjectReplanProposals] ([ProjectLaunchPlanArtifactId], [Revision]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809201444_P011ProjectLaunchOrchestration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809201444_P011ProjectLaunchOrchestration', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810090000_P012AssistantConversationContinuity'
)
BEGIN
    ALTER TABLE [AssistantSessions] ADD [ClarificationDraftJson] nvarchar(max) NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810090000_P012AssistantConversationContinuity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810090000_P012AssistantConversationContinuity', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    CREATE TABLE [AssistantTestRuns] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [OriginTurnId] uniqueidentifier NOT NULL,
        [ManifestId] nvarchar(80) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [IdempotencyKey] nvarchar(180) NULL,
        [Revision] bigint NOT NULL,
        [EventsJson] nvarchar(max) NOT NULL,
        [SafeSummary] nvarchar(600) NULL,
        [SafeErrorCode] nvarchar(80) NULL,
        [ConfirmedAt] datetimeoffset NULL,
        [StartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_AssistantTestRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssistantTestRuns_AssistantSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [AssistantSessions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AssistantTestRuns_AssistantTurns_OriginTurnId] FOREIGN KEY ([OriginTurnId]) REFERENCES [AssistantTurns] ([Id]),
        CONSTRAINT [FK_AssistantTestRuns_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssistantTestRuns_IdempotencyKey] ON [AssistantTestRuns] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssistantTestRuns_OriginTurnId] ON [AssistantTestRuns] ([OriginTurnId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    CREATE INDEX [IX_AssistantTestRuns_OwnerUserId_CreatedAt] ON [AssistantTestRuns] ([OwnerUserId], [CreatedAt]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    CREATE INDEX [IX_AssistantTestRuns_SessionId] ON [AssistantTestRuns] ([SessionId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810100000_P013SafeTestOrchestrator'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810100000_P013SafeTestOrchestrator', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811120548_P014ProjectRoleDefinitions'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProjectMembers]') AND [c].[name] = N'Role');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [ProjectMembers] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [ProjectMembers] ALTER COLUMN [Role] nvarchar(64) NOT NULL;
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811120548_P014ProjectRoleDefinitions'
)
BEGIN
    CREATE TABLE [ProjectRoleDefinitions] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [OrganizationId] uniqueidentifier NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(80) NOT NULL,
        [Description] nvarchar(400) NULL,
        [BaseRole] nvarchar(32) NOT NULL,
        [SkillTags] nvarchar(400) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ProjectRoleDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectRoleDefinitions_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectRoleDefinitions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id])
    );
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811120548_P014ProjectRoleDefinitions'
)
BEGIN
    CREATE INDEX [IX_ProjectRoleDefinitions_CreatedByUserId] ON [ProjectRoleDefinitions] ([CreatedByUserId]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811120548_P014ProjectRoleDefinitions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectRoleDefinitions_OrganizationId_Key] ON [ProjectRoleDefinitions] ([OrganizationId], [Key]);
END;

GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811120548_P014ProjectRoleDefinitions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811120548_P014ProjectRoleDefinitions', N'10.0.7');
END;

COMMIT;
GO

